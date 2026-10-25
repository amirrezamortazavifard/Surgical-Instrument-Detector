using SurgicalDetector.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace SurgicalDetector.Views
{
    public partial class LiveDetectionView : UserControl
    {
        public LiveDetectionView()
        {
            InitializeComponent();
            this.Unloaded += OnUnloaded;
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is LiveDetectionViewModel vm)
            {
                vm.OnNavigatedFrom();
            }
        }
    }
}