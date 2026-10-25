#!/usr/bin/env python3
"""
test.py
Single-image inference demonstration for surgical instrument detection.
"""

import os
import argparse
import cv2
from ultralytics import YOLO


def parse_args():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    default_model = os.path.join(script_dir, "YOLO_Result", "weights", "best.pt")
    if not os.path.exists(default_model):
        default_model = os.path.join(script_dir, "..", "SurgicalDetector", "SurgicalDetector", "best.onnx")

    default_image = os.path.join(script_dir, "..", "Images", "Test", "test_image.jpg")

    parser = argparse.ArgumentParser(description="Test YOLOv11 surgical instrument detection on a single image")
    parser.add_argument("--model", type=str, default=default_model,
                        help="Path to trained model (.pt or .onnx)")
    parser.add_argument("--image", type=str, default=default_image,
                        help="Path to input surgical image")
    parser.add_argument("--output-dir", type=str, default=os.path.join(script_dir, "inference_results"),
                        help="Directory to save annotated result")
    parser.add_argument("--conf", type=float, default=0.5,
                        help="Confidence threshold (default: 0.5)")
    parser.add_argument("--headless", action="store_true",
                        help="Run without GUI window popup (ideal for automated testing/CI)")
    return parser.parse_args()


def perform_detection(model_path, image_path, output_dir, conf=0.5, headless=False):
    model_path = os.path.normpath(model_path)
    image_path = os.path.normpath(image_path)

    if not os.path.exists(model_path):
        print(f"Error: Model file not found at '{model_path}'")
        return False

    if not os.path.exists(image_path):
        print(f"Error: Test image not found at '{image_path}'")
        return False

    print(f"Loading model: {model_path}")
    try:
        model = YOLO(model_path)
    except Exception as e:
        print(f"Error loading model: {e}")
        return False

    print(f"Processing image: {image_path}")
    try:
        results = model(image_path, conf=conf)
    except Exception as e:
        print(f"Error processing image: {e}")
        return False

    result = results[0]
    annotated_image = result.plot()

    os.makedirs(output_dir, exist_ok=True)
    output_filename = os.path.basename(image_path)
    output_path = os.path.join(output_dir, output_filename)
    cv2.imwrite(output_path, annotated_image)
    print(f"Annotated result saved to: {output_path}")

    # Print detected boxes
    if hasattr(result, 'boxes') and len(result.boxes) > 0:
        print(f"Detected {len(result.boxes)} instruments:")
        for box in result.boxes:
            cls_id = int(box.cls)
            cls_name = model.names.get(cls_id, f"Class {cls_id}")
            score = float(box.conf)
            print(f" - {cls_name} ({score:.1%})")
    else:
        print("No instruments detected above confidence threshold.")

    if not headless:
        window_name = "YOLOv11 Surgical Detection - Press any key to exit"
        cv2.imshow(window_name, annotated_image)
        print("Press any key to close the image window...")
        cv2.waitKey(0)
        cv2.destroyAllWindows()

    return True


if __name__ == "__main__":
    args = parse_args()
    perform_detection(args.model, args.image, args.output_dir, args.conf, args.headless)