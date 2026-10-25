using SurgicalDetector.Infrastructure;
using SurgicalDetector.Models;
using SurgicalDetector.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class SecurityConfigurationTests
    {
        [Fact]
        public void ConfigurationService_WhenNoKeyConfigured_HasGeminiApiKeyIsFalse()
        {
            var config = new ConfigurationService();
            config.GeminiApiKey = string.Empty;

            Assert.False(config.HasGeminiApiKey);
            Assert.Equal(string.Empty, config.GeminiApiKey);
        }

        [Fact]
        public void ConfigurationService_ReadsFromEnvironmentVariable()
        {
            const string testKeyName = "TEST_GEMINI_KEY_DUMMY";
            var originalEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", testKeyName);

                var config = new ConfigurationService();
                config.GeminiApiKey = string.Empty; // clear model key to test env fallback

                Assert.Equal(testKeyName, config.GeminiApiKey);
                Assert.True(config.HasGeminiApiKey);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalEnv);
            }
        }

        [Fact]
        public async Task BaymaxAiService_WhenKeyMissing_ReturnsSafeConfigurationPrompt()
        {
            var originalKey = ConfigurationService.Instance.GeminiApiKey;
            var originalEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
                ConfigurationService.Instance.GeminiApiKey = string.Empty;

                var baymax = new BaymaxAiService();
                var responseEnglish = await baymax.GetClinicalAnalysisAsync("Scalpel", "Cutting", "en");
                var responsePersian = await baymax.GetClinicalAnalysisAsync("Scalpel", "Cutting", "fa");

                Assert.Contains("Gemini API key is not configured", responseEnglish);
                Assert.Contains("تنظیم نشده", responsePersian);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalEnv);
                ConfigurationService.Instance.GeminiApiKey = originalKey;
            }
        }

        [Fact]
        public async Task ChatService_WhenKeyMissing_ReturnsSafeConfigurationPrompt()
        {
            var originalKey = ConfigurationService.Instance.GeminiApiKey;
            var originalEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
                ConfigurationService.Instance.GeminiApiKey = string.Empty;

                var chat = new ChatService();
                var response = await chat.GetAiResponseAsync(new List<ChatMessage>(), "Hello");

                Assert.Contains("Gemini API key is not configured", response);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalEnv);
                ConfigurationService.Instance.GeminiApiKey = originalKey;
            }
        }

        [Fact]
        public async Task ChatService_GenerateTitle_WhenKeyMissing_DoesNotThrow()
        {
            var originalKey = ConfigurationService.Instance.GeminiApiKey;
            var originalEnv = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

            try
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", null);
                ConfigurationService.Instance.GeminiApiKey = string.Empty;

                var chat = new ChatService();
                var title = await chat.GenerateTitleAsync("How to use scalpel?");

                Assert.False(string.IsNullOrWhiteSpace(title));
                Assert.DoesNotContain("AIza", title);
            }
            finally
            {
                Environment.SetEnvironmentVariable("GEMINI_API_KEY", originalEnv);
                ConfigurationService.Instance.GeminiApiKey = originalKey;
            }
        }
    }
}
