using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class AppPathsTests
{
    [TestMethod]
    public void Constructor_UsesExplicitPathsInsteadOfEnvironmentOverrides()
    {
        var explicitData = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "data");
        var explicitCodex = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "codex");
        var previousData = Environment.GetEnvironmentVariable("CHATGPT_ROSTER_DATA_ROOT");
        var previousCodex = Environment.GetEnvironmentVariable("CHATGPT_ROSTER_CODEX_HOME");

        try
        {
            Environment.SetEnvironmentVariable("CHATGPT_ROSTER_DATA_ROOT", "ignored-data");
            Environment.SetEnvironmentVariable("CHATGPT_ROSTER_CODEX_HOME", "ignored-codex");

            var paths = new AppPaths(explicitData, explicitCodex);

            Assert.AreEqual(Path.GetFullPath(explicitData), paths.DataRoot);
            Assert.AreEqual(Path.GetFullPath(explicitCodex), paths.AmbientCodexHome);
        }
        finally
        {
            Environment.SetEnvironmentVariable("CHATGPT_ROSTER_DATA_ROOT", previousData);
            Environment.SetEnvironmentVariable("CHATGPT_ROSTER_CODEX_HOME", previousCodex);
        }
    }
}
