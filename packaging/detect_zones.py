"""Local screenshot registration and coloured overlay extraction; runs only on import."""
import argparse
import json
import math
import os
from pathlib import Path
import struct
from urllib.parse import quote

os.environ.setdefault('OPENCV_IO_MAX_IMAGE_PIXELS', str(256 * 1024 * 1024))
import cv2
import numpy as np

cv2.setNumThreads(2)


def image_dimensions(path):
    """Inspect common screenshot headers before allocating decoded pixels."""
    with open(path, 'rb') as stream:
        header = stream.read(32)
        if header.startswith(b'\x89PNG\r\n\x1a\n') and len(header) >= 24:
            return struct.unpack('>II', header[16:24])
        if header.startswith(b'BM') and len(header) >= 26:
            dib = struct.unpack_from('<I', header, 14)[0]
            if dib >= 40:
                width, height = struct.unpack_from('<ii', header, 18)
                return width, abs(height)
        if header.startswith(b'\xff\xd8'):
            stream.seek(2)
            for _ in range(4096):
                if stream.read(1) != b'\xff':
                    break
                marker = stream.read(1)
                while marker == b'\xff':
                    marker = stream.read(1)
                if not marker or marker in (b'\xd9', b'\xda'):
                    break
                length_bytes = stream.read(2)
                if len(length_bytes) != 2:
                    break
                length = struct.unpack('>H', length_bytes)[0]
                if length < 2:
                    break
                if marker[0] in (0xc0, 0xc1, 0xc2, 0xc3, 0xc5, 0xc6, 0xc7, 0xc9, 0xca, 0xcb, 0xcd, 0xce, 0xcf):
                    frame = stream.read(5)
                    if len(frame) == 5:
                        height, width = struct.unpack('>HH', frame[1:])
                        return width, height
                    break
                stream.seek(length - 2, 1)
    raise ValueError('Use a valid PNG, JPEG or BMP image.')


def load_bounded_image(path, max_pixels):
    if Path(path).stat().st_size > 128 * 1024 * 1024:
        raise ValueError('Image file exceeds 128 MB.')
    width, height = image_dimensions(path)
    if width < 1 or height < 1 or width > 16384 or height > 16384 or width * height > max_pixels:
        raise ValueError('Image dimensions exceed the supported limit.')
    image = cv2.imdecode(np.fromfile(path, dtype=np.uint8), cv2.IMREAD_COLOR)
    if image is None or image.shape[0] * image.shape[1] > max_pixels:
        raise ValueError('Unable to decode the image within the supported limit.')
    return image


def register(screenshot, reference):
    ref_h, ref_w = reference.shape[:2]
    max_sift_dim = 4096
    scale = min(1.0, float(max_sift_dim) / max(ref_h, ref_w))
    if scale < 1.0:
        ref_sift = cv2.resize(reference, (int(round(ref_w * scale)), int(round(ref_h * scale))), interpolation=cv2.INTER_AREA)
    else:
        ref_sift = reference
    sift = cv2.SIFT_create(nfeatures=10000)
    kp, desc = sift.detectAndCompute(cv2.cvtColor(screenshot, cv2.COLOR_BGR2GRAY), None)
    rk, rd = sift.detectAndCompute(cv2.cvtColor(ref_sift, cv2.COLOR_BGR2GRAY), None)
    if desc is None or rd is None:
        raise ValueError('Not enough map detail to align this screenshot.')
    matches = cv2.BFMatcher().knnMatch(desc, rd, k=2)
    good = [pair[0] for pair in matches if len(pair) == 2 and pair[0].distance < .7 * pair[1].distance]
    if len(good) < 30:
        raise ValueError('Could not confidently match this screenshot to the base map.')
    src = np.float32([kp[m.queryIdx].pt for m in good])
    dst = np.float32([rk[m.trainIdx].pt for m in good])
    if scale < 1.0:
        dst = dst / scale
    matrix, mask = cv2.findHomography(src, dst, cv2.RANSAC, 3)
    if matrix is None or mask is None or mask.sum() < 25 or mask.mean() < .4:
        raise ValueError('Map alignment is unreliable. Use a clearer map screenshot.')
    error = np.linalg.norm(cv2.perspectiveTransform(src[:, None], matrix)[:, 0] - dst, axis=1)
    if np.median(error[mask.ravel() != 0]) > 2:
        raise ValueError('Map alignment error is too large.')
    spread = np.ptp(src[mask.ravel() != 0], axis=0)
    if min(spread / np.array(screenshot.shape[1::-1])) < .2:
        raise ValueError('Matches cover too little of the screenshot for reliable alignment.')
    return matrix, {'matches': len(good), 'inliers': int(mask.sum()),
                    'median_error_px': float(np.median(error[mask.ravel() != 0]))}


