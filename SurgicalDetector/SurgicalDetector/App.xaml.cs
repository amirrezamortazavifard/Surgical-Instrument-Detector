using Microsoft.Extensions.DependencyInjection;
using SurgicalDetector.Services;
using SurgicalDetector.ViewModels;
using System;
using System.Windows;

namespace SurgicalDetector
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; } = null!;
        public static int GlobalScanCount { get; set; } = 0;

        public App()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IDetectionService, OnnxDetectionService>();
            services.AddSingleton<INavigationService, NavigationService>();
            services.AddSingleton<IUIService, WpfUIService>();
            services.AddSingleton<IInstrumentService, InstrumentService>();
            services.AddSingleton<ICameraService, OpenCvCameraService>();
            services.AddSingleton<IGenAIService, BaymaxAiService>();
            services.AddSingleton<IChatService, ChatService>();
            services.AddSingleton<Infrastructure.IConfigurationService>(Infrastructure.ConfigurationService.Instance);

            services.AddSingleton<ShellViewModel>();
            services.AddSingleton<ImageDetectionViewModel>();
            services.AddSingleton<LiveDetectionViewModel>();
            services.AddSingleton<InstrumentDatabaseViewModel>();
            services.AddSingleton<SettingsViewModel>();
            services.AddSingleton<AboutViewModel>();
            services.AddSingleton<ChatViewModel>();

            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                var cameraService = ServiceProvider?.GetService<ICameraService>();
                cameraService?.Dispose();

                var liveVm = ServiceProvider?.GetService<LiveDetectionViewModel>();
                liveVm?.Dispose();

                var detectionService = ServiceProvider?.GetService<IDetectionService>() as IDisposable;
                detectionService?.Dispose();
            }
            catch { }

            base.OnExit(e);
        }
    }
}