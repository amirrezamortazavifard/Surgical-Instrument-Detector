namespace SurgicalDetector.Services
{
    /// <summary>
    /// Lifecycle interface for ViewModels that need to perform setup or teardown
    /// when the user navigates into or out of their view.
    /// </summary>
    public interface INavigationAware
    {
        /// <summary>
        /// Invoked when the ViewModel becomes the active view in the application shell.
        /// </summary>
        void OnNavigatedTo();

        /// <summary>
        /// Invoked when the user navigates away from the ViewModel's view.
        /// Ideal for halting background workers, releasing hardware devices, and pausing timers.
        /// </summary>
        void OnNavigatedFrom();
    }
}
