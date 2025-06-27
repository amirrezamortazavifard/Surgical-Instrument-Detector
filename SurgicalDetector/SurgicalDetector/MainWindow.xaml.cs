using System.Windows;

namespace SurgicalDetector
{
    
    public partial class MainWindow : Window
    {
        public MainWindow()
        {   
            InitializeComponent();
        }
       
        private void ImageDetectionButton_Click(object sender, RoutedEventArgs e)
        {
            var imageWindow = new ImageDetectionWindow();
            imageWindow.Show();
            this.Close();
        }

        private void LiveDetectionButton_Click(object sender, RoutedEventArgs e)
        {
            var liveWindow = new LiveDetectionWindow();
            liveWindow.Show();
            this.Close();
        }
    }
}