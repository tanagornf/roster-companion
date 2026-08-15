using System.Text.Json;
using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class JsonProfileStoreTests
{
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.Tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        if (Directory.Exists(_temporaryDirectory))
        {
            Directory.Delete(_temporaryDirectory, recursive: true);
        }
    }

    [TestMethod]
    public async Task SaveAndLoad_PreservesProfileMetadata()
    {
        var paths = new AppPaths(_temporaryDirectory);
        var store = new JsonProfileStore(paths);
        var expected = new AccountProfile
        {
            AccountId = "account-123",
            Email = "person@example.com",
            Alias = "Personal",
            Plan = "Plus",
            IsActive = true,
            Usage = new UsageSnapshot
            {
                ShortTerm = new UsageWindow
                {
                    Label = "5-hour",
                    RemainingPercent = 72,
                    ResetsAt = DateTimeOffset.UtcNow.AddHours(2)
                }
            }
        };

        await store.SaveAsync([expected]);
        var actual = await store.LoadAsync();

        Assert.AreEqual(1, actual.Count);
        Assert.AreEqual(expected.AccountId, actual[0].AccountId);
        Assert.AreEqual("Personal", actual[0].Alias);
        Assert.AreEqual(72, actual[0].Usage.ShortTerm?.RemainingPercent);
    }

    [TestMethod]
    public async Task Save_DoesNotPersistComputedDisplayName()
    {
        var paths = new AppPaths(_temporaryDirectory);
        var store = new JsonProfileStore(paths);

        await store.SaveAsync([new AccountProfile { Email = "person@example.com" }]);
        var json = await File.ReadAllTextAsync(paths.ProfilesFile);
        using var document = JsonDocument.Parse(json);

        Assert.IsFalse(document.RootElement[0].TryGetProperty("displayName", out _));
    }

    [TestMethod]
    public async Task SecondSave_CreatesRecoverableMetadataBackup()
    {
        var paths = new AppPaths(_temporaryDirectory);
        var store = new JsonProfileStore(paths);

        await store.SaveAsync([new AccountProfile { Email = "first@example.com" }]);
        await store.SaveAsync([new AccountProfile { Email = "second@example.com" }]);

        Assert.IsTrue(File.Exists(paths.ProfilesFile + ".bak"));
        var backupJson = await File.ReadAllTextAsync(paths.ProfilesFile + ".bak");
        StringAssert.Contains(backupJson, "first@example.com");
    }
}
