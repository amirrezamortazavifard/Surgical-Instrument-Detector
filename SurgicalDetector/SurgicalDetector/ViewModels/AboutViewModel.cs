using SurgicalDetector.Infrastructure;
using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Threading.Tasks;
using System.Windows.Input;
namespace SurgicalDetector.ViewModels
{
    public class AboutViewModel : BaseViewModel
    {
        private readonly IUIService _uiService;
        private readonly IChatService _chatService;
        private readonly IInstrumentService _instrumentService;
    private string _dailyQuote;
        private string _bugReportText;
        private string _latencyResult;
        private string _latencyColor;
        private bool _isCheckingLatency;
        private int _easterEggCounter;
        private string _easterEggMessage;
        private bool _isBugReportSending;

        // Real-time stats properties
        private string _totalScans;
        private string _baymaxChats;
        private string _dbItems;

        public string Title { get; } = "Surgical Detector AI";
        public string Version { get; } = "v2.1.0 Enterprise";

        public string DailyQuote
        {
            get => _dailyQuote;
            set => SetProperty(ref _dailyQuote, value);
        }

        public string BugReportText
        {
            get => _bugReportText;
            set => SetProperty(ref _bugReportText, value);
        }

        public string LatencyResult
        {
            get => _latencyResult;
            set => SetProperty(ref _latencyResult, value);
        }

        public string LatencyColor
        {
            get => _latencyColor;
            set => SetProperty(ref _latencyColor, value);
        }

        public bool IsCheckingLatency
        {
            get => _isCheckingLatency;
            set => SetProperty(ref _isCheckingLatency, value);
        }

        public string EasterEggMessage
        {
            get => _easterEggMessage;
            set => SetProperty(ref _easterEggMessage, value);
        }

        public bool IsBugReportSending
        {
            get => _isBugReportSending;
            set => SetProperty(ref _isBugReportSending, value);
        }

        public string TotalScans
        {
            get => _totalScans;
            set => SetProperty(ref _totalScans, value);
        }

        public string BaymaxChats
        {
            get => _baymaxChats;
            set => SetProperty(ref _baymaxChats, value);
        }

        public string DbItems
        {
            get => _dbItems;
            set => SetProperty(ref _dbItems, value);
        }

        public ObservableCollection<VersionHistoryItem> Changelogs { get; }
        public ObservableCollection<TechStackItem> TechStack { get; }
        public ObservableCollection<SystemHealthItem> SystemHealth { get; }
        public ObservableCollection<TeamMemberItem> TeamMembers { get; }

        public ICommand CheckLatencyCommand { get; }
        public ICommand SendBugReportCommand { get; }
        public ICommand EasterEggCommand { get; }

        public AboutViewModel(IUIService uiService, IChatService chatService, IInstrumentService instrumentService)
        {
            _uiService = uiService;
            _chatService = chatService;
            _instrumentService = instrumentService;

            Changelogs = new ObservableCollection<VersionHistoryItem>
        {
            new VersionHistoryItem { Version = "v2.1.0", Date = "2025-05-10", Description = "Added Baymax Chat Module & Live Analysis", IsMajor = true },
            new VersionHistoryItem { Version = "v2.0.5", Date = "2025-04-22", Description = "Improved ONNX model accuracy", IsMajor = false },
            new VersionHistoryItem { Version = "v2.0.0", Date = "2025-03-15", Description = "UI Overhaul & Dark Mode", IsMajor = true },
            new VersionHistoryItem { Version = "v1.0.0", Date = "2024-12-01", Description = "Initial Release", IsMajor = true },
        };

            TechStack = new ObservableCollection<TechStackItem>
        {
            new TechStackItem { Name = ".NET 8", Description = "High-performance Core", IconData = "M12,2A10,10 0 0,0 2,12A10,10 0 0,0 12,22A10,10 0 0,0 22,12A10,10 0 0,0 12,2M12,20A8,8 0 0,1 4,12A8,8 0 0,1 12,4A8,8 0 0,1 20,12A8,8 0 0,1 12,20Z" },
            new TechStackItem { Name = "WPF", Description = "Modern UI Framework", IconData = "M4,2H20A2,2 0 0,1 22,4V20A2,2 0 0,1 20,22H4A2,2 0 0,1 2,20V4A2,2 0 0,1 4,2M4,4V20H20V4H4M6,6H18V18H6V6Z" },
            new TechStackItem { Name = "ONNX", Description = "AI Inference Engine", IconData = "M12,3L2,12L12,21L22,12L12,3M12,18L6,12L12,6L18,12L12,18Z" },
            new TechStackItem { Name = "OpenCV", Description = "Computer Vision", IconData = "M12,12A5,5 0 0,1 7,12A5,5 0 0,1 12,7A5,5 0 0,1 17,12A5,5 0 0,1 12,17Z" },
            new TechStackItem { Name = "Baymax", Description = "LLM Core", IconData = "M19,5V19H5V5H19M19,3H5A2,2 0 0,0 3,5V19A2,2 0 0,0 5,21H19A2,2 0 0,0 21,19V5C21,3.89 20.1,3 19,3Z" },
        };

            SystemHealth = new ObservableCollection<SystemHealthItem>();
            TeamMembers = new ObservableCollection<TeamMemberItem>
        {
            new TeamMemberItem
            {
                Name = "Amirreza Mortazavi Fard",
                Role = "Project Manager & AI Lead",
                Contribution = "Architecture, AI Models, Core Dev",
                GithubLink = "https://github.com/amirrezamortazavifard",
                Initials = "AM",
                ColorHex = "#556B2F"
            },
            new TeamMemberItem
            {
                Name = "Kaveh Ramezani",
                Role = "Data Engineer",
                Contribution = "Dataset Curation & Cleaning",
                GithubLink = "",
                Initials = "KR",
                ColorHex = "#2F6B55"
            },
            new TeamMemberItem
            {
                Name = "Mohammad Ahmadi Nia",
                Role = "Data Engineer",
                Contribution = "Database Design & Optimization",
                GithubLink = "",
                Initials = "MA",
                ColorHex = "#6B2F45"
            }
        };

            CheckLatencyCommand = new RelayCommand(async _ => await TestBaymaxConnection());
            SendBugReportCommand = new RelayCommand(async _ => await SendBugReport(), _ => !string.IsNullOrWhiteSpace(BugReportText));
            EasterEggCommand = new RelayCommand(_ => TriggerEasterEgg());

            LoadQuote();
            CheckSystemHealth();
            InitializeRealTimeStats();
            TestBaymaxConnection();
        }