def extract(screenshot, reference, matrix):
    height, width = screenshot.shape[:2]
    aligned = cv2.warpPerspective(reference, np.linalg.inv(matrix), (width, height))
    delta = cv2.GaussianBlur(screenshot.astype(np.float32) - aligned.astype(np.float32), (0, 0), 1.2)
    b, g, r = cv2.split(delta)
    hsv = cv2.cvtColor(screenshot, cv2.COLOR_BGR2HSV)
    hue, sat, val = cv2.split(hsv)
    gray_scr = cv2.cvtColor(screenshot, cv2.COLOR_BGR2GRAY).astype(np.float32)
    gray_aln = cv2.cvtColor(aligned, cv2.COLOR_BGR2GRAY).astype(np.float32)
    delta_gray = gray_scr - gray_aln
    masks = {
        'Blue': (b > 18) & (b-r > 8) & (b-g > 4) & (sat < 110) & (val > 95),
        'Red': (r > 23) & (r-g > 16) & (r-b > 18),
        'Yellow': (r > 13) & (g > 10) & (r-b > 17) & (g-b > 12),
        'Green': (g > 14) & (g-r > 10) & (g-b > 8),
        'Purple': (r > 18) & (b > 18) & (r-g > 15) & (b-g > 10),
        'White': (delta_gray > 14) & (sat < 45) & (val > 135),
    }
    colours = {'Blue': (150, 200, 240), 'Red': (235, 70, 45), 'Yellow': (240, 215, 50),
               'Green': (105, 205, 50), 'Purple': (205, 90, 170), 'White': (255, 255, 255)}

    p1 = cv2.perspectiveTransform(np.array([[[0.0, 0.0], [100.0, 0.0]]], dtype=np.float32), matrix)[0]
    px_to_ref = max(1e-3, np.linalg.norm(p1[1] - p1[0]) / 100.0)
    base_r = max(5, int(round(176.75 / px_to_ref)))
    circle_scales = sorted(set([max(5, int(round(base_r * s))) for s in (0.8, 0.9, 1.0, 1.1, 1.2)]))

    found = []
    for colour, pixels in masks.items():
        mask_float = pixels.astype(np.float32)
        if colour == 'White':
            best_res = np.zeros((height, width), dtype=np.float32)
            best_rad = np.zeros((height, width), dtype=np.float32)
            for rad in circle_scales:
                y_g, x_g = np.ogrid[-rad:rad+1, -rad:rad+1]
                kernel = (x_g**2 + y_g**2 <= rad**2).astype(np.float32)
                res = cv2.matchTemplate(mask_float, kernel, cv2.TM_CCORR_NORMED)
                res_pad = np.pad(res, rad, mode='constant', constant_values=0)
                better = res_pad > best_res
                best_res[better] = res_pad[better]
                best_rad[better] = rad

            locs = np.where(best_res >= 0.70)
            candidates = list(zip(locs[1], locs[0], best_res[locs], best_rad[locs]))
            candidates.sort(key=lambda c: -c[2])

            chosen_circles = []
            for cx, cy, score, rad in candidates:
                if all((cx - ox)**2 + (cy - oy)**2 > (1.45 * min(rad, orad))**2 for ox, oy, _, orad in chosen_circles):
                    chosen_circles.append((cx, cy, score, rad))

            for cx, cy, score, rad in chosen_circles:
                circle_pts = []
                for s in range(32):
                    ang = 2 * np.pi * s / 32
                    circle_pts.append([cx + rad * np.cos(ang), cy + rad * np.sin(ang)])
                approx = np.array(circle_pts, dtype=np.float32)
                found.append({
                    'colour': colour,
                    'rgb': colours[colour],
                    'points': approx,
                    'area': float(np.pi * rad * rad),
                    'center': (float(cx), float(cy))
                })
            continue

        binary = np.uint8(pixels) * 255
        close_size = 21 if colour == 'Green' else 5
        binary = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, np.ones((close_size, close_size), np.uint8))
        binary = cv2.morphologyEx(binary, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
        contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for contour in contours:
            area = cv2.contourArea(contour)
            if area < (750 if colour in ('Red', 'Yellow') else 650 if colour == 'Green' else 550 if colour == 'Blue' else 350) or area > width * height * .03:
                continue
            x, y, w, h = cv2.boundingRect(contour)
            cx, cy = x + w / 2.0, y + h / 2.0
            if any(np.hypot(cx - z['center'][0], cy - z['center'][1]) < 1.2 * np.sqrt(z['area'] / np.pi) for z in found if z['colour'] == colour):
                continue
            (_, _), radius = cv2.minEnclosingCircle(contour)
            fill = area / (np.pi * radius * radius)
            rectangular = area / (w * h)
            if fill < .55 and rectangular < .70:
                continue
            if min(w, h) < 10 or max(w, h) / min(w, h) > 5:
                continue
            rot_rect = cv2.minAreaRect(contour)
            w_rot, h_rot = rot_rect[1]
            aspect_rot = max(w_rot, h_rot) / (min(w_rot, h_rot) + 1e-6)
            dists = np.linalg.norm(contour[:, 0] - np.array([cx, cy]), axis=1)
            r_var = np.std(dists) / (radius + 1e-6)

            if r_var > 0.12 or aspect_rot > 1.25:
                # Rectangle
                box = cv2.boxPoints(rot_rect).astype(np.float32)
                approx = box
            else:
                # Circle (smooth 32-point polygon)
                circle_pts = []
                r_fit = float(np.percentile(dists, 90))
                for s in range(32):
                    ang = 2 * np.pi * s / 32
                    circle_pts.append([cx + r_fit * np.cos(ang), cy + r_fit * np.sin(ang)])
                approx = np.array(circle_pts, dtype=np.float32)

            found.append({'colour': colour, 'rgb': colours[colour], 'points': approx,
                          'area': area, 'center': (cx, cy)})
    # Buildings recoloured inside a larger zone can produce a second colour component.
    retained = []
    for zone in sorted(found, key=lambda item: -item['area']):
        if any(cv2.pointPolygonTest(other['points'], zone['center'], False) >= 0
               and (zone['area'] < other['area']*.65 or
                    np.linalg.norm(np.array(zone['center'])-other['center']) < .25*np.sqrt(other['area']/np.pi))
               for other in retained):
            continue
        retained.append(zone)
    found = retained
    found.sort(key=lambda item: (round(item['center'][1]/80), item['center'][0]))
    return found


def assign_scum_names(zones, matrix, ref_shape, scummap_path):
    if not scummap_path or not Path(scummap_path).exists():
        return
    try:
        categories = {}
        markers = []
        with open(scummap_path, 'rb') as f:
            magic = f.read(4)
            if magic != b'SCMP':
                return
            v, cat_cnt = struct.unpack('<HH', f.read(4))
            for _ in range(cat_cnt):
                cid, slen = struct.unpack('<IB', f.read(5))
                sec = f.read(slen).decode('utf-8', errors='ignore')
                nlen, = struct.unpack('<B', f.read(1))
                name = f.read(nlen).decode('utf-8', errors='ignore')
                f.read(9)
                categories[cid] = {'sec': sec, 'name': name}
            mcnt, = struct.unpack('<I', f.read(4))
            for _ in range(mcnt):
                mid, cid, x, y, tlen = struct.unpack('<IIffH', f.read(18))
                title = f.read(tlen).decode('utf-8', errors='ignore') if tlen > 0 else ''
                cat_info = categories.get(cid, {})
                markers.append({
                    'cid': cid,
                    'sec': cat_info.get('sec', ''),
                    'cat': cat_info.get('name', ''),
                    'title': title,
                    'x': x,
                    'y': y
                })
    except Exception:
        return

    clean_names = {
        'Villages': lambda t, c: t if t else 'Village',
        'Points of interest': lambda t, c: t if (t and 'Halloween' not in t and 'Easter' not in t) else 'Point of Interest',
        'Outposts': lambda t, c: t if t else 'Outpost',
        'Bunkers': lambda t, c: t if t else 'Bunker',
        'Abandoned bunkers': lambda t, c: 'Abandoned Bunker',
        'WW2 bunkers': lambda t, c: 'WW2 Bunker',
        'Research Facilities': lambda t, c: 'Research Facility',
        'Killboxes': lambda t, c: 'Killbox',
        'Police stations': lambda t, c: 'Police Station',
        'Gas stations': lambda t, c: 'Gas Station',
        'Churches': lambda t, c: 'Church',
        'Workshops': lambda t, c: 'Workshop',
        'Warehouses': lambda t, c: 'Warehouse',
        'Military Hangars': lambda t, c: 'Military Hangar',
        'Military Warehouses': lambda t, c: 'Military Warehouse',
        'Schools': lambda t, c: 'School',
        'Pharmacies': lambda t, c: 'Pharmacy',
        'Lighthouses': lambda t, c: 'Lighthouse',
        'Bars': lambda t, c: 'Bar',
        'Pubs': lambda t, c: 'Pub',
        'Clubs': lambda t, c: 'Club',
        'Food Shop': lambda t, c: 'Food Shop',
        'Gun Shops': lambda t, c: 'Gun Shop',
        'Log Cabin': lambda t, c: 'Log Cabin',
        'Animal feeders': lambda t, c: 'Little Farm',
        'Corn fields': lambda t, c: 'Farm',
        'Sunflower fields': lambda t, c: 'Farm',
        'Grapevines': lambda t, c: 'Vineyard',
        'Apple trees': lambda t, c: 'Orchard',
        'Big Shipwrecks': lambda t, c: 'Shipwreck',
        'Small Shipyard': lambda t, c: 'Small Shipyard',
        'Caves': lambda t, c: 'Cave',
        'Fish Factory': lambda t, c: 'Fish Factory',
        'Car garages': lambda t, c: 'Garage',
    }

    canonical_faction_zones = [
        {'sec': 'A0', 'name': 'A0 Offices', 'norm': (1867.2/2048, 1296.2/2048)},
        {'sec': 'A0', 'name': 'A0 Camping', 'norm': (2009.2/2048, 1441.2/2048)},
        {'sec': 'A1', 'name': 'A1 Stables', 'norm': (1371.1/2048, 1355.2/2048)},
        {'sec': 'A1', 'name': 'A1 School', 'norm': (1484.1/2048, 1403.2/2048)},
        {'sec': 'A2', 'name': 'A2 Stables', 'norm': (957.1/2048, 1368.2/2048)},
        {'sec': 'A2', 'name': 'A2 Store with ATM', 'norm': (1123.1/2048, 1375.2/2048)},
        {'sec': 'A3', 'name': 'A3 Farm', 'norm': (464.1/2048, 1341.2/2048)},
        {'sec': 'A3', 'name': 'A3 WWII bunker', 'norm': (530.1/2048, 1593.2/2048)},
        {'sec': 'A3', 'name': 'A3 Island WWII bunker', 'norm': (762.1/2048, 1573.2/2048)},
        {'sec': 'A4', 'name': 'A4 House ruins', 'norm': (194.1/2048, 1292.3/2048)},
        {'sec': 'A4', 'name': 'A4 Lighthouse', 'norm': (87.1/2048, 1381.3/2048)},
        {'sec': 'A4', 'name': 'A4 Street between 2 houses', 'norm': (265.1/2048, 1492.2/2048)},
        {'sec': 'B1', 'name': 'B1 Little Farm', 'norm': (1462.1/2048, 1140.2/2048)},
        {'sec': 'B1', 'name': 'B1 WWII Bunker', 'norm': (1283.1/2048, 1195.2/2048)},
        {'sec': 'B1', 'name': 'B1 Barn', 'norm': (1422.1/2048, 1200.2/2048)},
        {'sec': 'B2', 'name': 'B2 Little Farm', 'norm': (1023.1/2048, 969.3/2048)},
        {'sec': 'B2', 'name': 'B2 Farm', 'norm': (1039.1/2048, 1155.2/2048)},
        {'sec': 'B2', 'name': 'B2 WWII Bunker', 'norm': (1111.1/2048, 1140.2/2048)},
        {'sec': 'B3', 'name': 'B3 WWII Bunker', 'norm': (554.1/2048, 1118.3/2048)},
        {'sec': 'B3', 'name': 'B3 Little farm', 'norm': (595.1/2048, 1098.3/2048)},
        {'sec': 'C1', 'name': 'C1 ww2 bunker', 'norm': (1427.1/2048, 427.3/2048)},
        {'sec': 'C1', 'name': 'C1 Little farm', 'norm': (1582.2/2048, 628.3/2048)},
        {'sec': 'C3', 'name': 'C3 Little cabin', 'norm': (744.1/2048, 577.3/2048)},
        {'sec': 'C3', 'name': 'C3 Workshop', 'norm': (636.1/2048, 614.3/2048)},
        {'sec': 'C3', 'name': 'C3 Garage', 'norm': (734.1/2048, 730.3/2048)},
        {'sec': 'C4', 'name': 'C4 WWII Bunker', 'norm': (54.1/2048, 676.3/2048)},
        {'sec': 'C4', 'name': 'C4 Wine farm', 'norm': (357.1/2048, 610.3/2048)},
        {'sec': 'D1', 'name': 'D1 Farm', 'norm': (1481.2/2048, 20.3/2048)},
        {'sec': 'D1', 'name': 'D1 Food store', 'norm': (1296.1/2048, 247.3/2048)},
        {'sec': 'D1', 'name': 'D1 Graveyard', 'norm': (1619.2/2048, 339.3/2048)},
        {'sec': 'D2', 'name': 'D2 Little bridge', 'norm': (865.1/2048, 122.3/2048)},
        {'sec': 'D2', 'name': 'D2 Lumbermill cabin', 'norm': (982.1/2048, 164.3/2048)},
        {'sec': 'D2', 'name': 'D2 Little town - House', 'norm': (905.1/2048, 236.3/2048)},
        {'sec': 'D3', 'name': 'D3 City Warehouse', 'norm': (780.1/2048, 34.3/2048)},
        {'sec': 'D3', 'name': 'D3 - General Store', 'norm': (473.1/2048, 145.3/2048)},
        {'sec': 'D3', 'name': 'D3 Gas Station', 'norm': (674.1/2048, 197.3/2048)},
        {'sec': 'D3', 'name': 'D3 Little town - House', 'norm': (727.1/2048, 187.3/2048)},
        {'sec': 'D4', 'name': 'D4 Clock house', 'norm': (341.1/2048, 43.3/2048)},
        {'sec': 'D4', 'name': 'D4 Stable ruins', 'norm': (222.1/2048, 179.3/2048)},
        {'sec': 'D4', 'name': 'D4 City - Police Station', 'norm': (87.1/2048, 236.3/2048)},
        {'sec': 'Z0', 'name': 'Z0 WWII bunker', 'norm': (1902.2/2048, 1705.2/2048)},
        {'sec': 'Z0', 'name': 'Z0 Lighthouse', 'norm': (1989.2/2048, 1809.2/2048)},
        {'sec': 'Z0', 'name': 'Z0 WWII Bunker South', 'norm': (1666.2/2048, 1992.2/2048)},
        {'sec': 'Z2', 'name': 'Z2 light house', 'norm': (920.1/2048, 1999.2/2048)},
        {'sec': 'Z3', 'name': 'Z3 WWII Bunker', 'norm': (425.1/2048, 1818.2/2048)},
        {'sec': 'Z3', 'name': 'Z3 Boat yard', 'norm': (630.1/2048, 1854.2/2048)},
        {'sec': 'Z3', 'name': 'Z3 Island Lighthouse', 'norm': (419.1/2048, 2008.2/2048)},
        {'sec': 'Z4', 'name': 'Z4 Lighthouse', 'norm': (25.1/2048, 1743.2/2048)},
        {'sec': 'Z4', 'name': 'Z4 WWII Bunker', 'norm': (141.1/2048, 1749.2/2048)},
        {'sec': 'Z4', 'name': 'Z4 Boatyard', 'norm': (297.1/2048, 1959.2/2048)},
        {'sec': 'Z4', 'name': 'Z4 Graveyard', 'norm': (216.1/2048, 1977.2/2048)},
    ]

    def get_priority(m):
        sec = m['sec']
        cat = m['cat']
        if sec == 'Points of interest': return 1
        if cat == 'Villages': return 1
        if sec == 'Outposts' or cat == 'Outposts': return 1
        if sec == 'Bunkers': return 2
        if cat in ('Police stations', 'Gas stations', 'Lighthouses', 'Churches', 'Workshops', 'Warehouses', 'Military Hangars'): return 3
        if cat in ('Animal feeders', 'Corn fields', 'Sunflower fields'): return 3
        if cat in ('Schools', 'Pharmacies', 'Bars', 'Pubs', 'Gun Shops', 'Food Shop', 'Log Cabin', 'Fish Factory'): return 4
        if cat in ('Caves', 'Big Shipwrecks', 'Small Shipyard', 'Grapevines', 'Apple trees'): return 5
        if cat in ('Car garages',): return 6
        return 20

    def sector_of(mx, my):
        wx = 617718.0 - mx * 1521618.0
        wy = 618618.0 - my * 1523618.0
        col = max(0, min(4, int(math.floor((617505.0 - wx) / 304132.0))))
        row = max(0, min(4, int(math.floor((617953.0 - wy) / 304356.0))))
        return 'DCBAZ'[row] + str(4 - col)

    used_names = {}
    used_canonical = set()
    for zone in zones:
        pts = zone['points']
        if matrix is not None:
            pts_ref = cv2.perspectiveTransform(pts[:, None], matrix)[:, 0]
        else:
            pts_ref = pts
        norm_pts = np.clip(pts_ref / np.array(ref_shape[1::-1]), 0, 1)
        cx, cy = float(np.mean(norm_pts[:, 0])), float(np.mean(norm_pts[:, 1]))
        sec = sector_of(cx, cy)

        # First check canonical faction zones for White circular zones
        assigned_name = None
        if zone.get('colour') == 'White':
            best_cdist = float('inf')
            best_canon = None
            for cz in canonical_faction_zones:
                if cz['sec'] == sec and cz['name'] not in used_canonical:
                    cd = math.hypot(cz['norm'][0] - cx, cz['norm'][1] - cy)
                    if cd < best_cdist:
                        best_cdist = cd
                        best_canon = cz
            if best_canon and best_cdist < 0.035:
                assigned_name = best_canon['name']
                used_canonical.add(assigned_name)

        if not assigned_name:
            candidates = []
            for m in markers:
                dx = (m['x'] - cx) * 15216.18
                dy = (m['y'] - cy) * 15236.18
                dist = math.hypot(dx, dy)
                if dist > 500:
                    continue
                prio = get_priority(m)
                if prio <= 10:
                    candidates.append((dist, prio, m))

            candidates.sort(key=lambda c: c[0] * (1.0 + 0.25 * (c[1] - 1)))
            if candidates:
                best_m = candidates[0][2]
                cat_name = best_m['cat']
                clean_fn = clean_names.get(cat_name, lambda t, c: t if t else c)
                area_name = clean_fn(best_m['title'], cat_name)
            else:
                area_name = 'Outpost'
            assigned_name = f"{sec} {area_name}"

        if assigned_name in used_names:
            used_names[assigned_name] += 1
            display_name = f"{assigned_name} {used_names[assigned_name]}"
        else:
            used_names[assigned_name] = 1
            display_name = assigned_name
        zone['name'] = display_name


def write_zones(path, zones, shape, matrix=None):
    lines = []
    for number, zone in enumerate(zones, 1):
        points = zone['points']
        if matrix is not None:
            points = cv2.perspectiveTransform(points[:, None], matrix)[:, 0]
        points = np.clip(points / np.array(shape[1::-1]), 0, 1)
        r, g, b = zone['rgb']
        argb = (255 << 24 | r << 16 | g << 8 | b) - 2**32
        name = zone.get('name') or (zone['colour'] + ' zone ' + str(number))
        lines.append(f"{quote(name, safe='')}\t{argb}\t" +
                     ';'.join(f'{x:.8f},{y:.8f}' for x, y in points))
    Path(path).write_text('\n'.join(lines), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--reference', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--source-output', required=True)
    parser.add_argument('--preview')
    parser.add_argument('--scummap')
    args = parser.parse_args()
    screenshot = load_bounded_image(args.input, 40000000)
    reference = load_bounded_image(args.reference, 256 * 1024 * 1024)
    if screenshot is None or reference is None:
        raise ValueError('Unable to read the screenshot or base map.')
    if screenshot.shape[0] * screenshot.shape[1] > 40000000:
        raise ValueError('Use a screenshot smaller than 40 megapixels.')
    matrix, report = register(screenshot, reference)
    zones = extract(screenshot, reference, matrix)
    if not zones:
        raise ValueError('Map aligned, but no coloured zone overlays were detected.')

    scummap_path = args.scummap
    if not scummap_path or not Path(scummap_path).exists():
        candidates = [
            Path(args.reference).parent / 'scummap.bin',
            Path(__file__).resolve().parent.parent / 'resources' / 'scummap.bin',
            Path(__file__).resolve().parent / 'scummap.bin',
            Path.cwd() / 'resources' / 'scummap.bin',
            Path.cwd() / 'scummap.bin',
        ]
        for cand in candidates:
            if cand.exists():
                scummap_path = str(cand)
                break
    if scummap_path:
        assign_scum_names(zones, matrix, reference.shape, scummap_path)

    write_zones(args.output, zones, reference.shape, matrix)
    write_zones(args.source_output, zones, screenshot.shape)
    if args.preview:
        annotated = screenshot.copy()
        for number, zone in enumerate(zones, 1):
            cv2.polylines(annotated, [zone['points'].astype(np.int32)], True, (255, 0, 255), 2)
            x, y = map(int, zone['center'])
            label = zone.get('name') or str(number)
            cv2.putText(annotated, label, (x - 20, y - 8), cv2.FONT_HERSHEY_SIMPLEX, .40, (0, 0, 0), 3)
            cv2.putText(annotated, label, (x - 20, y - 8), cv2.FONT_HERSHEY_SIMPLEX, .40, (255, 255, 255), 1)
        cv2.imencode('.png', annotated)[1].tofile(args.preview)
    print(json.dumps(dict(report, zones=len(zones), colours={c: sum(z['colour']==c for z in zones) for c in sorted({z['colour'] for z in zones})})))


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        import sys
        print(str(exc), file=sys.stderr)
        sys.exit(1)
