import os
from ultralytics import YOLO

def main():
    # Path to the trained model
    model_path = os.path.join('YOLO_Result', 'weights', 'best.pt')
    
    if not os.path.exists(model_path):
        print(f"Error: Model not found at {model_path}")
        return

    print("Loading model...")
    model = YOLO(model_path)

    # Path to the data configuration
    data_yaml_path = 'data.yaml'
    
    if not os.path.exists(data_yaml_path):
        print(f"Error: data.yaml not found at {data_yaml_path}")
        return

    print("Starting evaluation on the TEST split (EXP-001)...")
    try:
        # We explicitly evaluate on the test split
        metrics = model.val(data=data_yaml_path, split='test', save_json=True, conf=0.5)
        
        print("\n--- TEST SET RESULTS ---")
        print(f"mAP@0.5:      {metrics.box.map50:.4f}")
        print(f"mAP@0.5:0.95: {metrics.box.map:.4f}")
        
        # Print per-class results if available
        if hasattr(metrics.box, 'maps'):
            print("\nPer-class mAP@0.5:0.95:")
            for i, class_name in enumerate(model.names.values()):
                if i < len(metrics.box.maps):
                    print(f" - {class_name}: {metrics.box.maps[i]:.4f}")
                    
        print("\nNote: Please copy these metrics for the Results section of the manuscript.")
        
    except Exception as e:
        print(f"\nEvaluation failed! Ensure the dataset is present in the path specified in data.yaml.")
        print(f"Error details: {e}")

if __name__ == '__main__':
    main()
