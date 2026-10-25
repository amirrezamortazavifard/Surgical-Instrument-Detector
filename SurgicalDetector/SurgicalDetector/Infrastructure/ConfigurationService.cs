using System;
using System.IO;
using System.Text.Json;

namespace SurgicalDetector.Infrastructure
{
    public class AppSettingsModel
    {
        public string GeminiApiKey { get; set; } = string.Empty;
        public float ConfidenceThreshold { get; set; } = 0.5f;
        public float NmsThreshold { get; set; } = 0.45f;
        public bool UseLetterbox { get; set; } = true;
        public string ExecutionProvider { get; set; } = "CPU";
    }

    public interface IConfigurationService
    {
        string GeminiApiKey { get; set; }
        float ConfidenceThreshold { get; set; }
        float NmsThreshold { get; set; }
        bool UseLetterbox { get; set; }
        string ExecutionProvider { get; set; }
        bool HasGeminiApiKey { get; }
        string GetAppDataDirectory();
        void Save();
    }

    public class ConfigurationService : IConfigurationService
    {
        private static readonly Lazy<ConfigurationService> _lazyInstance =
            new Lazy<ConfigurationService>(() => new ConfigurationService());

        public static ConfigurationService Instance => _lazyInstance.Value;

        private readonly object _lock = new object();
        private AppSettingsModel _settings;
        private readonly string _configFilePath;

        public ConfigurationService()
        {
            var appDataDir = GetAppDataDirectory();
            if (!Directory.Exists(appDataDir))
            {
                try
                {
                    Directory.CreateDirectory(appDataDir);
                }
                catch
                {
                    // Fall back to current directory if AppData is inaccessible
                }
            }

            _configFilePath = Path.Combine(appDataDir, "config.json");
            _settings = LoadSettings();
        }

        public string GetAppDataDirectory()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(localAppData))
            {
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data");
            }
            return Path.Combine(localAppData, "SurgicalDetector");
        }

        private AppSettingsModel LoadSettings()
        {
            AppSettingsModel model = null;

            // 1. Try reading from %LOCALAPPDATA%\SurgicalDetector\config.json
            if (File.Exists(_configFilePath))
            {
                try
                {
                    var json = File.ReadAllText(_configFilePath);
                    model = JsonSerializer.Deserialize<AppSettingsModel>(json);
                }
                catch
                {
                    model = null;
                }
            }

            // 2. Try fallback to AppDomain.CurrentDomain.BaseDirectory\config.json
            if (model == null)
            {
                var localFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (File.Exists(localFile))
                {
                    try
                    {
                        var json = File.ReadAllText(localFile);
                        model = JsonSerializer.Deserialize<AppSettingsModel>(json);
                    }
                    catch
                    {
                        model = null;
                    }
                }
            }

            model ??= new AppSettingsModel();

            // 3. Environment variable override for GEMINI_API_KEY
            var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
            if (!string.IsNullOrWhiteSpace(envKey) && string.IsNullOrWhiteSpace(model.GeminiApiKey))
            {
                model.GeminiApiKey = envKey.Trim();
            }

            return model;
        }

        public string GeminiApiKey
        {
            get
            {
                lock (_lock)
                {
                    if (string.IsNullOrWhiteSpace(_settings.GeminiApiKey))
                    {
                        var envKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");
                        if (!string.IsNullOrWhiteSpace(envKey))
                        {
                            return envKey.Trim();
                        }
                    }
                    return _settings.GeminiApiKey ?? string.Empty;
                }
            }
            set
            {
                lock (_lock)
                {
                    _settings.GeminiApiKey = value?.Trim() ?? string.Empty;
                }
            }
        }

        public float ConfidenceThreshold
        {
            get { lock (_lock) { return _settings.ConfidenceThreshold; } }
            set { lock (_lock) { _settings.ConfidenceThreshold = Math.Clamp(value, 0.05f, 0.99f); } }
        }

        public float NmsThreshold
        {
            get { lock (_lock) { return _settings.NmsThreshold; } }
            set { lock (_lock) { _settings.NmsThreshold = Math.Clamp(value, 0.05f, 0.99f); } }
        }

        public bool UseLetterbox
        {
            get { lock (_lock) { return _settings.UseLetterbox; } }
            set { lock (_lock) { _settings.UseLetterbox = value; } }
        }

        public string ExecutionProvider
        {
            get { lock (_lock) { return _settings.ExecutionProvider; } }
            set { lock (_lock) { _settings.ExecutionProvider = value ?? "CPU"; } }
        }

        public bool HasGeminiApiKey => !string.IsNullOrWhiteSpace(GeminiApiKey);

        public void Save()
        {
            lock (_lock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(_configFilePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_configFilePath, json);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to save config: {ex.Message}");
                }
            }
        }
    }
}
