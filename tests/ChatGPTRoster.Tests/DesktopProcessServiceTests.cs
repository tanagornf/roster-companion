using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class DesktopProcessServiceTests
{
    [TestMethod]
    public void PackagedPathValidation_AcceptsOnlyVerifiedCodexPackageExecutables()
    {
        Assert.IsTrue(DesktopProcessService.IsPackagedDesktopPath(
            @"C:\Program Files\WindowsApps\OpenAI.Codex_1.2.3_x64__publisher\app\ChatGPT.exe"));
        Assert.IsTrue(DesktopProcessService.IsPackagedDesktopPath(
            @"C:\Program Files\WindowsApps\OpenAI.Codex_1.2.3_x64__publisher\app\resources\codex.exe"));

        Assert.IsFalse(DesktopProcessService.IsPackagedDesktopPath(@"C:\Tools\codex.exe"));
        Assert.IsFalse(DesktopProcessService.IsPackagedDesktopPath(
            @"C:\Program Files\WindowsApps\Other.App_1.0\app\ChatGPT.exe"));
        Assert.IsFalse(DesktopProcessService.IsPackagedDesktopPath(
            @"C:\Program Files\WindowsApps\OpenAI.Codex_1.2.3\app\helper.exe"));
    }
}
