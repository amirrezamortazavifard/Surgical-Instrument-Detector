using SurgicalDetector.Models;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
namespace SurgicalDetector.Services
{
    public class ChatService : IChatService
    {
        private readonly string _storagePath;
        private readonly HttpClient _httpClient;
      
    public ChatService()
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _storagePath = !string.IsNullOrWhiteSpace(localAppData)
                ? Path.Combine(localAppData, "SurgicalDetector", "Chats")
                : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Chats");

            try
            {
                var legacyPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Chats");
                if (Directory.Exists(legacyPath) && !string.Equals(legacyPath, _storagePath, StringComparison.OrdinalIgnoreCase))
                {
                    Directory.CreateDirectory(_storagePath);
                    foreach (var file in Directory.GetFiles(legacyPath, "*.json"))
                    {
                        var dest = Path.Combine(_storagePath, Path.GetFileName(file));
                        if (!File.Exists(dest)) File.Copy(file, dest);
                    }
                }

                if (!Directory.Exists(_storagePath))
                {
                    Directory.CreateDirectory(_storagePath);
                }
            }
            catch
            {
                // Fall back gracefully to base directory if AppData is not writable
                _storagePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "Chats");
                if (!Directory.Exists(_storagePath)) Directory.CreateDirectory(_storagePath);
            }

            _httpClient = new HttpClient();
        }

        public async Task<List<ChatSession>> LoadAllSessionsAsync()
        {
            var sessions = new List<ChatSession>();
            var files = Directory.GetFiles(_storagePath, "*.json");

            foreach (var file in files)
            {
                try
                {
                    var json = await File.ReadAllTextAsync(file);
                    var session = JsonSerializer.Deserialize<ChatSession>(json);
                    if (session != null)
                    {
                        sessions.Add(session);
                    }
                }
                catch { }
            }

            return sessions.OrderByDescending(s => s.LastUpdated).ToList();
        }

        public async Task SaveSessionAsync(ChatSession session)
        {
            var filePath = Path.Combine(_storagePath, $"{session.Id}.json");
            var json = JsonSerializer.Serialize(session);
            await File.WriteAllTextAsync(filePath, json);
        }

        public async Task DeleteSessionAsync(string sessionId)
        {
            var filePath = Path.Combine(_storagePath, $"{sessionId}.json");
            if (File.Exists(filePath))
            {
                await Task.Run(() => File.Delete(filePath));
            }
        }

        public async Task<string> GetAiResponseAsync(List<ChatMessage> history, string newMessage, string imageBase64 = null)
        {
            if (string.IsNullOrWhiteSpace(AppConfig.ApiKey))
            {
                return "Baymax: Gemini API key is not configured. Please configure your API key in Settings or set the GEMINI_API_KEY environment variable.";
            }

            try
            {
                var contents = new List<object>();

                var systemInstruction = "You are Baymax, a healthcare companion. Be helpful, precise, and caring. If an image is provided, analyze it surgically.";

                // Sliding window: last 10 messages to save tokens
                var recentHistory = history.TakeLast(10).ToList();

                foreach (var msg in recentHistory)
                {
                    contents.Add(new { role = msg.Role, parts = new[] { new { text = msg.Content } } });
                }

                var currentParts = new List<object>();
                currentParts.Add(new { text = newMessage });

                if (!string.IsNullOrEmpty(imageBase64))
                {
                    currentParts.Add(new { inline_data = new { mime_type = "image/jpeg", data = imageBase64 } });
                }

                contents.Add(new { role = "user", parts = currentParts.ToArray() });

                var requestBody = new
                {
                    system_instruction = new { parts = new { text = systemInstruction } },
                    contents = contents.ToArray()
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{AppConfig.BaseUrl}?key={AppConfig.ApiKey}", content);

                if (!response.IsSuccessStatusCode) return "Baymax: I am having trouble connecting to the network.";

                var responseString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                using var doc = JsonDocument.Parse(responseString);

                var text = doc.RootElement
                    .GetProperty("candidates")[0]
                    .GetProperty("content")
                    .GetProperty("parts")[0]
                    .GetProperty("text")
                    .GetString();

                return text;
            }
            catch
            {
                return "Baymax: I apologize, I cannot process your request at the moment.";
            }
        }

        public async Task<string> GenerateTitleAsync(string firstMessage)
        {
            try
            {
                var prompt = $"Summarize this text into a very short title (max 4 words) for a chat history: '{firstMessage}'";
                var requestBody = new
                {
                    contents = new[] { new { parts = new[] { new { text = prompt } } } }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{AppConfig.BaseUrl}?key={AppConfig.ApiKey}", content);

                var responseString = await response.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(responseString);
                return doc.RootElement.GetProperty("candidates")[0].GetProperty("content").GetProperty("parts")[0].GetProperty("text").GetString().Trim();
            }
            catch
            {
                return "New Chat";
            }
        }
    }
}