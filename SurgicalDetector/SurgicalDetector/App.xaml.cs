using Microsoft.Extensions.DependencyInjection;
using SurgicalDetector.Services;
using SurgicalDetector.ViewModels;
using System;
using System.Windows;
namespace SurgicalDetector
{
    public partial class App : Application
    {
        public static IServiceProvider ServiceProvider { get; private set; }
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

            services.AddSingleton<ShellViewModel>();
            services.AddTransient<ImageDetectionViewModel>();
            services.AddTransient<LiveDetectionViewModel>();
            services.AddTransient<InstrumentDatabaseViewModel>();
            services.AddTransient<SettingsViewModel>();
            services.AddTransient<AboutViewModel>();
            services.AddTransient<ChatViewModel>();

            services.AddSingleton<MainWindow>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            var mainWindow = ServiceProvider.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }
    }
}