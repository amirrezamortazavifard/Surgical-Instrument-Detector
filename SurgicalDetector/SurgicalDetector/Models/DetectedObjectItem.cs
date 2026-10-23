using SurgicalDetector.Infrastructure;
using SurgicalDetector.Services;
using SurgicalDetector.ViewModels;
using System.Windows.Input;
using System.Windows.Media;
namespace SurgicalDetector.Models
{
    public class DetectedObjectItem : BaseViewModel
    {
        private readonly IGenAIService _genAIService;
        private string _aiAnalysis;
        private bool _isAiLoading;
        private bool _isAiExpanded;
        private string _currentLang;
       
    public ImageSource Thumbnail { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public float Confidence { get; set; }
        public Color BorderColor { get; set; }
        public SolidColorBrush ColorBrush { get; set; }
        public string LocalDescription { get; set; }

        public string AiAnalysis
        {
            get => _aiAnalysis;
            set => SetProperty(ref _aiAnalysis, value);
        }

        public bool IsAiLoading
        {
            get => _isAiLoading;
            set => SetProperty(ref _isAiLoading, value);
        }

        public bool IsAiExpanded
        {
            get => _isAiExpanded;
            set => SetProperty(ref _isAiExpanded, value);
        }

        public ICommand AskBaymaxCommand { get; }

        public DetectedObjectItem(IGenAIService genAIService)
        {
            _genAIService = genAIService;
            AskBaymaxCommand = new RelayCommand(OnAskBaymax);
        }

        private async void OnAskBaymax(object parameter)
        {
            string lang = parameter as string ?? "en";

            if (IsAiExpanded && _currentLang == lang)
            {
                IsAiExpanded = false;
                return;
            }

            _currentLang = lang;
            IsAiExpanded = true;
            IsAiLoading = true;
            AiAnalysis = "";

            AiAnalysis = await _genAIService.GetClinicalAnalysisAsync(Name, Category, lang);
            IsAiLoading = false;
        }
    }
}