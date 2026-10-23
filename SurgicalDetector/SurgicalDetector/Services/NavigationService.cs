using Microsoft.Extensions.DependencyInjection;
using SurgicalDetector.ViewModels;
using System;

namespace SurgicalDetector.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        public event Action<BaseViewModel> CurrentViewModelChanged;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void NavigateTo<TViewModel>() where TViewModel : BaseViewModel
        {
            var viewModel = _serviceProvider.GetRequiredService<TViewModel>();
            CurrentViewModelChanged?.Invoke(viewModel);
        }
    }
}