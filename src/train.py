from ultralytics import YOLO

if __name__ == '__main__':
    
    model = YOLO('yolov11l.pt')

   
    results = model.train(
        data='data.yaml',
        epochs=100,
        imgsz=640,
        lr0=0.001,  
        device=0 
    )