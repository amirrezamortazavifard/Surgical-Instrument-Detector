from ultralytics import YOLO
import cv2
import os

MODEL_PATH = '..\\Surgical-Instrument-Detector\\result\\weights\\best.pt'
IMAGE_PATH = '..\\Surgical-Instrument-Detector\\images\\\\test_image.jpg'
OUTPUT_DIR = 'inference_results'

def perform_detection(model_path, image_path):
    try:
        model = YOLO(model_path)
    except Exception as e:
        print(f"Error loading model: {e}")
        print(f"Please ensure the path '{model_path}' is correct.")
        return

    try:
        results = model(image_path)
    except Exception as e:
        print(f"Error processing image: {e}")
        print(f"Please ensure the path '{image_path}' is a valid image file.")
        return

    result = results[0]
    annotated_image = result.plot()

    window_name = "YOLOv8 Detection Result - Press any key to exit"
    cv2.imshow(window_name, annotated_image)
    
    if not os.path.exists(OUTPUT_DIR):
        os.makedirs(OUTPUT_DIR)
        
    output_filename = os.path.basename(image_path)
    output_path = os.path.join(OUTPUT_DIR, output_filename)
    cv2.imwrite(output_path, annotated_image)
    print(f"Result saved to: {output_path}")

    print("Press any key to close the image window...")
    cv2.waitKey(0)
    cv2.destroyAllWindows()

if __name__ == "__main__":
    if not os.path.exists(MODEL_PATH):
         print(f"FATAL: Model file not found at '{MODEL_PATH}'")
         print("Please update the MODEL_PATH variable with the correct path to your 'best.pt' file.")
    elif not os.path.exists(IMAGE_PATH):
         print(f"FATAL: Test image not found at '{IMAGE_PATH}'")
         print("Please update the IMAGE_PATH variable with a valid path to an image you want to test.")
    else:
        perform_detection(MODEL_PATH, IMAGE_PATH)