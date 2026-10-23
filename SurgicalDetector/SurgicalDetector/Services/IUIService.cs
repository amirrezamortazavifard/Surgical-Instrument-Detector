namespace SurgicalDetector.Services
{
    public interface IUIService
    {
        string ShowOpenFileDialog(string filter);
        void ShowError(string message);
    }
}