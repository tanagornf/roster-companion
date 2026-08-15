using System.Text;
using System.Text.Json;
using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class CredentialSwitcherTests
{
    [TestMethod]
    public void UpdateCreatorId_PreservesPrefixAndChangesProviderAccount()
    {
        var oldId = "1ea93d04-5c50-42e3-857b-3db850785967";
        var newId = "83c5ae92-f5ee-41f8-9528-199110d1d0f9";

        var updated = CredentialSwitcher.UpdateCreatorId($"user-prefix__{oldId}", oldId, newId);

        Assert.AreEqual($"user-prefix__{newId}", updated);
    }

    [TestMethod]
    public async Task Activate_BacksUpAmbientAndAtomicallySelectsTarget()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.CredentialTests", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "AppData");
        var ambient = Path.Combine(root, ".codex");
        var currentHome = Path.Combine(dataRoot, "profiles", "current");
        var targetHome = Path.Combine(dataRoot, "profiles", "target");
        Directory.CreateDirectory(ambient);
        Directory.CreateDirectory(currentHome);
        Directory.CreateDirectory(targetHome);
        WriteAuth(ambient, "current@example.com", "current-id", "auth0|current");
        WriteAuth(currentHome, "current@example.com", "current-id", "auth0|current");
        WriteAuth(targetHome, "target@example.com", "target-id", "auth0|target");

        try
        {
            var paths = new AppPaths(dataRoot, ambient);
            var reader = new AuthFileReader();
            var service = new CredentialSwitcher(paths, reader);
            var current = new AccountProfile
            {
                AccountId = "current-id",
                Subject = "auth0|current",
                ProfilePath = currentHome
            };
            var target = new AccountProfile
            {
                AccountId = "target-id",
                Subject = "auth0|target",
                ProfilePath = targetHome
            };

            var receipt = await service.ActivateAsync(current, target);
            var active = await reader.ReadIdentityAsync(ambient);

            Assert.AreEqual("target-id", active.AccountId);
            Assert.IsNotNull(receipt.BackupPath);
            Assert.IsTrue(File.Exists(receipt.BackupPath));

            await service.RollbackAsync(receipt);
            Assert.AreEqual("current-id", (await reader.ReadIdentityAsync(ambient)).AccountId);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public async Task Rollback_RemovesActivatedCredentialWhenAmbientCredentialDidNotExist()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.CredentialTests", Guid.NewGuid().ToString("N"));
        var dataRoot = Path.Combine(root, "AppData");
        var ambient = Path.Combine(root, ".codex");
        var currentHome = Path.Combine(dataRoot, "profiles", "current");
        var targetHome = Path.Combine(dataRoot, "profiles", "target");
        Directory.CreateDirectory(currentHome);
        Directory.CreateDirectory(targetHome);
        WriteAuth(currentHome, "current@example.com", "current-id", "auth0|current");
        WriteAuth(targetHome, "target@example.com", "target-id", "auth0|target");

        try
        {
            var paths = new AppPaths(dataRoot, ambient);
            var service = new CredentialSwitcher(paths, new AuthFileReader());
            var current = new AccountProfile
            {
                AccountId = "current-id",
                Subject = "auth0|current",
                ProfilePath = currentHome
            };
            var target = new AccountProfile
            {
                AccountId = "target-id",
                Subject = "auth0|target",
                ProfilePath = targetHome
            };

            var receipt = await service.ActivateAsync(current, target);
            var ambientAuth = Path.Combine(ambient, "auth.json");
            Assert.IsTrue(File.Exists(ambientAuth));
            Assert.IsFalse(receipt.AmbientCredentialPreviouslyExisted);

            await service.RollbackAsync(receipt);

            Assert.IsFalse(File.Exists(ambientAuth));
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
        var auth = new
        {
            tokens = new
            {
                access_token = $"access-{accountId}",
                refresh_token = $"refresh-{accountId}",
                id_token = $"header.{encoded}.signature",
                account_id = accountId
            }
        };
        File.WriteAllText(Path.Combine(home, "auth.json"), JsonSerializer.Serialize(auth));
    }
}
