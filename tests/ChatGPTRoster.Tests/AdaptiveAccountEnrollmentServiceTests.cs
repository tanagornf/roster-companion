using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class AdaptiveAccountEnrollmentServiceTests
{
    [TestMethod]
    public async Task Enroll_NoCliAndDeclinedDesktopFlowDoesNotChangeFiles()
    {
        var root = CreateRoot();
        try
        {
            var paths = PreparePaths(root);
            var service = CreateService(paths, confirm: false, new FakeAuthReader());
            var current = CurrentProfile(paths);

            await Assert.ThrowsExceptionAsync<OperationCanceledException>(() => service.EnrollAsync(current));

            Assert.IsTrue(File.Exists(Path.Combine(paths.AmbientCodexHome, "auth.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Enroll_SessionBackupFailurePreservesLiveSessionAndReopensDesktop()
    {
        var root = CreateRoot();
        try
        {
            var paths = PreparePaths(root);
            var processes = new FakeProcesses { IsRunning = true };
            var sessions = new FakeSessions { FailBackup = true };
            var service = new AdaptiveAccountEnrollmentService(
                paths, new MissingCli(), new FakeAuthReader(), processes, sessions, _ => true);

            var exception = await Assert.ThrowsExceptionAsync<AccountEnrollmentException>(
                () => service.EnrollAsync(CurrentProfile(paths)));

            Assert.AreEqual("previous-credential", File.ReadAllText(Path.Combine(paths.AmbientCodexHome, "auth.json")));
            CollectionAssert.AreEqual(new[] { "backup:current" }, sessions.Events);
            Assert.AreEqual(1, processes.CloseCount);
            Assert.AreEqual(1, processes.ReopenCount);
            Assert.IsInstanceOfType<IOException>(exception.InnerException);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Enroll_PartialCloseFailureReopensWithoutAttemptingCredentialRecovery()
    {
        var root = CreateRoot();
        try
        {
            var paths = PreparePaths(root);
            var processes = new FakeProcesses { IsRunning = true, FailClose = true };
            var sessions = new FakeSessions();
            var service = new AdaptiveAccountEnrollmentService(
                paths, new MissingCli(), new FakeAuthReader(), processes, sessions, _ => true);

            var exception = await Assert.ThrowsExceptionAsync<AccountEnrollmentException>(
                () => service.EnrollAsync(CurrentProfile(paths)));

            Assert.AreEqual("previous-credential", File.ReadAllText(Path.Combine(paths.AmbientCodexHome, "auth.json")));
            Assert.AreEqual(0, sessions.Events.Count);
            Assert.AreEqual(1, processes.CloseCount);
            Assert.AreEqual(1, processes.ReopenCount);
            Assert.IsInstanceOfType<IOException>(exception.InnerException);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Enroll_DesktopImportFailureRestoresPreviousCredentialAndSession()
    {
        var root = CreateRoot();
        try
        {
            var paths = PreparePaths(root);
            var sessions = new FakeSessions();
            var service = new AdaptiveAccountEnrollmentService(
                paths,
                new MissingCli(),
                new FakeAuthReader(),
                new FakeProcesses(),
                sessions,
                _ => true);
            var current = CurrentProfile(paths);

            await Assert.ThrowsExceptionAsync<AccountEnrollmentException>(() => service.EnrollAsync(current));

            Assert.IsTrue(File.Exists(Path.Combine(paths.AmbientCodexHome, "auth.json")));
            CollectionAssert.Contains(sessions.Events, "restore:current");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static AdaptiveAccountEnrollmentService CreateService(
        AppPaths paths,
        bool confirm,
        IAuthFileReader reader) =>
        new(paths, new MissingCli(), reader, new FakeProcesses(), new FakeSessions(), _ => confirm);

    private static string CreateRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "RosterCompanion.Enrollment", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        return root;
    }

    private static AppPaths PreparePaths(string root)
    {
        var paths = new AppPaths(Path.Combine(root, "data"), Path.Combine(root, "codex"));
        Directory.CreateDirectory(paths.AmbientCodexHome);
        File.WriteAllText(Path.Combine(paths.AmbientCodexHome, "auth.json"), "previous-credential");
        return paths;
    }

    private static AccountProfile CurrentProfile(AppPaths paths)
    {
        var profile = new AccountProfile
        {
            Id = "current",
            AccountId = "current-account",
            Subject = "current-subject",
            Email = "current@example.com",
            ProfilePath = Path.Combine(paths.ProfilesDirectory, "current"),
            IsActive = true
        };
        Directory.CreateDirectory(profile.ProfilePath);
        return profile;
    }

    private sealed class MissingCli : ICodexCliLocator
    {
        public string? FindExecutable() => null;
    }

    private sealed class FakeProcesses : IDesktopProcessService
    {
        public bool IsRunning { get; set; }
        public bool FailClose { get; init; }
        public int CloseCount { get; private set; }
        public int ReopenCount { get; private set; }

        public Task<DesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DesktopProcessSnapshot(IsRunning, null));

        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCount++;
            IsRunning = false;
            if (FailClose) throw new IOException("Shutdown failed after closing the window.");
            return Task.CompletedTask;
        }

        public Task ReopenAsync(DesktopProcessSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            ReopenCount++;
            IsRunning = true;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSessions : IDesktopSessionService
    {
        public List<string> Events { get; } = [];
        public bool FailBackup { get; init; }

        public Task BackupAsync(AccountProfile profile, CancellationToken cancellationToken = default)
        {
            Events.Add($"backup:{profile.Id}");
            if (FailBackup) throw new IOException("The live session could not be backed up.");
            return Task.CompletedTask;
        }

        public Task RestoreOrClearAsync(AccountProfile profile, CancellationToken cancellationToken = default)
        {
            Events.Add($"restore:{profile.Id}");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthReader : IAuthFileReader
    {
        public Task<AuthIdentity> ReadIdentityAsync(string profilePath, CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuthIdentity("new@example.com", "new-account", "new-subject", "plus"));

        public Task<LocalCredentials> ReadCredentialsAsync(string profilePath, CancellationToken cancellationToken = default) =>
            throw new NotImplementedException();
    }
}
