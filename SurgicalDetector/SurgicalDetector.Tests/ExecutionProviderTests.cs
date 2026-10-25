using Microsoft.ML.OnnxRuntime;
using Xunit;

namespace SurgicalDetector.Tests
{
    public class ExecutionProviderTests
    {
        [Fact]
        public void OrtEnv_ReportsAvailableProviders()
        {
            var providers = OrtEnv.Instance().GetAvailableProviders();
            Assert.NotNull(providers);
            Assert.Contains("CPUExecutionProvider", providers);
        }
    }
}
