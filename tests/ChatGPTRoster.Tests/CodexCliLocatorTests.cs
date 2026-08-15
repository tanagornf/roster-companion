using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class CodexCliLocatorTests
{
    [TestMethod]
    public void FindExecutable_DesktopEnrollmentOverrideIgnoresInstalledCli()
    {
        var root = Path.Combine(Path.GetTempPath(), "RosterCompanion.Cli", Guid.NewGuid().ToString("N"));
        var executable = Path.Combine(root, ".bun", "bin", "codex.exe");
        var previous = Environment.GetEnvironmentVariable(CodexCliLocator.ForceDesktopEnrollmentVariable);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(executable)!);
            File.WriteAllText(executable, string.Empty);
            Environment.SetEnvironmentVariable(CodexCliLocator.ForceDesktopEnrollmentVariable, "1");

            var locator = new CodexCliLocator(root, root, pathValue: string.Empty);

            Assert.IsNull(locator.FindExecutable());
        }
        finally
        {
            Environment.SetEnvironmentVariable(CodexCliLocator.ForceDesktopEnrollmentVariable, previous);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void FindExecutable_IncludesOfficialWindowsInstallLocation()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.CliTests", Guid.NewGuid().ToString("N"));
        var expected = Path.Combine(root, "Programs", "OpenAI", "Codex", "bin", "codex.exe");
        var previous = Environment.GetEnvironmentVariable(CodexCliLocator.ForceDesktopEnrollmentVariable);
        Directory.CreateDirectory(Path.GetDirectoryName(expected)!);
        File.WriteAllText(expected, string.Empty);

        try
        {
            Environment.SetEnvironmentVariable(CodexCliLocator.ForceDesktopEnrollmentVariable, null);
            var locator = new CodexCliLocator(root, root, pathValue: string.Empty);

            Assert.AreEqual(Path.GetFullPath(expected), locator.FindExecutable());
        }
        finally
        {
            Environment.SetEnvironmentVariable(CodexCliLocator.ForceDesktopEnrollmentVariable, previous);
            Directory.Delete(root, recursive: true);
        }
    }
}
