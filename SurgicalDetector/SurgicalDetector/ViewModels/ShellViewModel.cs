using SurgicalDetector.Infrastructure;
using SurgicalDetector.Services;
using System.Windows.Input;
namespace SurgicalDetector.ViewModels
{
    public class ShellViewModel : BaseViewModel
    {
        private readonly INavigationService _navigationService;
        private BaseViewModel _currentViewModel;
        public BaseViewModel CurrentViewModel
        {
            get => _currentViewModel;
            set => SetProperty(ref _currentViewModel, value);
        }

        public ICommand NavigateImageDetectionCommand { get; }
        public ICommand NavigateLiveDetectionCommand { get; }
        public ICommand NavigateInstrumentDatabaseCommand { get; }
        public ICommand NavigateChatCommand { get; }
        public ICommand NavigateSettingsCommand { get; }
        public ICommand NavigateAboutCommand { get; }

        public ShellViewModel(INavigationService navigationService)
        {
            _navigationService = navigationService;
            _navigationService.CurrentViewModelChanged += (vm) => CurrentViewModel = vm;

            NavigateImageDetectionCommand = new RelayCommand(p => _navigationService.NavigateTo<ImageDetectionViewModel>());
            NavigateLiveDetectionCommand = new RelayCommand(p => _navigationService.NavigateTo<LiveDetectionViewModel>());
            NavigateInstrumentDatabaseCommand = new RelayCommand(p => _navigationService.NavigateTo<InstrumentDatabaseViewModel>());
            NavigateChatCommand = new RelayCommand(p => _navigationService.NavigateTo<ChatViewModel>());
            NavigateSettingsCommand = new RelayCommand(p => _navigationService.NavigateTo<SettingsViewModel>());
            NavigateAboutCommand = new RelayCommand(p => _navigationService.NavigateTo<AboutViewModel>());

            _navigationService.NavigateTo<ImageDetectionViewModel>();
        }
    }
}