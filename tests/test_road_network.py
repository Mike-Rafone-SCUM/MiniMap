import importlib.util
import copy
import gzip
import struct
import unittest
from pathlib import Path

spec = importlib.util.spec_from_file_location('roads', Path(__file__).resolve().parents[1]/'scripts/Build-RoadNetwork.py')
roads = importlib.util.module_from_spec(spec)
spec.loader.exec_module(roads)


class RoadCompilationTests(unittest.TestCase):
    def graph(self):
        positions = [[0, 0, 0], [10000, 0, 0], [10001, 0, 0], [20000, 0, 0]]
        nodes = {str(i): {'position': p} for i, p in enumerate(positions)}
        def segment(a, b, mesh='asphalt'):
            return dict(connectedGeometryValid=True, meshes=[mesh], lengthCm=10000,
                        center=[positions[a], positions[b]], endpoints=[str(a), str(b)])
        return dict(units='cm', coordinateSpace='world', nodes=nodes,
                    segments={'a': segment(0, 1), 'b': segment(2, 3),
                              'river': segment(1, 2, 'river'),
                              'mixed': segment(1, 2, 'asphalt')})

    def test_per_segment_surface_overrides_mesh_default(self):
        graph = self.graph()
        graph['segments'] = {'a':graph['segments']['a']}
        graph['segments']['a']['meshes'] = ['gravel']
        _,report = roads.compile_network(graph,{'gravel'},surface_overrides={'a':'paved'})
        self.assertEqual(report['surfaceEdges']['paved'],1)
        with self.assertRaises(ValueError):
            roads.compile_network(graph,{'gravel'},surface_overrides={'a':'rail'})
        with self.assertRaises(ValueError):
            roads.compile_network(graph,{'gravel'},surface_overrides={'missing':'paved'})

    def test_explicit_map_surface_overrides_asset_name(self):
        graph = self.graph()
        graph['segments'] = {'a':graph['segments']['a']}
        mesh = 'StaticMesh /Game/ConZ_Files/Models/Road/Asphalt_Road_02/Asphalt_Road_02.Asphalt_Road_02'
        graph['segments']['a']['meshes'] = [mesh]
        blob,report = roads.compile_network(graph,{mesh},dirt_meshes={mesh})
        self.assertEqual(report['surfaceEdges']['dirt'],1)
        self.assertEqual(report['surfaceEdges']['paved'],0)

    def test_surface_flags_preserve_dirt_on_split_roads_and_seams(self):
        class Dry:
            def allows(self,a,b): return True
        graph = self.graph()
        graph['segments'] = {k:graph['segments'][k] for k in ('a','b')}
        graph['segments']['b']['meshes'] = ['gravel']
        blob, report = roads.compile_network(graph, {'asphalt','gravel'}, Dry())
        data = gzip.decompress(blob)
        version,n,m,_ = struct.unpack_from('<4I',data,4)
        offset = 20+8*n
        flags = []
        for i in range(m):
            u,v,length,flag,count = struct.unpack_from('<iifBH',data,offset)
            flags.append(flag); offset += 15+8*count
        self.assertEqual(version,4)
        self.assertIn(0,flags)
        self.assertEqual(flags.count(2),2)  # dirt road and its conservative seam
        self.assertEqual(report['surfaceEdges']['dirt'],2)

    def test_filter_and_preserve_distinct_coincident_endpoints(self):
        graph = self.graph()
        graph['segments']['mixed']['meshes'].append('river')
        binary, report = roads.compile_network(graph, {'asphalt'})
        self.assertEqual(report['sourceSegments'], ['a', 'b'])
        self.assertEqual(report['nodes'], 4)  # 1 cm gap must not create a junction
        self.assertEqual(struct.unpack_from('<I', gzip.decompress(binary), 4)[0], 4)
        self.assertEqual(binary, roads.compile_network(graph, {'asphalt'})[0])

    def test_shared_identity_connects_and_invalid_geometry_is_rejected(self):
        graph = self.graph()
        graph['segments'] = {'a': graph['segments']['a'], 'b': graph['segments']['b']}
        graph['segments']['b']['endpoints'][0] = '1'
        graph['segments']['b']['center'][0] = graph['nodes']['1']['position']
        self.assertEqual(roads.compile_network(graph, {'asphalt'})[1]['nodes'], 3)
        bad = copy.deepcopy(graph)
        bad['segments']['b']['center'][0] = [999999, 0, 0]
        with self.assertRaises(ValueError):
            roads.compile_network(bad, {'asphalt'})

    def test_nonroad_and_unverified_segments_do_not_compile(self):
        graph = self.graph()
        for segment in graph['segments'].values():
            segment['connectedGeometryValid'] = False
        with self.assertRaises(ValueError):
            roads.compile_network(graph, {'asphalt'})

    def test_nearby_endpoints_and_middle_of_road_form_dry_height_checked_junctions(self):
        class Dry:
            def allows(self, a, b): return True
        graph = self.graph()
        graph['segments'] = {k: graph['segments'][k] for k in ('a', 'b')}
        report = roads.compile_network(graph, {'asphalt'}, Dry())[1]
        self.assertEqual(report['components'], 1)
        self.assertEqual(len(report['seams']), 1)
        # A T-junction ending at the interior of the first road, not its node.
        graph['nodes']['2']['position'] = [5000, 100, 0]
        graph['segments']['b']['center'][0] = [5000, 100, 0]
        report = roads.compile_network(graph, {'asphalt'}, Dry())[1]
        self.assertEqual(report['components'], 1)
        self.assertGreater(report['nodes'], 4)  # target road was split at the junction

    def test_water_long_gaps_and_stacked_roads_remain_disconnected(self):
        class Dry:
            def allows(self, a, b): return True
        class Wet:
            def allows(self, a, b): return False
        graph = self.graph()
        graph['segments'] = {k: graph['segments'][k] for k in ('a', 'b')}
        # Two metres across a river must not be healed as a road seam.
        graph['nodes']['2']['position'] = [10200, 0, 0]
        graph['segments']['b']['center'][0] = [10200, 0, 0]
        self.assertEqual(roads.compile_network(graph, {'asphalt'}, Wet())[1]['components'], 2)
        # Six metres exceeds the bounded five-metre seam policy, even on land.
        graph['nodes']['2']['position'] = [10600, 0, 0]
        graph['segments']['b']['center'][0] = [10600, 0, 0]
        self.assertEqual(roads.compile_network(graph, {'asphalt'}, Dry())[1]['components'], 2)
        # Same XY at a different elevation is an overpass, not a junction.
        graph['nodes']['2']['position'] = [10000, 0, 500]
        graph['segments']['b']['center'][0] = [10000, 0, 500]
        self.assertEqual(roads.compile_network(graph, {'asphalt'}, Dry())[1]['components'], 2)

    def test_cycles_are_retained_even_when_roads_already_share_a_component(self):
        class Dry:
            def allows(self, a, b): return True
        graph = self.graph()
        graph['segments'] = {k: graph['segments'][k] for k in ('a', 'b')}
        graph['segments']['link'] = dict(connectedGeometryValid=True, meshes=['asphalt'],
                lengthCm=20000, center=[[0,0,0],[20000,0,0]], endpoints=['0','3'])
        report = roads.compile_network(graph, {'asphalt'}, Dry())[1]
        self.assertGreater(len(report['seams']), 0)
        self.assertEqual(report['components'], 1)

    def test_mid_spline_crossroads_connect_but_overpasses_do_not(self):
        class Dry:
            def allows(self, a, b): return True
        positions = [[-5000,0,0],[5000,0,0],[0,-5000,0],[0,5000,0]]
        graph = dict(units='cm',coordinateSpace='world',
                     nodes={str(i):dict(position=p) for i,p in enumerate(positions)},segments={})
        for name,a,b in [('horizontal',0,1),('vertical',2,3)]:
            graph['segments'][name] = dict(connectedGeometryValid=True, meshes=['asphalt'],
                    lengthCm=10000, center=[positions[a],positions[b]],endpoints=[str(a),str(b)])
        report = roads.compile_network(graph, {'asphalt'}, Dry())[1]
        self.assertEqual(report['components'], 1)
        self.assertEqual(len(report['atGradeIntersections']), 1)
        positions[2][2] = positions[3][2] = 500
        self.assertEqual(roads.compile_network(graph, {'asphalt'}, Dry())[1]['components'], 2)

    def test_map_survey_connects_source_roads_and_rejects_water_or_moved_anchors(self):
        class Dry:
            def allows(self, a, b): return True
        class Wet:
            def allows(self, a, b): return False
        graph = self.graph()
        graph['segments'] = {k: graph['segments'][k] for k in ('a', 'b')}
        graph['nodes']['2']['position'] = [11000,0,0]
        graph['segments']['b']['center'][0] = [11000,0,0]
        survey = dict(name='Reviewed map road',sourceSegments=['a','b'],
                      pointsUV=[roads.uv([10000,0,0]),roads.uv([10500,100,0]),roads.uv([11000,0,0])],
                      maxApproachMeters=3,maxLengthMeters=500,imageSha256='0'*64)
        report = roads.compile_network(graph, {'asphalt'}, Dry(), map_links=[survey])[1]
        self.assertEqual(report['components'], 1)
        self.assertEqual(len(report['mapSurveys']), 1)
        graph['segments']['b']['meshes'] = ['gravel']
        survey['surface'] = 'paved'
        _,classified = roads.compile_network(graph,{'asphalt','gravel'},Dry(),map_links=[survey])
        self.assertEqual(classified['surfaceEdges']['paved'],2)  # road a and surveyed curve
        graph['segments']['b']['meshes'] = ['asphalt']
        with self.assertRaises(ValueError):
            roads.compile_network(graph, {'asphalt'}, Wet(), map_links=[survey])
        invalid = copy.deepcopy(survey)
        invalid['pointsUV'][0] = roads.uv([10000,1000,0])
        with self.assertRaises(ValueError):
            roads.compile_network(graph, {'asphalt'}, Dry(), map_links=[invalid])
        invalid = copy.deepcopy(survey)
        invalid['pointsUV'][1] = [float('nan'),.5]
        with self.assertRaises(ValueError):
            roads.compile_network(graph, {'asphalt'}, Dry(), map_links=[invalid])


if __name__ == '__main__':
    unittest.main()
