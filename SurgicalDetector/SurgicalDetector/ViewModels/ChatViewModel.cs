using SurgicalDetector.Infrastructure;
using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace SurgicalDetector.ViewModels
{
    public class ChatViewModel : BaseViewModel
    {
        private readonly IChatService _chatService;
        private readonly IUIService _uiService;
    private ChatSession _selectedSession;
        private string _inputText;
        private string _attachedImagePath;
        private ImageSource _attachedImagePreview;
        private bool _isTyping;
        private string _searchText;

        public ObservableCollection<ChatSession> Sessions { get; } = new ObservableCollection<ChatSession>();
        public ObservableCollection<ChatMessage> CurrentMessages { get; } = new ObservableCollection<ChatMessage>();
        public ObservableCollection<ChatSession> FilteredSessions { get; private set; }

        public ChatSession SelectedSession
        {
            get => _selectedSession;
            set
            {
                if (SetProperty(ref _selectedSession, value))
                {
                    LoadMessagesForSession();
                }
            }
        }

        public string InputText
        {
            get => _inputText;
            set => SetProperty(ref _inputText, value);
        }

        public string AttachedImagePath
        {
            get => _attachedImagePath;
            set
            {
                if (SetProperty(ref _attachedImagePath, value))
                {
                    if (!string.IsNullOrEmpty(value))
                    {
                        var bitmap = new BitmapImage();
                        bitmap.BeginInit();
                        bitmap.UriSource = new Uri(value);
                        bitmap.CacheOption = BitmapCacheOption.OnLoad;
                        bitmap.EndInit();
                        AttachedImagePreview = bitmap;
                    }
                    else
                    {
                        AttachedImagePreview = null;
                    }
                }
            }
        }

        public ImageSource AttachedImagePreview
        {
            get => _attachedImagePreview;
            set => SetProperty(ref _attachedImagePreview, value);
        }

        public bool IsTyping
        {
            get => _isTyping;
            set => SetProperty(ref _isTyping, value);
        }

        public string SearchText
        {
            get => _searchText;
            set
            {
                SetProperty(ref _searchText, value);
                FilterSessions();
            }
        }

        public ICommand SendCommand { get; }
        public ICommand AttachImageCommand { get; }
        public ICommand RemoveImageCommand { get; }
        public ICommand NewChatCommand { get; }
        public ICommand DeleteChatCommand { get; }
        public ICommand RenameChatCommand { get; }
        public ICommand SaveChatTitleCommand { get; }

        public ChatViewModel(IChatService chatService, IUIService uiService)
        {
            _chatService = chatService;
            _uiService = uiService;

            SendCommand = new RelayCommand(async _ => await SendMessageAsync(), _ => !IsTyping && (!string.IsNullOrWhiteSpace(InputText) || AttachedImagePath != null));
            AttachImageCommand = new RelayCommand(_ => AttachImage());
            RemoveImageCommand = new RelayCommand(_ => AttachedImagePath = null);
            NewChatCommand = new RelayCommand(_ => CreateNewSession());
            DeleteChatCommand = new RelayCommand(DeleteSession);
            RenameChatCommand = new RelayCommand(RenameSession);
            SaveChatTitleCommand = new RelayCommand(SaveSessionTitle);

            LoadSessionsAsync();
        }

        private async void LoadSessionsAsync()
        {
            var sessions = await _chatService.LoadAllSessionsAsync();
            Sessions.Clear();
            foreach (var s in sessions) Sessions.Add(s);
            FilterSessions();

            if (Sessions.Any())
                SelectedSession = Sessions.First();
            else
                CreateNewSession();
        }

        private void FilterSessions()
        {
            if (string.IsNullOrWhiteSpace(SearchText))
            {
                FilteredSessions = new ObservableCollection<ChatSession>(Sessions);
            }
            else
            {
                FilteredSessions = new ObservableCollection<ChatSession>(Sessions.Where(s => s.Title.ToLower().Contains(SearchText.ToLower())));
            }
            OnPropertyChanged(nameof(FilteredSessions));
        }

        private void LoadMessagesForSession()
        {
            CurrentMessages.Clear();
            if (SelectedSession != null)
            {
                foreach (var msg in SelectedSession.Messages)
                {
                    if (msg.HasImage && msg.DisplayImage == null)
                    {
                        try
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.StreamSource = new MemoryStream(Convert.FromBase64String(msg.ImageBase64));
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            bitmap.Freeze();
                            msg.DisplayImage = bitmap;
                        }
                        catch { }
                    }
                    CurrentMessages.Add(msg);
                }
            }
        }

        private void CreateNewSession()
        {
            var newSession = new ChatSession
            {
                Id = Guid.NewGuid().ToString(),
                Title = "New Chat",
                LastUpdated = DateTime.Now
            };
            Sessions.Insert(0, newSession);
            SelectedSession = newSession;
            FilterSessions();
        }

        private void AttachImage()
        {
            var path = _uiService.ShowOpenFileDialog("Images|*.jpg;*.jpeg;*.png");
            if (!string.IsNullOrEmpty(path))
            {
                AttachedImagePath = path;
            }
        }

        private async Task SendMessageAsync()
        {
            if (SelectedSession == null) CreateNewSession();

            var userContent = InputText;
            var imagePath = AttachedImagePath;
            string imageBase64 = null;
            BitmapImage displayImg = null;

            InputText = "";
            AttachedImagePath = null;

            if (!string.IsNullOrEmpty(imagePath))
            {
                var bytes = await File.ReadAllBytesAsync(imagePath);
                imageBase64 = Convert.ToBase64String(bytes);

                displayImg = new BitmapImage();
                displayImg.BeginInit();
                displayImg.StreamSource = new MemoryStream(bytes);
                displayImg.CacheOption = BitmapCacheOption.OnLoad;
                displayImg.EndInit();
                displayImg.Freeze();
            }

            var userMsg = new ChatMessage
            {
                Role = "user",
                Content = userContent,
                Timestamp = DateTime.Now,
                ImageBase64 = imageBase64,
                DisplayImage = displayImg
            };

            CurrentMessages.Add(userMsg);
            SelectedSession.Messages.Add(userMsg);
            SelectedSession.LastUpdated = DateTime.Now;

            if (Sessions.Contains(SelectedSession))
            {
                Sessions.Move(Sessions.IndexOf(SelectedSession), 0);
            }

            IsTyping = true;

            if (SelectedSession.Messages.Count == 1)
            {
                var title = await _chatService.GenerateTitleAsync(userContent);
                SelectedSession.Title = title.Replace("\"", "");
                // Refresh to show title
                FilterSessions();
            }

            var aiResponseText = await _chatService.GetAiResponseAsync(SelectedSession.Messages, userContent, imageBase64);

            var aiMsg = new ChatMessage
            {
                Role = "model",
                Content = "",
                Timestamp = DateTime.Now
            };
            CurrentMessages.Add(aiMsg);

            IsTyping = false;

            foreach (var word in aiResponseText.Split(' '))
            {
                aiMsg.Content += word + " ";
                await Task.Delay(50);
            }

            aiMsg.Content = aiResponseText.Trim();
            SelectedSession.Messages.Add(aiMsg);
            await _chatService.SaveSessionAsync(SelectedSession);
        }

        private async void DeleteSession(object param)
        {
            if (param is ChatSession session)
            {
                await _chatService.DeleteSessionAsync(session.Id);
                Sessions.Remove(session);
                FilterSessions();
                if (SelectedSession == session)
                {
                    SelectedSession = Sessions.FirstOrDefault();
                }
            }
        }

        private void RenameSession(object param)
        {
            if (param is ChatSession session)
            {
                session.IsEditing = true;
            }
        }

        private async void SaveSessionTitle(object param)
        {
            if (param is ChatSession session)
            {
                session.IsEditing = false;
                await _chatService.SaveSessionAsync(session);
                FilterSessions();
            }
        }
    }
}