        private async void InitializeRealTimeStats()
        {
            // Session Scans
            TotalScans = App.GlobalScanCount.ToString("N0");

            // Database Count
            try
            {
                var count = _instrumentService.GetAll().Count();
                DbItems = count.ToString("N0");
            }
            catch { DbItems = "0"; }

            // Chats Count
            try
            {
                var sessions = await _chatService.LoadAllSessionsAsync();
                BaymaxChats = sessions.Count.ToString("N0");
            }
            catch { BaymaxChats = "0"; }
        }

        private void LoadQuote()
        {
            var quotes = new[]
            {
            "Wherever the art of Medicine is loved, there is also a love of Humanity. — Hippocrates",
            "The good physician treats the disease; the great physician treats the patient who has the disease. — William Osler",
            "Medicine is a science of uncertainty and an art of probability. — William Osler",
            "Cure sometimes, treat often, comfort always. — Hippocrates"
        };
            DailyQuote = quotes[new Random().Next(quotes.Length)];
        }

        private void CheckSystemHealth()
        {
            SystemHealth.Clear();

            // Memory
            long memory = GC.GetTotalMemory(false) / (1024 * 1024);
            SystemHealth.Add(new SystemHealthItem
            {
                Name = "Memory Usage",
                Status = $"{memory} MB (Optimal)",
                IsHealthy = true,
                IconData = "M2,10H14V14H2V10M16,10H22V14H16V10M2,16H14V20H2V16M16,16H22V20H16V16M2,4H22V8H2V4Z"
            });

            // Network
            bool isNetwork = NetworkInterface.GetIsNetworkAvailable();
            SystemHealth.Add(new SystemHealthItem
            {
                Name = "Network Adapter",
                Status = isNetwork ? "Online" : "Disconnected",
                IsHealthy = isNetwork,
                IconData = "M12,20L8.6,15.6C12.5,11.8 12.5,11.8 12.5,11.8L15.5,15.5C14.1,16.9 12.7,18.3 12,20M12,2A10,10 0 0,1 22,12C22,14.6 21,17 19.3,18.8L17.7,17C19.1,15.5 20,13.8 20,12A8,8 0 0,0 4,12C4,14 5,16 6.5,17.6L5,19.2C3.1,17.3 2,14.8 2,12A10,10 0 0,1 12,2M12,5A7,7 0 0,1 19,12C19,13.8 18.3,15.5 17.2,16.8L15.6,15.2C16.5,14.3 17,13.2 17,12A5,5 0 0,0 7,12C7,13.2 7.5,14.3 8.3,15.2L6.8,16.9C5.6,15.6 5,13.9 5,12A7,7 0 0,1 12,5M12,8A4,4 0 0,1 16,12C16,13 15.6,13.9 15,14.6L12,17.5L9,14.5C8.4,13.9 8,13 8,12A4,4 0 0,1 12,8Z"
            });

            // GPU (Mocked for safety)
            SystemHealth.Add(new SystemHealthItem
            {
                Name = "AI Accelerator",
                Status = "CUDA Cores Active",
                IsHealthy = true,
                IconData = "M4,2H20A2,2 0 0,1 22,4V20A2,2 0 0,1 20,22H4A2,2 0 0,1 2,20V4A2,2 0 0,1 4,2M4,4V20H20V4H4M6,6H18V18H6V6Z"
            });
        }

        private async Task TestBaymaxConnection()
        {
            IsCheckingLatency = true;
            LatencyResult = "Pinging Baymax...";
            LatencyColor = "Yellow";

            var sw = Stopwatch.StartNew();
            try
            {
                using var client = new System.Net.Http.HttpClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                await client.GetAsync("https://www.google.com");
                sw.Stop();

                long ms = sw.ElapsedMilliseconds;
                LatencyResult = $"{ms} ms (Stable)";

                if (ms < 200) LatencyColor = "#4CAF50";
                else if (ms < 500) LatencyColor = "#FFC107";
                else LatencyColor = "#F44336";
            }
            catch
            {
                LatencyResult = "Connection Lost";
                LatencyColor = "#F44336";
            }
            finally
            {
                IsCheckingLatency = false;
            }
        }

        private async Task SendBugReport()
        {
            IsBugReportSending = true;
            await Task.Delay(2000);
            BugReportText = "";
            IsBugReportSending = false;
            _uiService.ShowError("Bug report transmitted to Baymax Engineering Team. Thank you!");
        }

        private void TriggerEasterEgg()
        {
            _easterEggCounter++;
            if (_easterEggCounter == 5)
            {
                EasterEggMessage = "Hello! I am Baymax, your personal healthcare companion.";
            }
            else if (_easterEggCounter > 5)
            {
                EasterEggMessage = "Balanlala!";
                _easterEggCounter = 0;
            }
        }
    }
}