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
        public Task<DesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new DesktopProcessSnapshot(false, null));

        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ReopenAsync(DesktopProcessSnapshot snapshot, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }

    private sealed class FakeSessions : IDesktopSessionService
    {
        public List<string> Events { get; } = [];

        public Task BackupAsync(AccountProfile profile, CancellationToken cancellationToken = default)
        {
            Events.Add($"backup:{profile.Id}");
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
