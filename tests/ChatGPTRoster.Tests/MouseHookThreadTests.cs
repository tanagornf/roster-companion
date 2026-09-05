using System.Windows.Threading;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class MouseHookThreadTests
{
    [TestMethod]
    public async Task Start_PumpsOnSeparateThread_AndUninstallsOnThatThread()
    {
        var ownerThread = Environment.CurrentManagedThreadId;
        var installedThread = 0;
        var uninstalledThread = 0;
        Dispatcher? dispatcher = null;
        using var hook = new MouseHookThread(() =>
        {
            installedThread = Environment.CurrentManagedThreadId;
            dispatcher = Dispatcher.CurrentDispatcher;
        }, () => uninstalledThread = Environment.CurrentManagedThreadId);

        hook.Start();
        hook.Start(); // Starting twice must not install another hook.
        Assert.AreNotEqual(ownerThread, installedThread);
        Assert.IsNotNull(dispatcher);
        var delivered = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        _ = dispatcher.BeginInvoke(() => delivered.SetResult(Environment.CurrentManagedThreadId));
        Assert.AreEqual(installedThread, await delivered.Task.WaitAsync(TimeSpan.FromSeconds(5)));
        hook.Stop();
        Assert.AreEqual(installedThread, uninstalledThread);
    }

    [TestMethod]
    public void StopThenStart_CreatesFreshPumpAndBalancesCleanup()
    {
        var installs = 0;
        var removals = 0;
        using var hook = new MouseHookThread(
            () => Interlocked.Increment(ref installs),
            () => Interlocked.Increment(ref removals));
        hook.Start();
        hook.Stop();
        hook.Stop();
        hook.Start();
        hook.Stop();
        Assert.AreEqual(2, installs);
        Assert.AreEqual(2, removals);
    }

    [TestMethod]
    public void InstallFailure_IsReportedToCaller()
    {
        using var hook = new MouseHookThread(
            () => throw new InvalidOperationException("Install failed"), () => { });
        Assert.ThrowsException<InvalidOperationException>(() => hook.Start());
    }
}
