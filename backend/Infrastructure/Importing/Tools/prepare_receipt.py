"""Produce a contrast-normalized receipt candidate for local OCR."""
import sys

import cv2
import numpy as np


def prepare(source, destination):
    image = cv2.imread(source, cv2.IMREAD_GRAYSCALE)
    if image is None or image.size > 40_000_000:
        raise ValueError("Unreadable or oversized receipt image")

    mask = cv2.inRange(image, 115, 255)
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((15, 15), np.uint8))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        cv2.imwrite(destination, image)
        return

    points = max(contours, key=cv2.contourArea).reshape(-1, 2).astype(np.float32)
    sums = points.sum(axis=1)
    differences = points[:, 1] - points[:, 0]
    corners = np.array([
        points[sums.argmin()], points[differences.argmin()],
        points[sums.argmax()], points[differences.argmax()],
    ], np.float32)
    width = int(max(np.linalg.norm(corners[0] - corners[1]), np.linalg.norm(corners[2] - corners[3])))
    height = int(max(np.linalg.norm(corners[0] - corners[3]), np.linalg.norm(corners[1] - corners[2])))
    if width < 50 or height < 50:
        cv2.imwrite(destination, image)
        return

    target = np.array([[0, 0], [width, 0], [width, height], [0, height]], np.float32)
    transform = cv2.getPerspectiveTransform(corners, target)
    crop = cv2.warpPerspective(image, transform, (width, height))
    background = cv2.GaussianBlur(crop, (0, 0), 25)
    normalized = cv2.divide(crop, background, scale=255)
    cv2.imwrite(destination, normalized)


if __name__ == "__main__":
    prepare(sys.argv[1], sys.argv[2])
