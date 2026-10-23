using System.Threading.Tasks;
namespace SurgicalDetector.Services
{
    public interface IGenAIService
    {
        Task<string> GetClinicalAnalysisAsync(string instrumentName, string category, string lang);
    }
}