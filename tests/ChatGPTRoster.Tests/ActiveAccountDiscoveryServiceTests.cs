using System.Text;
using System.Text.Json;
using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class ActiveAccountDiscoveryServiceTests
{
    [TestMethod]
    public async Task Synchronize_ImportsPreviouslyUntrackedAmbientAccount()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.DiscoveryTests", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "AppData");
        var ambient = Path.Combine(root, ".codex");
        Directory.CreateDirectory(ambient);
        WriteAuth(ambient, "person@example.com", "account-123", "auth0|person");

        try
        {
            var paths = new AppPaths(dataRoot, ambient);
            var service = new ActiveAccountDiscoveryService(paths, new AuthFileReader());

            var profiles = await service.SynchronizeAsync([]);

            Assert.AreEqual(1, profiles.Count);
            Assert.IsTrue(profiles[0].IsActive);
            Assert.AreEqual("account-123", profiles[0].AccountId);
            Assert.IsTrue(File.Exists(Path.Combine(profiles[0].ProfilePath, "auth.json")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Synchronize_DoesNotConfuseSameAccountIdWithDifferentSubject()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.DiscoveryTests", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "AppData");
        var ambient = Path.Combine(root, ".codex");
        Directory.CreateDirectory(ambient);
        WriteAuth(ambient, "second@example.com", "shared-account", "auth0|second");
        var existing = new AccountProfile
        {
            AccountId = "shared-account",
            Subject = "auth0|first",
            Email = "first@example.com",
            ProfilePath = Path.Combine(dataRoot, "profiles", "first"),
            IsActive = true
        };

        try
        {
            var service = new ActiveAccountDiscoveryService(new AppPaths(dataRoot, ambient), new AuthFileReader());

            var profiles = await service.SynchronizeAsync([existing]);

            Assert.AreEqual(2, profiles.Count);
            Assert.IsFalse(existing.IsActive);
            Assert.AreEqual("auth0|second", profiles.Single(profile => profile.IsActive).Subject);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WriteAuth(string home, string email, string accountId, string subject)
    {
        var payload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["email"] = email,
            ["sub"] = subject,
            ["https://api.openai.com/auth"] = new Dictionary<string, string>
            {
                ["chatgpt_account_id"] = accountId,
                ["chatgpt_plan_type"] = "plus"
            }
        });
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(payload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        File.WriteAllText(
            Path.Combine(home, "auth.json"),
            JsonSerializer.Serialize(new
            {
                tokens = new
                {
                    access_token = "access-token",
                    id_token = $"header.{encoded}.signature",
                    account_id = accountId
                }
            }));
    }
}
