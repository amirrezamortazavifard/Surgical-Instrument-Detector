using SurgicalDetector.ViewModels;
using System.Windows;

namespace SurgicalDetector
{
    public partial class MainWindow : Window
    {
        public MainWindow(ShellViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }
    }
}