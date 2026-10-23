using SurgicalDetector.ViewModels;
using System;

namespace SurgicalDetector.Services
{
    public interface INavigationService
    {
        event Action<BaseViewModel> CurrentViewModelChanged;
        void NavigateTo<TViewModel>() where TViewModel : BaseViewModel;
    }
}