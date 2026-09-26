using ChatGPTRoster.Services;
using System.Diagnostics;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class DesktopProcessServiceTests
{
    [TestMethod]
    public async Task TerminateDesktopProcess_PreservesIndependentChildProcess()
    {
        var start = new ProcessStartInfo("powershell.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true
        };
        start.ArgumentList.Add("-NoProfile");
        start.ArgumentList.Add("-Command");
        start.ArgumentList.Add("$child = Start-Process cmd.exe -ArgumentList '/c ping -n 60 127.0.0.1 >nul' -WindowStyle Hidden -PassThru; Write-Output $child.Id; Start-Sleep -Seconds 60");
        using var parent = Process.Start(start)!;
        Process? child = null;
        try
        {
            var childId = await parent.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(15));
            child = Process.GetProcessById(int.Parse(childId!));
            DesktopProcessService.TerminateDesktopProcess(parent);
            await parent.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await Task.Delay(300);
            Assert.IsFalse(child.HasExited, "Closing the desktop must not kill a companion launched beneath it.");
        }
        finally
        {
            if (!parent.HasExited) parent.Kill(entireProcessTree: true);
            if (child is not null)
            {
                if (!child.HasExited) child.Kill(entireProcessTree: true);
                child.Dispose();
            }
        }
    }

    [TestMethod]
    public void LauncherValidation_RejectsBundledCli()
    {
        Assert.IsFalse(DesktopProcessService.IsLauncherPath(
            @"C:\Program Files\WindowsApps\OpenAI.Codex_1.2.3_x64__publisher\app\resources\codex.exe"));
    }

    [TestMethod]
    public async Task Reopen_UsesPackageIdentityEvenWhenCapturedExecutableIsGone()
    {
        string? activated = null;
        var service = new DesktopProcessService(id => activated = id, _ => throw new AssertFailedException("Unexpected executable launch"),
            () => Task.FromResult(true), _ => false, TimeSpan.Zero);

        await service.ReopenAsync(new DesktopProcessSnapshot(true, "removed-by-update.exe", "OpenAI.Codex_publisher!App"));

        Assert.AreEqual("OpenAI.Codex_publisher!App", activated);
    }

    [TestMethod]
    public async Task Reopen_DoesNotReportSuccessWhenNoDesktopWindowAppears()
    {
        var service = new DesktopProcessService(_ => { }, _ => { }, () => Task.FromResult(false), _ => true, TimeSpan.Zero);
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            service.ReopenAsync(new DesktopProcessSnapshot(true, null, "OpenAI.Codex_publisher!App")));
    }

    [TestMethod]
    public async Task Reopen_MissingLauncherReportsFailure()
    {
        var service = new DesktopProcessService();
        await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
            service.ReopenAsync(new DesktopProcessSnapshot(true, null)));
    }

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
