using SurgicalDetector.Models;
using System.Collections.Generic;
using System.Threading.Tasks;
namespace SurgicalDetector.Services
{
    public interface IChatService
    {
        Task<List<ChatSession>> LoadAllSessionsAsync();
        Task SaveSessionAsync(ChatSession session);
        Task DeleteSessionAsync(string sessionId);
        Task<string> GetAiResponseAsync(List<ChatMessage> history, string newMessage, string imageBase64 = null);
        Task<string> GenerateTitleAsync(string firstMessage);
    }
}