using System.Text;
using System.Text.Json;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class AuthFileReaderTests
{
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.AuthTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    [TestMethod]
    public async Task ReadIdentity_UsesNonSecretJwtClaims()
    {
        WriteAuth(
            "person@example.com",
            "account-123",
            "auth0|subject-123",
            "plus",
            "2026-08-12T11:01:55.166468500+00:00");
        var reader = new AuthFileReader();

        var identity = await reader.ReadIdentityAsync(_temporaryDirectory);

        Assert.AreEqual("person@example.com", identity.Email);
        Assert.AreEqual("account-123", identity.AccountId);
        Assert.AreEqual("auth0|subject-123", identity.Subject);
        Assert.AreEqual("plus", identity.Plan);
    }

    [TestMethod]
    public async Task ReadCredentials_AcceptsNanosecondTimestamp()
    {
        WriteAuth(
            "person@example.com",
            "account-123",
            "auth0|subject-123",
            "plus",
            "2026-08-12T11:01:55.166468500+00:00");
        var reader = new AuthFileReader();

        var credentials = await reader.ReadCredentialsAsync(_temporaryDirectory);

        Assert.IsNotNull(credentials.LastRefresh);
        Assert.AreEqual(166, credentials.LastRefresh.Value.Millisecond);
    }

    private void WriteAuth(string email, string accountId, string subject, string plan, string lastRefresh)
    {
        var jwtPayload = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["email"] = email,
            ["sub"] = subject,
            ["https://api.openai.com/auth"] = new Dictionary<string, string>
            {
                ["chatgpt_plan_type"] = plan,
                ["chatgpt_account_id"] = accountId
            }
        });
        var encodedPayload = Convert.ToBase64String(Encoding.UTF8.GetBytes(jwtPayload))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        var idToken = $"header.{encodedPayload}.signature";
        var auth = new
        {
            tokens = new
            {
                access_token = "test-access-token",
                refresh_token = "test-refresh-token",
                id_token = idToken,
                account_id = accountId
            },
            last_refresh = lastRefresh
        };

        File.WriteAllText(
            Path.Combine(_temporaryDirectory, "auth.json"),
            JsonSerializer.Serialize(auth));
    }
}
