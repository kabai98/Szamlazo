"""Run with an environment containing opencv-python and numpy: python -m unittest discover -s tests/ocr."""
import importlib.util
from pathlib import Path
from types import SimpleNamespace
import unittest
from unittest.mock import patch

import cv2
import numpy as np

tool = Path(__file__).resolve().parents[2] / "backend/Infrastructure/Importing/Tools/prepare_receipt.py"
spec = importlib.util.spec_from_file_location("prepare_receipt", tool)
prepare = importlib.util.module_from_spec(spec)
spec.loader.exec_module(prepare)


class ReceiptPreprocessingTests(unittest.TestCase):
    def test_uncertain_orientation_keeps_original(self):
        image = np.arange(72, dtype=np.uint8).reshape(4, 6, 3)
        result = SimpleNamespace(returncode=0, stdout="Rotate: 180\nOrientation confidence: 0.76")
        with patch.object(prepare.subprocess, "run", return_value=result):
            np.testing.assert_array_equal(image, prepare.orient(image, "receipt.png"))

    def test_supported_quarter_turn_preserves_pixels(self):
        image = np.arange(72, dtype=np.uint8).reshape(4, 6, 3)
        result = SimpleNamespace(returncode=0, stdout="Rotate: 90\nOrientation confidence: 12.5")
        with patch.object(prepare.subprocess, "run", return_value=result):
            rotated = prepare.orient(image, "receipt.png")
        self.assertEqual((6, 4, 3), rotated.shape)
        np.testing.assert_array_equal(np.sort(image.ravel()), np.sort(rotated.ravel()))

    def test_deskew_expands_canvas_and_does_not_modify_original(self):
        image = np.full((500, 300), 255, np.uint8)
        for y in (60, 140, 220, 300, 380):
            cv2.line(image, (10, y), (285, y + 20), 0, 2)
        original = image.copy()
        result = prepare.deskew(image)
        np.testing.assert_array_equal(original, image)
        self.assertGreaterEqual(result.shape[0], image.shape[0])
        self.assertGreaterEqual(result.shape[1], image.shape[1])


if __name__ == "__main__":
    unittest.main()
