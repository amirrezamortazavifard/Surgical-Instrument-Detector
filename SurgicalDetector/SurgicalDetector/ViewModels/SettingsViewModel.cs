using SurgicalDetector.Infrastructure;
using System;
using System.Windows.Input;

namespace SurgicalDetector.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private readonly IConfigurationService _configService;

        private string _geminiApiKey;
        private float _confidenceThreshold;
        private float _nmsThreshold;
        private bool _useLetterbox;
        private string _executionProvider;
        private string _statusMessage;
        private bool _isStatusSuccess;

        public string Title { get; } = "Settings";

        public string GeminiApiKey
        {
            get => _geminiApiKey;
            set => SetProperty(ref _geminiApiKey, value);
        }

        public float ConfidenceThreshold
        {
            get => _confidenceThreshold;
            set
            {
                if (SetProperty(ref _confidenceThreshold, (float)Math.Round(value, 2)))
                {
                    OnPropertyChanged(nameof(ConfidencePercentageText));
                }
            }
        }

        public string ConfidencePercentageText => $"{(int)(ConfidenceThreshold * 100)}%";

        public float NmsThreshold
        {
            get => _nmsThreshold;
            set
            {
                if (SetProperty(ref _nmsThreshold, (float)Math.Round(value, 2)))
                {
                    OnPropertyChanged(nameof(NmsPercentageText));
                }
            }
        }

        public string NmsPercentageText => $"{(int)(NmsThreshold * 100)}%";

        public bool UseLetterbox
        {
            get => _useLetterbox;
            set => SetProperty(ref _useLetterbox, value);
        }

        public string ExecutionProvider
        {
            get => _executionProvider;
            set => SetProperty(ref _executionProvider, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        public bool IsStatusSuccess
        {
            get => _isStatusSuccess;
            set => SetProperty(ref _isStatusSuccess, value);
        }

        public ICommand SaveSettingsCommand { get; }
        public ICommand ResetDefaultsCommand { get; }

        public SettingsViewModel() : this(ConfigurationService.Instance)
        {
        }

        public SettingsViewModel(IConfigurationService configService)
        {
            _configService = configService;

            // Load current settings
            _geminiApiKey = _configService.GeminiApiKey;
            _confidenceThreshold = _configService.ConfidenceThreshold;
            _nmsThreshold = _configService.NmsThreshold;
            _useLetterbox = _configService.UseLetterbox;
            _executionProvider = _configService.ExecutionProvider;

            SaveSettingsCommand = new RelayCommand(OnSaveSettings);
            ResetDefaultsCommand = new RelayCommand(OnResetDefaults);
        }

        private void OnSaveSettings(object parameter)
        {
            try
            {
                _configService.GeminiApiKey = _geminiApiKey;
                _configService.ConfidenceThreshold = _confidenceThreshold;
                _configService.NmsThreshold = _nmsThreshold;
                _configService.UseLetterbox = _useLetterbox;
                _configService.ExecutionProvider = _executionProvider;
                _configService.Save();

                StatusMessage = "Settings saved successfully to user configuration.";
                IsStatusSuccess = true;
            }
            catch (Exception ex)
            {
                StatusMessage = $"Failed to save settings: {ex.Message}";
                IsStatusSuccess = false;
            }
        }

        private void OnResetDefaults(object parameter)
        {
            ConfidenceThreshold = 0.50f;
            NmsThreshold = 0.45f;
            UseLetterbox = true;
            ExecutionProvider = "CPU";
            StatusMessage = "Defaults restored. Click 'Save Configuration' to persist.";
            IsStatusSuccess = true;
        }
    }
}