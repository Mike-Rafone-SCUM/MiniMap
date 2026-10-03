"""Geometry regressions for the screenshot importer (requires packaging dependencies)."""
import importlib.util
from pathlib import Path
import unittest

import cv2
import numpy as np

spec = importlib.util.spec_from_file_location(
    'detect_zones', Path(__file__).resolve().parents[1] / 'packaging' / 'detect_zones.py')
detector = importlib.util.module_from_spec(spec)
spec.loader.exec_module(detector)


class ZoneGeometryTests(unittest.TestCase):
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

    def test_irregular_polygon_is_not_enclosed_in_rectangle(self):
        image = np.zeros((200, 200), np.uint8)
        expected = np.array([[20, 20], [140, 20], [140, 60], [70, 60], [70, 140], [20, 140]])
        cv2.fillPoly(image, [expected], 255)
        points = self.outline(image)
        self.assertEqual(len(points), 6)
        self.assertLess(abs(cv2.contourArea(points) - cv2.contourArea(expected.astype(np.float32))), 150)

    def test_clipped_circle_stays_inside_image(self):
        image = np.zeros((120, 120), np.uint8)
        cv2.circle(image, (60, 8), 35, 255, -1)
        points = self.outline(image)
        self.assertLess(len(points), 64)
        self.assertGreaterEqual(points[:, 1].min(), 0)


if __name__ == '__main__':
    unittest.main()
