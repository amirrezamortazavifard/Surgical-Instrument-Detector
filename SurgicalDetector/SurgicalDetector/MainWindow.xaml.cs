using SurgicalDetector.Services;
using SurgicalDetector.ViewModels;
using System.ComponentModel;
using System.Windows;

namespace SurgicalDetector
{
    public partial class MainWindow : Window
    {
        private readonly ShellViewModel _viewModel;

        public MainWindow(ShellViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = viewModel;
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            base.OnClosing(e);
            try
            {
                if (_viewModel.CurrentViewModel is INavigationAware aware)
                {
                    aware.OnNavigatedFrom();
                }
            }
            catch { }
        }
    }
}