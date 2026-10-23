using SurgicalDetector.ViewModels;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Windows.Media.Imaging;
namespace SurgicalDetector.Models
{
    public class ChatMessage : BaseViewModel
    {
        private string _role;
        private string _content;
        private DateTime _timestamp;
        private string _imageBase64;
        private BitmapImage _displayImage;

    public string Role
        {
            get => _role;
            set => SetProperty(ref _role, value);
        }

        public string Content
        {
            get => _content;
            set => SetProperty(ref _content, value);
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set => SetProperty(ref _timestamp, value);
        }

        public string ImageBase64
        {
            get => _imageBase64;
            set
            {
                if (SetProperty(ref _imageBase64, value))
                {
                    OnPropertyChanged(nameof(HasImage));
                }
            }
        }

        [JsonIgnore]
        public BitmapImage DisplayImage
        {
            get => _displayImage;
            set => SetProperty(ref _displayImage, value);
        }

        [JsonIgnore]
        public bool HasImage => !string.IsNullOrEmpty(ImageBase64);

        [JsonIgnore]
        public bool IsUser => Role == "user";
    }

    public class ChatSession : BaseViewModel
    {
        private string _title;
        private bool _isEditing;

        public string Id { get; set; }

        public string Title
        {
            get => _title;
            set => SetProperty(ref _title, value);
        }

        public DateTime LastUpdated { get; set; }
        public List<ChatMessage> Messages { get; set; } = new List<ChatMessage>();

        [JsonIgnore]
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }
    }
}