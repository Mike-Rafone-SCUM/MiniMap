"""Compile classified road curves, height-checked seams and explicit bridge surveys."""
import argparse
import gzip
import hashlib
import json
import math
import struct
from collections import defaultdict
from pathlib import Path


class WaterMask:
    def __init__(self, path):
        data = gzip.decompress(Path(path).read_bytes())
        if data[:4] != b'WATR':
            raise ValueError('Invalid water mask')
        self.width, self.height = struct.unpack_from('<ii', data, 4)
        self.bits = data[12:]
        if not (0 < self.width <= 8192 and 0 < self.height <= 8192) or len(self.bits) != (self.width*self.height+7)//8:
            raise ValueError('Invalid water coverage')

    def allows(self, a, b):
        a, b = uv(a), uv(b)
        count = max(1, math.ceil(max(abs(a[0]-b[0])*self.width, abs(a[1]-b[1])*self.height)*2))
        for i in range(count+1):
            x, y = (a[d]+(b[d]-a[d])*i/count for d in (0, 1))
            if not (0 <= x < 1 and 0 <= y < 1):
                return False
            index = int(y*self.height)*self.width+int(x*self.width)
            if self.bits[index//8] & (1 << (index % 8)):
                return False
        return True


def uv(point):
    if len(point) != 3 or not all(math.isfinite(v) for v in point):
        raise ValueError('Invalid world position')
    return ((617718.0-point[0])/1521618.0, (618618.0-point[1])/1523618.0)


def project(p, a, b):
    dx, dy = b[0]-a[0], b[1]-a[1]
    squared = dx*dx+dy*dy
    t = max(0, min(1, ((p[0]-a[0])*dx+(p[1]-a[1])*dy)/squared)) if squared else 0
    q = [a[d]+(b[d]-a[d])*t for d in range(3)]
    return math.dist(p[:2], q[:2]), t, q


def compile_network(graph, allowed, water=None, bridges=(), map_links=()):
    if graph.get('units') != 'cm' or graph.get('coordinateSpace') != 'world':
        raise ValueError('Expected world-space centimeters')
    nodes, edges, identities, world, selected, roads = [], [], {}, [], [], []
    bridge_edges = set()
    for sid, segment in graph['segments'].items():
        if not (segment['connectedGeometryValid'] and segment['meshes']
                and set(segment['meshes']).issubset(allowed)):
            continue
        points = segment['center']
        length = segment['lengthCm']/100.0
        if len(points) < 2 or not math.isfinite(length) or length <= 0:
            raise ValueError('Invalid road geometry: '+sid)
        endpoints = segment['endpoints']
        if len(endpoints) != 2:
            raise ValueError('Invalid endpoints: '+sid)
        for endpoint, point in zip(endpoints, (points[0], points[-1])):
            position = graph['nodes'][endpoint]['position']
            if math.dist(position, point) > 5.0:
                raise ValueError('Endpoint does not match captured connection: '+sid)
            if endpoint not in identities:
                identities[endpoint] = len(nodes)
                nodes.append(uv(position))
                world.append(position)
        cumulative = [0.0]
        for a, b in zip(points, points[1:]):
            uv(a); uv(b)
            cumulative.append(cumulative[-1]+math.dist(a, b))
        u, v = (identities[n] for n in endpoints)
        roads.append(dict(points=points, cumulative=cumulative, u=u, v=v,
                          bridge=any('Bridge' in m or '/Dam/' in m for m in segment['meshes']),
                          cuts=[(0.0, u, points[0]), (cumulative[-1], v, points[-1])]))
        selected.append(sid)

    parent = list(range(len(nodes)))
    def root(n):
        while parent[n] != n:
            parent[n] = parent[parent[n]]
            n = parent[n]
        return n

    degree = [0]*len(nodes)
    for road in roads:
        parent[root(road['u'])] = root(road['v'])
        degree[road['u']] += 1; degree[road['v']] += 1

    # Index complete line intervals, not just samples: T-junctions often meet the
    # middle of a captured spline. Never connect two crossing roads by XY alone.
    cells = defaultdict(list)
    cell_size, seam_cm = 2000.0, 500.0
    for ri, road in enumerate(roads):
        for pi, (a, b) in enumerate(zip(road['points'], road['points'][1:])):
            for x in range(math.floor(min(a[0], b[0])/cell_size), math.floor(max(a[0], b[0])/cell_size)+1):
                for y in range(math.floor(min(a[1], b[1])/cell_size), math.floor(max(a[1], b[1])/cell_size)+1):
                    cells[x, y].append((ri, pi))

    def cut(ri, pi, t, point):
        road = roads[ri]
        offset = road['cumulative'][pi]+t*(road['cumulative'][pi+1]-road['cumulative'][pi])
        for at, node, _ in road['cuts']:
            if abs(at-offset) <= .01:
                return node
        node = len(nodes)
        nodes.append(uv(point)); world.append(point); parent.append(root(road['u']))
        road['cuts'].append((offset, node, point))
        return node

    seams, seam_pairs = [], set()
    if water is not None:
        for node in range(len(degree)):
            if degree[node] != 1:
                continue
            p = world[node]
            nearby = set()
            for x in range(math.floor((p[0]-seam_cm)/cell_size), math.floor((p[0]+seam_cm)/cell_size)+1):
                for y in range(math.floor((p[1]-seam_cm)/cell_size), math.floor((p[1]+seam_cm)/cell_size)+1):
                    nearby.update(cells[x, y])
            candidates = {}
            for ri, pi in sorted(nearby):
                road = roads[ri]
                if node in (road['u'], road['v']):
                    continue
                distance, t, point = project(p, *road['points'][pi:pi+2])
                height = abs(p[2]-point[2])
                if distance <= seam_cm and height <= min(200.0, max(30.0, distance*.25)):
                    if math.dist(p, point) <= 5.0 or water.allows(p, point):
                        candidate = (distance, ri, pi, t, point)
                        if ri not in candidates or candidate < candidates[ri]:
                            candidates[ri] = candidate
            for distance, ri, pi, t, point in sorted(candidates.values()):
                other = cut(ri, pi, t, point)
                pair = tuple(sorted((node, other)))
                if node == other or pair in seam_pairs:
                    continue
                seam_pairs.add(pair)
                parent[root(node)] = root(other)
                edges.append((node, other, max(.001, math.dist(p, point)/100), [uv(p), uv(point)]))
                seams.append(dict(fromNode=node, targetSegment=selected[ri], distanceCm=distance,
                                  heightDifferenceCm=abs(p[2]-point[2]), points=[p, point]))

    # At-grade road intersections may occur in the middle of two splines.
    # Keep real cycles as well as reachability; a spanning tree causes huge detours.
    intersections, inspected = [], set()
    for members in cells.values():
        for j, (ri, pi) in enumerate(members):
            a, b = roads[ri]['points'][pi:pi+2]
            dx, dy = b[0]-a[0], b[1]-a[1]
            for rj, pj in members[j+1:]:
                if ri == rj:
                    continue
                pair = tuple(sorted(((ri, pi), (rj, pj))))
                if pair in inspected:
                    continue
                inspected.add(pair)
                c, d = roads[rj]['points'][pj:pj+2]
                ex, ey = d[0]-c[0], d[1]-c[1]
                determinant = dx*ey-dy*ex
                if abs(determinant) < .000001:
                    continue
                t = ((c[0]-a[0])*ey-(c[1]-a[1])*ex)/determinant
                q = ((c[0]-a[0])*dy-(c[1]-a[1])*dx)/determinant
                if not (0 <= t <= 1 and 0 <= q <= 1):
                    continue
                p1 = [a[k]+t*(b[k]-a[k]) for k in range(3)]
                p2 = [c[k]+q*(d[k]-c[k]) for k in range(3)]
                if abs(p1[2]-p2[2]) > 30 or water is None or not water.allows(p1,p2):
                    continue
                u, v = cut(ri, pi, t, p1), cut(rj, pj, q, p2)
                connection = tuple(sorted((u, v)))
                if u == v or connection in seam_pairs:
                    continue
                seam_pairs.add(connection)
                edges.append((u, v, max(.001, math.dist(p1,p2)/100), [uv(p1), uv(p2)]))
                parent[root(u)] = root(v)
                intersections.append(dict(sourceSegments=[selected[ri],selected[rj]], points=[p1,p2]))

    bridge_report = []
    for bridge in bridges:
        # Static highway decks are absent from landscape splines. Keep their
        # existing surveyed curves, attached to classified source identities.
        curve = bridge['pointsUV']
        attached = []
        for anchor in (curve[0], curve[-1]):
            p = [617718-anchor[0]*1521618, 618618-anchor[1]*1523618, 0]
            ri = selected.index(bridge['sourceSegments'][len(attached)])
            road = roads[ri]
            choices = [(project(p, a, b)[0], pi, project(p, a, b))
                       for pi, (a, b) in enumerate(zip(road['points'], road['points'][1:]))]
            distance, pi, (_, t, point) = min(choices)
            if distance > bridge['maxApproachMeters']*100:
                raise ValueError('Bridge approach no longer matches capture: '+bridge['name'])
            attached.append((cut(ri, pi, t, point), point))
        u, a = attached[0]; v, b = attached[1]
        points = [uv(a)]+[tuple(p) for p in curve]+[uv(b)]
        length = sum(math.hypot((b[0]-a[0])*15216.18, (b[1]-a[1])*15236.18) for a, b in zip(points, points[1:]))
        bridge_edges.add(len(edges))
        edges.append((u, v, length, points)); parent[root(u)] = root(v)
        bridge_report.append(bridge['name'])

    map_report = []
    for survey in map_links:
        # Owner-map surveys fill gaps that proximity alone cannot justify. Both
        # ends must still attach to their exact classified source road identities.
        curve = survey['pointsUV']
        if not (2 <= len(curve) <= 2048) or len(survey['sourceSegments']) != 2 or water is None:
            raise ValueError('Invalid map survey geometry or missing water coverage')
        if not (0 <= survey['maxApproachMeters'] <= 3 and 0 < survey['maxLengthMeters'] <= 500):
            raise ValueError('Map survey bounds exceed supported limits')
        for p in curve:
            if len(p) != 2 or not all(math.isfinite(v) and 0 <= v < 1 for v in p):
                raise ValueError('Invalid map survey coordinate')
        attached = []
        for endpoint, sid in zip((curve[0], curve[-1]), survey['sourceSegments']):
            p = [617718-endpoint[0]*1521618, 618618-endpoint[1]*1523618, 0]
            ri = selected.index(sid)
            road = roads[ri]
            distance, pi, t, point = min((distance, pi, t, point)
                for pi, (a,b) in enumerate(zip(road['points'],road['points'][1:]))
                for distance,t,point in [project(p,a,b)])
            if distance > survey['maxApproachMeters']*100:
                raise ValueError('Map survey source anchor moved: '+survey['name'])
            attached.append((cut(ri,pi,t,point),point))
        if len(attached) != 2:
            raise ValueError('Map survey needs two road anchors')
        (u,a),(v,b) = attached
        points = [uv(a)]+[tuple(p) for p in curve]+[uv(b)]
        length = sum(math.hypot((q[0]-p[0])*15216.18,(q[1]-p[1])*15236.18)
                     for p,q in zip(points,points[1:]))
        if not (0 < length <= min(500.0,survey['maxLengthMeters'])) or abs(a[2]-b[2]) > max(100,length*20):
            raise ValueError('Map survey length or height exceeds limits')
        world_curve = [[617718-p[0]*1521618,618618-p[1]*1523618,0] for p in points]
        if not all(water.allows(p,q) for p,q in zip(world_curve,world_curve[1:])):
            raise ValueError('Map survey crosses water: '+survey['name'])
        edges.append((u,v,length,points)); parent[root(u)] = root(v)
        map_report.append(dict(name=survey['name'],sourceSegments=survey['sourceSegments'],
                               imageSha256=survey['imageSha256'],lengthMeters=length))

    for road in roads:
        cuts = sorted(road['cuts'])
        for (start, u, a), (end, v, b) in zip(cuts, cuts[1:]):
            if end-start < .001:
                continue
            points = [a]+[p for at, p in zip(road['cumulative'], road['points']) if start < at < end]+[b]
            if road['bridge']:
                bridge_edges.add(len(edges))
            edges.append((u, v, (end-start)/100.0, [uv(p) for p in points]))
    if not edges or len(edges) > 60000 or len(nodes) > 100000:
        raise ValueError('Unsupported road counts')
    grid_res = 128
    grid = defaultdict(list)
    for index, (_, _, _, points) in enumerate(edges):
        x0, y0 = (max(0, min(127, int(min(p[d] for p in points)*128))) for d in (0, 1))
        x1, y1 = (max(0, min(127, int(max(p[d] for p in points)*128))) for d in (0, 1))
        for y in range(y0, y1+1):
            for x in range(x0, x1+1):
                grid[y*128+x].append(index)
    # Version 3 adds an explicit bridge flag. Only bridge decks may snap over water.
    data = bytearray(b'ROAD'+struct.pack('<IIII', 3, len(nodes), len(edges), grid_res))
    for point in nodes:
        data.extend(struct.pack('<ff', *point))
    for index, (u, v, length, points) in enumerate(edges):
        data.extend(struct.pack('<iifBH', u, v, length, int(index in bridge_edges), len(points)))
        for point in points:
            data.extend(struct.pack('<ff', *point))
    for cell in range(grid_res*grid_res):
        refs = grid[cell]
        data.extend(struct.pack('<H', len(refs)))
        for index in refs:
            data.extend(struct.pack('<H', index))
    sizes = defaultdict(int)
    for node in range(len(nodes)):
        sizes[root(node)] += 1
    return gzip.compress(data, mtime=0), {'nodes': len(nodes), 'segments': len(edges),
            'sourceRoadCount': len(selected), 'components': len(sizes),
            'largestComponentPercent': round(max(sizes.values())/len(nodes)*100, 2),
            'seamMaxDistanceCm': seam_cm, 'seams': seams, 'surveyedBridges': bridge_report,
            'atGradeIntersections': intersections,
            'mapSurveys': map_report,
            'sourceSegments': selected}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('graph', type=Path)
    parser.add_argument('allowlist', type=Path)
    parser.add_argument('--output', type=Path, default=Path(__file__).resolve().parents[1]/'resources/roads.bin')
    resources = Path(__file__).resolve().parents[1]/'resources'
    parser.add_argument('--additional-allowlist', type=Path, default=resources/'routing-road-meshes.json')
    parser.add_argument('--water-mask', type=Path, default=resources/'water-mask.bin')
    parser.add_argument('--bridges', type=Path, default=resources/'routing-bridges.json')
    parser.add_argument('--map-links', type=Path, default=resources/'routing-map-links.json')
    args = parser.parse_args()
    allowed = set(json.loads(args.allowlist.read_text(encoding='utf-8')))
    allowed.update(json.loads(args.additional_allowlist.read_text(encoding='utf-8')))
    surveys = json.loads(args.map_links.read_text(encoding='utf-8'))
    image_hashes = {item['sha256'] for item in surveys['images']}
    if surveys['schemaVersion'] != 1 or any(link['imageSha256'] not in image_hashes for link in surveys['links']):
        raise ValueError('Invalid map survey evidence')
    binary, report = compile_network(json.loads(args.graph.read_text(encoding='utf-8')),
                                     allowed, WaterMask(args.water_mask),
                                     json.loads(args.bridges.read_text(encoding='utf-8')),surveys['links'])
    args.output.write_bytes(binary)
    report.update(graphSha256=hashlib.sha256(args.graph.read_bytes()).hexdigest(),
                  allowlistSha256=hashlib.sha256(args.allowlist.read_bytes()).hexdigest(),
                  binarySha256=hashlib.sha256(binary).hexdigest())
    report.update(additionalAllowlistSha256=hashlib.sha256(args.additional_allowlist.read_bytes()).hexdigest(),
                  waterMaskSha256=hashlib.sha256(args.water_mask.read_bytes()).hexdigest(),
                  bridgesSha256=hashlib.sha256(args.bridges.read_bytes()).hexdigest())
    report['mapLinksSha256'] = hashlib.sha256(args.map_links.read_bytes()).hexdigest()
    args.output.with_suffix('.provenance.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
    print(f"Compiled {report['sourceRoadCount']} source roads, {len(report['seams'])} dry/3D seams, {len(report['surveyedBridges'])} surveyed bridges, {len(report['mapSurveys'])} map-surveyed gaps; largest component {report['largestComponentPercent']}%")
