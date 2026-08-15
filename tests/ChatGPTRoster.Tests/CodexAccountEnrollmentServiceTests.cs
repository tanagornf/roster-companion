using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class CodexAccountEnrollmentServiceTests
{
    [TestMethod]
    public async Task Enroll_InvalidLoginResultRemovesIncompleteManagedProfile()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.EnrollmentTests", Guid.NewGuid().ToString("N"));
        var paths = new AppPaths(Path.Combine(root, "data"), Path.Combine(root, "codex"));
        var commandPrompt = Path.Combine(Environment.SystemDirectory, "cmd.exe");
        var service = new CodexAccountEnrollmentService(
            paths,
            new FixedCliLocator(commandPrompt),
            new UnexpectedAuthReader());

        try
        {
            await Assert.ThrowsExceptionAsync<AuthFileException>(() => service.EnrollAsync());

            Assert.IsTrue(Directory.Exists(paths.ProfilesDirectory));
            Assert.AreEqual(0, Directory.EnumerateFileSystemEntries(paths.ProfilesDirectory).Count());
        }
        finally
        {
            if (Directory.Exists(root))
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }

    private sealed class FixedCliLocator(string executable) : ICodexCliLocator
    {
        public string? FindExecutable() => executable;
    }

    private sealed class UnexpectedAuthReader : IAuthFileReader
    {
        public Task<AuthIdentity> ReadIdentityAsync(
            string profilePath,
            CancellationToken cancellationToken = default) =>
            throw new AuthFileException("Synthetic login did not produce credentials.");

        public Task<LocalCredentials> ReadCredentialsAsync(
            string profilePath,
            CancellationToken cancellationToken = default) =>
            throw new AssertFailedException("Credential parsing should not run after a failed login process.");
    }
}
