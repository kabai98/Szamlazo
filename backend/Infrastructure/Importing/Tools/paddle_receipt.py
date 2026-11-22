"""Read local receipt images with PaddleOCR and retain spatial row order."""
import contextlib
import json
import os
import statistics
import sys

os.environ.setdefault("PADDLE_PDX_DISABLE_MODEL_SOURCE_CHECK", "True")
os.environ.setdefault("OMP_NUM_THREADS", "1")


def create_reader():
    # Paddle libraries write progress to stdout. Keep the protocol output JSON-only.
    with contextlib.redirect_stdout(sys.stderr):
        from paddleocr import PaddleOCR

        return PaddleOCR(
            lang="hu",
            device="cpu",
            use_doc_orientation_classify=True,
            use_doc_unwarping=False,
            use_textline_orientation=False,
            cpu_threads=2,
            enable_mkldnn=False,
        )


def read_receipt(path, reader=None):
    with contextlib.redirect_stdout(sys.stderr):
        import cv2

        reader = reader or create_reader()
        image = cv2.imread(path)
        if image is None or image.shape[0] * image.shape[1] > 40_000_000:
            raise ValueError("Unreadable or oversized receipt image")
        pages = list(reader.predict(image))

    lines = []
    slopes = []
    for page in pages:
        result = page.json["res"]
        for text, score, box in zip(
            result["rec_texts"], result["rec_scores"], result["rec_polys"]
        ):
            if score < 0.45 or not text.strip():
                continue
            xs = [point[0] for point in box]
            ys = [point[1] for point in box]
            delta_x = box[1][0] - box[0][0]
            if delta_x:
                slopes.append((box[1][1] - box[0][1]) / delta_x)
            lines.append({
                "text": text,
                "confidence": float(score),
                "x": float(min(xs)),
                "center_x": float(sum(xs) / len(xs)),
                "y": float(sum(ys) / len(ys)),
                "height": float(max(ys) - min(ys)),
                "box": [[float(x), float(y)] for x, y in box],
            })

    slope = statistics.median(slopes) if slopes else 0
    for line in lines:
        line["row_y"] = line["y"] - slope * line["center_x"]
    rows = []
    for line in sorted(lines, key=lambda line: line["row_y"]):
        if rows and abs(rows[-1]["y"] - line["row_y"]) < min(rows[-1]["height"], line["height"]) * 0.55:
            rows[-1]["parts"].append(line)
        else:
            rows.append({"y": line["row_y"], "height": line["height"], "parts": [line]})
    text = "\n".join(
        "    ".join(part["text"] for part in sorted(row["parts"], key=lambda part: part["x"]))
        for row in rows
    )
    return {"text": text, "lines": lines}


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    print(json.dumps(read_receipt(sys.argv[1]), ensure_ascii=False))
