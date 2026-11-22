"""Create bounded OCR candidates using the Tesseract image-quality guidance."""
import sys
import os
import re
import subprocess

import cv2
import numpy as np


def orient(color, source):
    try:
        result = subprocess.run(
            ["tesseract", source, "stdout", "--psm", "0", "-l", "osd"],
            capture_output=True, text=True, timeout=10,
            env={**os.environ, "OMP_THREAD_LIMIT": "1"},
        )
    except (FileNotFoundError, subprocess.TimeoutExpired):
        return color
    rotation = re.search(r"Rotate: (90|180|270)\b", result.stdout)
    confidence = re.search(r"Orientation confidence: ([\d.]+)", result.stdout)
    # Weak OSD results can invert an upright receipt on a patterned background.
    if result.returncode != 0 or not rotation or not confidence or float(confidence.group(1)) < 5:
        return color
    rotations = {
        "90": cv2.ROTATE_90_CLOCKWISE,
        "180": cv2.ROTATE_180,
        "270": cv2.ROTATE_90_COUNTERCLOCKWISE,
    }
    return cv2.rotate(color, rotations[rotation.group(1)])


def deskew(image):
    edges = cv2.Canny(image, 50, 150)
    lines = cv2.HoughLinesP(
        edges, 1, np.pi / 180, threshold=60,
        minLineLength=max(40, image.shape[1] // 8), maxLineGap=15,
    )
    angles = []
    if lines is not None:
        for segment in lines[:, 0]:
            x1, y1, x2, y2 = segment
            angle = np.degrees(np.arctan2(y2 - y1, x2 - x1))
            if abs(angle) < 15:
                angles.append(angle)
    if not angles:
        return image
    angle = float(np.median(angles))
    height, width = image.shape
    transform = cv2.getRotationMatrix2D((width / 2, height / 2), angle, 1)
    cosine, sine = abs(transform[0, 0]), abs(transform[0, 1])
    output_width = int(np.ceil(width * cosine + height * sine))
    output_height = int(np.ceil(height * cosine + width * sine))
    transform[0, 2] += (output_width - width) / 2
    transform[1, 2] += (output_height - height) / 2
    return cv2.warpAffine(image, transform, (output_width, output_height), borderValue=255)


def paper_crop(image, color):
    hsv = cv2.cvtColor(color, cv2.COLOR_BGR2HSV)
    luminance_cutoff, _ = cv2.threshold(image, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    mask = cv2.inRange(hsv, np.array([0, 0, max(60, min(180, luminance_cutoff))], dtype=np.uint8),
                       np.array([180, 75, 255], dtype=np.uint8))
    mask = cv2.morphologyEx(mask, cv2.MORPH_CLOSE, np.ones((15, 15), np.uint8))
    contours, _ = cv2.findContours(mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not contours:
        return image
    contour = max(contours, key=cv2.contourArea)
    if cv2.contourArea(contour) < image.size * 0.25:
        return image
    polygon = cv2.approxPolyDP(contour, cv2.arcLength(contour, True) * 0.025, True)
    if len(polygon) != 4 or not cv2.isContourConvex(polygon):
        # Brightness alone is insufficient evidence for a guessed bounding-box crop.
        return image
    points = polygon.reshape(4, 2).astype(np.float32)
    sums = points.sum(axis=1)
    differences = points[:, 1] - points[:, 0]
    corners = np.array([
        points[sums.argmin()], points[differences.argmin()],
        points[sums.argmax()], points[differences.argmax()],
    ], np.float32)
    width = int(max(np.linalg.norm(corners[0] - corners[1]), np.linalg.norm(corners[2] - corners[3])))
    height = int(max(np.linalg.norm(corners[0] - corners[3]), np.linalg.norm(corners[1] - corners[2])))
    if width < 50 or height < 50 or len(np.unique(corners, axis=0)) != 4:
        return image
    target = np.array([[0, 0], [width - 1, 0], [width - 1, height - 1], [0, height - 1]], np.float32)
    transform = cv2.getPerspectiveTransform(corners, target)
    return cv2.warpPerspective(image, transform, (width, height), borderValue=255)


def prepare(source, destination):
    color = cv2.imread(source, cv2.IMREAD_COLOR)
    if color is None or color.shape[0] * color.shape[1] > 40_000_000:
        raise ValueError("Unreadable or oversized receipt image")
    color = orient(color, source)
    image = cv2.cvtColor(color, cv2.COLOR_BGR2GRAY)
    if max(image.shape) > 3200:
        scale = 3200 / max(image.shape)
        image = cv2.resize(image, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
        color = cv2.resize(color, None, fx=scale, fy=scale, interpolation=cv2.INTER_AREA)
    crop = deskew(paper_crop(image, color))
    background = cv2.GaussianBlur(crop, (0, 0), 25)
    normalized = cv2.divide(crop, background, scale=255)
    if normalized.shape[1] < 1400:
        scale = min(2, 1400 / normalized.shape[1])
        normalized = cv2.resize(normalized, None, fx=scale, fy=scale, interpolation=cv2.INTER_CUBIC)
    binary = cv2.adaptiveThreshold(
        normalized, 255, cv2.ADAPTIVE_THRESH_GAUSSIAN_C, cv2.THRESH_BINARY, 51, 15
    )
    for path, candidate in [(destination, normalized), (destination + ".binary.png", binary)]:
        candidate = cv2.copyMakeBorder(candidate, 10, 10, 10, 10, cv2.BORDER_CONSTANT, value=255)
        if not cv2.imwrite(path, candidate):
            raise ValueError("Cannot write OCR candidate")


if __name__ == "__main__":
    prepare(sys.argv[1], sys.argv[2])
