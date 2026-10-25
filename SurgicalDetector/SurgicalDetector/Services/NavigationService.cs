using Microsoft.Extensions.DependencyInjection;
using SurgicalDetector.ViewModels;
using System;

namespace SurgicalDetector.Services
{
    public class NavigationService : INavigationService
    {
        private readonly IServiceProvider _serviceProvider;
        private BaseViewModel? _currentViewModel;

        public event Action<BaseViewModel>? CurrentViewModelChanged;

        public NavigationService(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public void NavigateTo<TViewModel>() where TViewModel : BaseViewModel
        {
            if (_currentViewModel is INavigationAware departing)
            {
                try
                {
                    departing.OnNavigatedFrom();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation teardown exception: {ex.Message}");
                }
            }

            var nextViewModel = _serviceProvider.GetRequiredService<TViewModel>();
            _currentViewModel = nextViewModel;

            if (nextViewModel is INavigationAware entering)
            {
                try
                {
                    entering.OnNavigatedTo();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Navigation entry exception: {ex.Message}");
                }
            }

            CurrentViewModelChanged?.Invoke(nextViewModel);
        }
    }
}