using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class WindowsAccountSwitcherTests
{
    [TestMethod]
    public async Task Switch_PostActivationFailureRollsBackAndReopensDesktop()
    {
        var process = new FakeDesktopProcessService(wasRunning: true);
        var sessions = new FakeDesktopSessionService { FailTargetRestore = true };
        var credentials = new FakeCredentialSwitcher();
        var service = new WindowsAccountSwitcher(process, sessions, credentials);
        var current = new AccountProfile { Id = "current" };
        var target = new AccountProfile { Id = "target" };

        var exception = await Assert.ThrowsExceptionAsync<AccountSwitchException>(
            () => service.SwitchAsync(current, target));

        Assert.IsFalse(exception.AccountChanged);
        Assert.IsTrue(credentials.Activated);
        Assert.IsTrue(credentials.RolledBack);
        CollectionAssert.AreEqual(
            new[] { "backup:current", "restore:target", "restore:current" },
            sessions.Events);
        Assert.IsTrue(process.Closed);
        Assert.IsTrue(process.Reopened);
    }

    [TestMethod]
    public async Task Switch_ReopenFailureReportsThatAccountChanged()
    {
        var process = new FakeDesktopProcessService(wasRunning: true) { FailReopen = true };
        var sessions = new FakeDesktopSessionService();
        var credentials = new FakeCredentialSwitcher();
        var service = new WindowsAccountSwitcher(process, sessions, credentials);

        var exception = await Assert.ThrowsExceptionAsync<AccountSwitchException>(
            () => service.SwitchAsync(
                new AccountProfile { Id = "current" },
                new AccountProfile { Id = "target" }));

        Assert.IsTrue(exception.AccountChanged);
        Assert.IsTrue(credentials.Activated);
        Assert.IsFalse(credentials.RolledBack);
        StringAssert.Contains(exception.Message, "changed successfully");
    }

    private sealed class FakeDesktopProcessService : IDesktopProcessService
    {
        private readonly bool _wasRunning;

        public FakeDesktopProcessService(bool wasRunning)
        {
            _wasRunning = wasRunning;
        }

        public bool Closed { get; private set; }
        public bool Reopened { get; private set; }
        public bool FailReopen { get; init; }

        public Task<DesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DesktopProcessSnapshot(_wasRunning, @"C:\verified\Codex.exe"));

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            Closed = true;
            return Task.CompletedTask;
        }

        public Task ReopenAsync(DesktopProcessSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            Reopened = true;
            return FailReopen
                ? Task.FromException(new InvalidOperationException("reopen failed"))
                : Task.CompletedTask;
        }
    }

    private sealed class FakeDesktopSessionService : IDesktopSessionService
    {
        public List<string> Events { get; } = [];
        public bool FailTargetRestore { get; init; }

        public Task BackupAsync(AccountProfile profile, CancellationToken cancellationToken = default)
        {
            Events.Add($"backup:{profile.Id}");
            return Task.CompletedTask;
        }

        public Task RestoreOrClearAsync(AccountProfile profile, CancellationToken cancellationToken = default)
        {
            Events.Add($"restore:{profile.Id}");
            if (FailTargetRestore && profile.Id == "target")
            {
                throw new IOException("restore failed");
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialSwitcher : ICredentialSwitcher
    {
        public bool Activated { get; private set; }
        public bool RolledBack { get; private set; }

        public Task<CredentialSwitchReceipt> ActivateAsync(
            AccountProfile currentProfile,
            AccountProfile targetProfile,
            CancellationToken cancellationToken = default)
        {
            Activated = true;
            return Task.FromResult(new CredentialSwitchReceipt("backup", "ambient", true));
        }

        public Task RollbackAsync(
            CredentialSwitchReceipt receipt,
            CancellationToken cancellationToken = default)
        {
            RolledBack = true;
            return Task.CompletedTask;
        }
    }
}
