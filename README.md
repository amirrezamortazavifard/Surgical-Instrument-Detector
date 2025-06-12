# Real-Time Surgical Instrument Detection using YOLOv8

This project utilizes the YOLOv8 model to detect and classify common surgical instruments from images and video streams. It's trained on a custom dataset to identify 4 classes of tools, showcasing a practical application of computer vision in the medical technology field.

![Detection Demo](https://i.imgur.com/rLzG9qL.jpg)


## 📋 Key Features

- **Real-time detection** of 4 classes of surgical instruments.
- Built with **YOLOv8m (Medium)** for a strong balance between accuracy and speed.
- Trained and validated on the "Labeled Surgical Tools and Images" dataset.
- Includes scripts for easy data organization, training, and inference.

## 🛠️ Technology Stack

- **Python 3.9+**
- **PyTorch**
- **Ultralytics YOLOv11**
- **OpenCV**
- **NumPy**
- **PyYAML**

## 📂 Dataset

This project is trained on the **Labeled Surgical Tools and Images** dataset, which was created for a Master's Thesis. It contains over 3,000 images with detailed annotations for 4 classes of surgical tools.

- **Dataset Source:** [Kaggle - Labeled Surgical Tools and Images](https://www.kaggle.com/datasets/dlovado/labeled-surgical-tools)
- **Classes:**
  1.  `Scalpel`
  2.  `Straight Dissection Clamp`
  3.  `Straight Mayo Scissor`
  4.  `Curved Mayo Scissor`

## 🚀 Getting Started

Follow these instructions to set up the project on your local machine.

### Prerequisites

- An **NVIDIA GPU** with CUDA and cuDNN installed is highly recommended for training.
- Python 3.9 or later.

### Installation

1.  **Clone the repository:**
    ```sh
    git clone [https://github.com/your-username/your-repository-name.git](https://github.com/your-username/your-repository-name.git)
    cd your-repository-name
    ```

2.  **Create and activate a Python virtual environment:**
    ```sh
    python -m venv venv
    # On Windows
    .\venv\Scripts\activate
    # On macOS/Linux
    source venv/bin/activate
    ```

3.  **Install required libraries:**
    ```sh
    pip install -r requirements.txt
    ```
 

4.  **Download and Organize Dataset:**
    - Download the dataset from the [Kaggle link](https://www.kaggle.com/datasets/dlovado/labeled-surgical-tools) and unzip it into a folder named `source_dataset`.
    - Run the provided Python script to automatically organize the data into `train`, `val`, and `test` sets:
      ```sh
      python organize_dataset.py
      ```

## ⚙️ Usage

### Training

To train the model from scratch using the prepared dataset, run the training script. All training parameters can be adjusted within this file.

```sh
python train.py
```
The trained model weights (especially `best.pt`) will be saved in the `runs/detect/train/weights/` directory.

### Inference

To run inference on a new image, video, or webcam stream, use the `predict` command with your best-trained model.

- **On an image:**
  ```sh
  yolo predict model=runs/detect/train/weights/best.pt source='path/to/your/image.jpg'
  ```

- **On a video:**
  ```sh
  yolo predict model=runs/detect/train/weights/best.pt source='path/to/your/video.mp4'
  ```

## 📈 Results

After training for 100 epochs, the YOLOv8m model achieved the following performance on the validation set.

| Metric    | Value     |
| :-------- | :-------- |
| **mAP50-95** | **`0.XX`** |
| **mAP50** | **`0.XX`** |
| Precision | `0.XX`    |
| Recall    | `0.XX`    |


## 📄 License

This project is licensed under the MIT License. See the `LICENSE` file for details.

## 🙏 Acknowledgments

A special thanks to **Danilo L. V. et al.** for creating and sharing the high-quality "Labeled Surgical Tools and Images" dataset, which made this project possible.
