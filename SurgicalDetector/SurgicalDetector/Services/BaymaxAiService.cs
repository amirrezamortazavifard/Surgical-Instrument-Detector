using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
namespace SurgicalDetector.Services
{
    public class BaymaxAiService : IGenAIService
    {
        private readonly HttpClient _httpClient;
        
    public BaymaxAiService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<string> GetClinicalAnalysisAsync(string instrumentName, string category, string lang)
        {
            try
            {
                var systemPrompt = AppConfig.GetBaymaxPrompt(instrumentName, category, lang);

                var requestBody = new
                {
                    contents = new[]
                    {
                    new
                    {
                        parts = new[]
                        {
                            new { text = systemPrompt }
                        }
                    }
                }
                };

                var jsonContent = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{AppConfig.BaseUrl}?key={AppConfig.ApiKey}", content);

                if (!response.IsSuccessStatusCode)
                {
                    return $"Baymax Alert: I cannot connect to my knowledge base right now. (Error Code: {response.StatusCode})";
                }

                var responseString = await response.Content.ReadAsStringAsync();

                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var result = JsonSerializer.Deserialize<GeminiResponse>(responseString, options);

                var text = result?.Candidates?[0]?.Content?.Parts?[0]?.Text;
                return !string.IsNullOrEmpty(text) ? text : "Baymax Alert: My scan is complete, but I have no data on this item.";
            }
            catch (Exception)
            {
                return "Baymax Alert: Connection lost. Are you satisfied with your care?";
            }
        }

        private class GeminiResponse
        {
            public Candidate[] Candidates { get; set; }
        }

        private class Candidate
        {
            public Content Content { get; set; }
        }

        private class Content
        {
            public Part[] Parts { get; set; }
        }

        private class Part
        {
            public string Text { get; set; }
        }
    }
}