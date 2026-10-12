"""Geometry regressions for the screenshot importer (requires packaging dependencies)."""
import importlib.util
from pathlib import Path
import unittest
import struct
import tempfile

import cv2
import numpy as np

spec = importlib.util.spec_from_file_location(
    'detect_zones', Path(__file__).resolve().parents[1] / 'packaging' / 'detect_zones.py')
detector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(detector)


class ZoneGeometryTests(unittest.TestCase):
    def test_export_preserves_regular_shapes_after_registration(self):
        angle=np.arange(64)*2*np.pi/64
        circle=np.array([10,10])+20*np.column_stack((np.cos(angle),np.sin(angle)))
        rectangle=self.polygon(30,30,70,50)
        transform=np.array([[1,.2,0],[.1,1,0],[.001,0,1]],dtype=float)
        with tempfile.TemporaryDirectory() as folder:
            file=Path(folder)/'zones.tsv'
            detector.write_zones(file,[dict(points=circle.astype(np.float32),rgb=(255,255,255),colour='White'),dict(points=rectangle,rgb=(255,0,0),colour='Red')],(100,100),transform)
            outlines=[np.array([[float(v) for v in p.split(',')] for p in line.split('\t')[2].split(';')])*100 for line in file.read_text().splitlines()]
            radii=np.linalg.norm(outlines[0]-outlines[0].mean(axis=0),axis=1)
            self.assertLess(np.ptp(radii),.00001)
            edges=np.roll(outlines[1],-1,axis=0)-outlines[1]
            self.assertLess(abs(np.dot(edges[0],edges[1])),.001)
            for points in outlines:
                self.assertGreaterEqual(points.min(),0)
                self.assertLessEqual(points.max(),100)

    def test_white_circles_follow_reference_resolution_and_image_edges(self):
        for size in (1024, 2048):
            reference = np.full((size, size, 3), 60, np.uint8)
            screenshot = reference.copy()
            radius = round(176.75 * size / 14481)
            centres = [(size//4, size//3), (size//2, size//2), (size*3//4, 2)]
            for centre in centres:
                cv2.circle(screenshot, centre, radius, (220, 220, 220), -1)
            zones = detector.extract(screenshot, reference, np.eye(3))
            white = [z for z in zones if z['colour']=='White']
            self.assertEqual(len(white), 3)
            for centre in centres:
                self.assertLess(min(np.linalg.norm(np.array(z['center'])-centre) for z in white), 4)

    def polygon(self, x1, y1, x2, y2):
        return np.array([[x1, y1], [x2, y1], [x2, y2], [x1, y2]], dtype=np.float32)

    def test_nearby_towns_have_independent_outline_owners(self):
        a, b = self.polygon(.1, .1, .12, .12), self.polygon(.125, .1, .15, .12)
        markers = [dict(cat='Villages', sec='', x=.119, y=.11), dict(cat='Villages', sec='', x=.14, y=.11)]
        self.assertEqual(detector.area_marker_owners([a, b], markers), {0: 0, 1: 1})
        self.assertEqual(detector.area_marker_owners([b, a], markers), {0: 1, 1: 0})

    def test_contained_town_name_is_not_reused_by_nearby_zone(self):
        with tempfile.TemporaryDirectory() as folder:
            source = Path(folder) / 'markers.bin'
            category = b'Villages'
            data = b'SCMP' + struct.pack('<HH', 1, 1) + struct.pack('<IB', 1, 0)
            data += struct.pack('<B', len(category)) + category + bytes(9) + struct.pack('<I', 1)
            title = b'Alpha'
            data += struct.pack('<IIffH', 1, 1, .11, .11, len(title)) + title
            source.write_bytes(data)
            zones = [dict(points=self.polygon(.1,.1,.3,.3)*1000, colour='Orange'),
                     dict(points=self.polygon(.075,.095,.09,.12)*1000, colour='Orange')]
            detector.assign_scum_names(zones, None, (1000,1000), source)
            self.assertTrue(zones[0]['name'].endswith(' Alpha'))
            self.assertTrue(zones[1]['name'].endswith(' Zone'))
            self.assertNotIn('Alpha', zones[1]['name'])

    def outline(self, image):
        contours, _ = cv2.findContours(image, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_NONE)
        return detector.trace_outline(max(contours, key=cv2.contourArea))

    def test_circle_size_and_center(self):
        for radius in (15, 30, 70):
            image = np.zeros((200, 200), np.uint8)
            cv2.circle(image, (96, 103), radius, 255, -1)
            points = self.outline(image)
            self.assertEqual(len(points), 64)
            np.testing.assert_allclose(points.mean(axis=0), (96, 103), atol=.2)
            self.assertLess(abs(np.linalg.norm(points[0] - (96, 103)) - radius), 1)

    def test_rotated_rectangle_keeps_corners(self):
        image = np.zeros((200, 200), np.uint8)
        expected = cv2.boxPoints(((100, 100), (90, 44), 32)).astype(np.int32)
        cv2.fillPoly(image, [expected], 255)
        points = self.outline(image)
        self.assertEqual(len(points), 4)
        for corner in expected:
            self.assertLess(np.linalg.norm(points - corner, axis=1).min(), 2)

    def test_irregular_polygon_is_normalized_to_rectangle(self):
        image = np.zeros((200, 200), np.uint8)
        expected = np.array([[20, 20], [140, 20], [140, 60], [70, 60], [70, 140], [20, 140]])
        cv2.fillPoly(image, [expected], 255)
        points = self.outline(image)
        self.assertEqual(len(points), 4)
        edges = np.roll(points, -1, axis=0)-points
        self.assertLess(abs(np.dot(edges[0], edges[1])), .01)
        self.assertGreaterEqual(cv2.contourArea(points), cv2.contourArea(expected.astype(np.float32)))

    def test_clipped_circle_stays_inside_image(self):
        image = np.zeros((120, 120), np.uint8)
        cv2.circle(image, (60, 8), 35, 255, -1)
        points = self.outline(image)
        self.assertLess(len(points), 64)
        self.assertGreaterEqual(points[:, 1].min(), 0)


if __name__ == '__main__':
    unittest.main()
