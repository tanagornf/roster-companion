using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class DesktopSessionServiceTests
{
    private string _root = null!;
    private string _localAppData = null!;
    private string _browserRoot = null!;
    private string _profilePath = null!;

    [TestInitialize]
    public void Initialize()
    {
        _root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.SessionTests", Guid.NewGuid().ToString("N"));
        _localAppData = Path.Combine(_root, "Local");
        _browserRoot = Path.Combine(
            _localAppData,
            "Packages",
            "OpenAI.Codex_test",
            "LocalCache",
            "Roaming",
            "Codex",
            "web",
            "Codex");
        _profilePath = Path.Combine(_root, "Profiles", "account-1");
        Directory.CreateDirectory(Path.Combine(_browserRoot, "Default", "Network"));
        Directory.CreateDirectory(_profilePath);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_root, recursive: true);
    }

    [TestMethod]
    public async Task BackupAndRestore_PreservesModernSessionEntries()
    {
        var liveCookie = Path.Combine(_browserRoot, "Default", "Network", "Cookies");
        await File.WriteAllTextAsync(liveCookie, "account-one-session");
        var profile = new AccountProfile { ProfilePath = _profilePath };
        var service = new DesktopSessionService(_localAppData);

        await service.BackupAsync(profile);
        await File.WriteAllTextAsync(liveCookie, "different-session");
        await service.RestoreOrClearAsync(profile);

        Assert.AreEqual("account-one-session", await File.ReadAllTextAsync(liveCookie));
        Assert.IsTrue(File.Exists(Path.Combine(_profilePath, "desktop-session", "adapter.json")));
    }

    [TestMethod]
    public async Task RestoreOrClear_ClearsStaleEntriesForFirstUseProfile()
    {
        var liveCookie = Path.Combine(_browserRoot, "Default", "Network", "Cookies");
        await File.WriteAllTextAsync(liveCookie, "stale-session");
        var newProfilePath = Path.Combine(_root, "Profiles", "account-2");
        Directory.CreateDirectory(newProfilePath);
        var service = new DesktopSessionService(_localAppData);

        await service.RestoreOrClearAsync(new AccountProfile { ProfilePath = newProfilePath });

        Assert.IsFalse(Directory.Exists(Path.Combine(_browserRoot, "Default", "Network")));
    }
}
