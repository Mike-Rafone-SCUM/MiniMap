"""Local screenshot registration and coloured overlay extraction; runs only on import."""
import argparse
import json
from pathlib import Path
from urllib.parse import quote

import cv2
import numpy as np

cv2.setNumThreads(2)


def register(screenshot, reference):
    sift = cv2.SIFT_create(nfeatures=10000)
    kp, desc = sift.detectAndCompute(cv2.cvtColor(screenshot, cv2.COLOR_BGR2GRAY), None)
    rk, rd = sift.detectAndCompute(cv2.cvtColor(reference, cv2.COLOR_BGR2GRAY), None)
    if desc is None or rd is None:
        raise ValueError('Not enough map detail to align this screenshot.')
    matches = cv2.BFMatcher().knnMatch(desc, rd, k=2)
    good = [pair[0] for pair in matches if len(pair) == 2 and pair[0].distance < .7 * pair[1].distance]
    if len(good) < 30:
        raise ValueError('Could not confidently match this screenshot to the base map.')
    src = np.float32([kp[m.queryIdx].pt for m in good])
    dst = np.float32([rk[m.trainIdx].pt for m in good])
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
    masks = {
        'Blue': (b > 18) & (b-r > 8) & (b-g > 4) & (sat < 110) & (val > 95),
        'Red': (r > 23) & (r-g > 16) & (r-b > 18),
        'Yellow': (r > 13) & (g > 10) & (r-b > 17) & (g-b > 12),
        'Green': (g > 14) & (g-r > 10) & (g-b > 8),
        'Purple': (r > 18) & (b > 18) & (r-g > 15) & (b-g > 10),
    }
    colours = {'Blue': (150, 200, 240), 'Red': (235, 70, 45), 'Yellow': (240, 215, 50),
               'Green': (105, 205, 50), 'Purple': (205, 90, 170)}
    found = []
    for colour, pixels in masks.items():
        binary = np.uint8(pixels) * 255
        close_size = 21 if colour == 'Green' else 5
        binary = cv2.morphologyEx(binary, cv2.MORPH_CLOSE, np.ones((close_size, close_size), np.uint8))
        binary = cv2.morphologyEx(binary, cv2.MORPH_OPEN, np.ones((5, 5), np.uint8))
        contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
        for contour in contours:
            area = cv2.contourArea(contour)
            if area < (750 if colour in ('Red', 'Yellow') else 550 if colour == 'Blue' else 180) or area > width * height * .03:
                continue
            x, y, w, h = cv2.boundingRect(contour)
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
            dists = np.linalg.norm(contour[:, 0] - np.array([x + w/2.0, y + h/2.0]), axis=1)
            r_var = np.std(dists) / (radius + 1e-6)

            if r_var > 0.12 or aspect_rot > 1.25:
                # Rectangle
                box = cv2.boxPoints(rot_rect).astype(np.float32)
                approx = box
            else:
                # Circle (smooth 32-point polygon)
                circle_pts = []
                r_fit = float(np.percentile(dists, 90))
                cx, cy = x + w/2.0, y + h/2.0
                for s in range(32):
                    ang = 2 * np.pi * s / 32
                    circle_pts.append([cx + r_fit * np.cos(ang), cy + r_fit * np.sin(ang)])
                approx = np.array(circle_pts, dtype=np.float32)

            found.append({'colour': colour, 'rgb': colours[colour], 'points': approx,
                          'area': area, 'center': (x+w/2, y+h/2)})
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


def write_zones(path, zones, shape, matrix=None):
    lines = []
    for number, zone in enumerate(zones, 1):
        points = zone['points']
        if matrix is not None:
            points = cv2.perspectiveTransform(points[:, None], matrix)[:, 0]
        points = np.clip(points / np.array(shape[1::-1]), 0, 1)
        r, g, b = zone['rgb']
        argb = (255 << 24 | r << 16 | g << 8 | b) - 2**32
        lines.append(f"{quote(zone['colour'] + ' zone ' + str(number), safe='')}\t{argb}\t" +
                     ';'.join(f'{x:.8f},{y:.8f}' for x, y in points))
    Path(path).write_text('\n'.join(lines), encoding='utf-8')


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--input', required=True)
    parser.add_argument('--reference', required=True)
    parser.add_argument('--output', required=True)
    parser.add_argument('--source-output', required=True)
    parser.add_argument('--preview')
    args = parser.parse_args()
    screenshot = cv2.imdecode(np.fromfile(args.input, dtype=np.uint8), cv2.IMREAD_COLOR)
    reference = cv2.imdecode(np.fromfile(args.reference, dtype=np.uint8), cv2.IMREAD_COLOR)
    if screenshot is None or reference is None:
        raise ValueError('Unable to read the screenshot or base map.')
    if screenshot.shape[0] * screenshot.shape[1] > 40000000:
        raise ValueError('Use a screenshot smaller than 40 megapixels.')
    matrix, report = register(screenshot, reference)
    zones = extract(screenshot, reference, matrix)
    if not zones:
        raise ValueError('Map aligned, but no coloured zone overlays were detected.')
    write_zones(args.output, zones, reference.shape, matrix)
    write_zones(args.source_output, zones, screenshot.shape)
    if args.preview:
        annotated = screenshot.copy()
        for number, zone in enumerate(zones, 1):
            cv2.polylines(annotated, [zone['points'].astype(np.int32)], True, (255, 0, 255), 2)
            x, y = map(int, zone['center'])
            cv2.putText(annotated, str(number), (x, y), cv2.FONT_HERSHEY_SIMPLEX, .55, (0, 0, 0), 4)
            cv2.putText(annotated, str(number), (x, y), cv2.FONT_HERSHEY_SIMPLEX, .55, (255, 255, 255), 1)
        cv2.imencode('.png', annotated)[1].tofile(args.preview)
    print(json.dumps(dict(report, zones=len(zones), colours={c: sum(z['colour']==c for z in zones) for c in sorted({z['colour'] for z in zones})})))


if __name__ == '__main__':
    try:
        main()
    except Exception as exc:
        import sys
        print(str(exc), file=sys.stderr)
        sys.exit(1)
