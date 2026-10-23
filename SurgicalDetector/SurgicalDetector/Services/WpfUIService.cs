using Microsoft.Win32;
using System.Windows;

namespace SurgicalDetector.Services
{
    public class WpfUIService : IUIService
    {
        public string ShowOpenFileDialog(string filter)
        {
            var openFileDialog = new OpenFileDialog { Filter = filter };
            if (openFileDialog.ShowDialog() == true)
            {
                return openFileDialog.FileName;
            }
            return null;
        }

        public void ShowError(string message)
        {
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}