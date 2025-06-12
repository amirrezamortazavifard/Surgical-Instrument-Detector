# organize_dataset.py

import os
import shutil
import random

# --- Configuration ---
SOURCE_DATA_PATH = 'source_dataset\\labeled surgical tools'
SPLIT_FILES_PATH = os.path.join(SOURCE_DATA_PATH, 'Test-Train Groups')
DEST_PATH = 'organized_dataset'
VALIDATION_SPLIT = 0.15

# --- Helper function to extract filename ---
def get_filename_from_path(line):
    return os.path.basename(line.strip())

# --- Read the train and test file lists from the author's files ---
with open(os.path.join(SPLIT_FILES_PATH, 'train_new.txt'), 'r') as f:
    train_filenames = {get_filename_from_path(line) for line in f}

with open(os.path.join(SPLIT_FILES_PATH, 'test_new.txt'), 'r') as f:
    test_filenames = {get_filename_from_path(line) for line in f}

print(f"Found {len(train_filenames)} training files and {len(test_filenames)} testing files from the source .txt files.")

# --- Create destination directories ---
for folder in ['train', 'val', 'test']:
    os.makedirs(os.path.join(DEST_PATH, 'images', folder), exist_ok=True)
    os.makedirs(os.path.join(DEST_PATH, 'labels', folder), exist_ok=True)

# --- Split training data into train and validation ---
train_list = list(train_filenames)
random.shuffle(train_list)
split_index = int(len(train_list) * (1 - VALIDATION_SPLIT))
val_filenames = set(train_list[split_index:])
train_filenames_final = set(train_list[:split_index])

print(f"Splitting train set into {len(train_filenames_final)} for training and {len(val_filenames)} for validation.")

# --- Copy files to the new structured directory ---
source_images_path = os.path.join(SOURCE_DATA_PATH, 'images')
source_labels_path = os.path.join(SOURCE_DATA_PATH, 'labels')

all_source_files = os.listdir(source_images_path)
for image_filename in all_source_files:
    if not image_filename.endswith('.jpg'):
        continue

    label_filename = image_filename.replace('.jpg', '.txt')
    source_image_path = os.path.join(source_images_path, image_filename)
    source_label_path = os.path.join(source_labels_path, label_filename)

    if not os.path.exists(source_label_path):
        continue

    dest_folder = ''
    if image_filename in train_filenames_final:
        dest_folder = 'train'
    elif image_filename in val_filenames:
        dest_folder = 'val'
    elif image_filename in test_filenames:
        dest_folder = 'test'
    
    if dest_folder:
        shutil.copy(source_image_path, os.path.join(DEST_PATH, 'images', dest_folder, image_filename))
        shutil.copy(source_label_path, os.path.join(DEST_PATH, 'labels', dest_folder, label_filename))

print("Dataset successfully organized into train, val, and test sets based on the provided files!")