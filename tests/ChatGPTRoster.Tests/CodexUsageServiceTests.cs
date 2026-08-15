using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class CodexUsageServiceTests
{
    private string _temporaryDirectory = null!;

    [TestInitialize]
    public void Initialize()
    {
        _temporaryDirectory = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.UsageTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_temporaryDirectory);
        File.WriteAllText(
            Path.Combine(_temporaryDirectory, "auth.json"),
            JsonSerializer.Serialize(new
            {
                tokens = new
                {
                    access_token = "test-access-token",
                    account_id = "account-123"
                }
            }));
    }

    [TestCleanup]
    public void Cleanup()
    {
        Directory.Delete(_temporaryDirectory, recursive: true);
    }

    [TestMethod]
    public async Task GetUsage_MapsFiveHourAndWeeklyWindowsToRemainingPercent()
    {
        var reset = DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds();
        var handler = new StubHandler(HttpStatusCode.OK, $$"""
            {
              "plan_type": "pro",
              "rate_limit": {
                "allowed": true,
                "limit_reached": false,
                "primary_window": {
                  "used_percent": 28,
                  "reset_at": {{reset}},
                  "limit_window_seconds": 18000
                },
                "secondary_window": {
                  "used_percent": 59,
                  "reset_at": {{reset}},
                  "limit_window_seconds": 604800
                }
              }
            }
            """);
        var service = new CodexUsageService(new AuthFileReader(), new HttpClient(handler));

        var snapshot = await service.GetUsageAsync(CreateProfile());

        Assert.AreEqual("Pro", snapshot.Plan);
        Assert.AreEqual(72, snapshot.ShortTerm?.RemainingPercent);
        Assert.AreEqual(41, snapshot.Weekly?.RemainingPercent);
        Assert.AreEqual("chatgpt.com", handler.LastRequestUri?.Host);
        Assert.AreEqual("account-123", handler.LastAccountHeader);
    }

    [TestMethod]
    public async Task GetUsage_KeepsWeeklyOnlyDataInWeeklySlot()
    {
        var handler = new StubHandler(HttpStatusCode.OK, """
            {
              "rate_limit": {
                "primary_window": {
                  "used_percent": 42,
                  "limit_window_seconds": 604800
                }
              }
            }
            """);
        var service = new CodexUsageService(new AuthFileReader(), new HttpClient(handler));

        var snapshot = await service.GetUsageAsync(CreateProfile());

        Assert.IsNull(snapshot.ShortTerm);
        Assert.AreEqual(58, snapshot.Weekly?.RemainingPercent);
    }

    [TestMethod]
    public async Task GetUsage_RejectsConfiguredNonOpenAiHostBeforeSendingToken()
    {
        await File.WriteAllTextAsync(
            Path.Combine(_temporaryDirectory, "config.toml"),
            "chatgpt_base_url = \"https://example.com/custom\"");
        var handler = new StubHandler(HttpStatusCode.OK, "{}");
        var service = new CodexUsageService(new AuthFileReader(), new HttpClient(handler));

        await Assert.ThrowsExceptionAsync<UsageServiceException>(() => service.GetUsageAsync(CreateProfile()));

        Assert.AreEqual(0, handler.RequestCount);
    }

    [TestMethod]
    public void EnsureAllowedUsageUri_RequiresHttpsAndExactAllowlistedHost()
    {
        CodexUsageService.EnsureAllowedUsageUri(new Uri("https://chatgpt.com/backend-api/wham/usage"));

        Assert.ThrowsException<UsageServiceException>(() =>
            CodexUsageService.EnsureAllowedUsageUri(new Uri("https://chatgpt.com.example.org/usage")));
        Assert.ThrowsException<UsageServiceException>(() =>
            CodexUsageService.EnsureAllowedUsageUri(new Uri("http://chatgpt.com/usage")));
    }

    private AccountProfile CreateProfile() => new()
    {
        AccountId = "account-123",
        Email = "person@example.com",
        ProfilePath = _temporaryDirectory
    };

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _responseBody;

        public StubHandler(HttpStatusCode statusCode, string responseBody)
        {
            _statusCode = statusCode;
            _responseBody = responseBody;
        }

        public int RequestCount { get; private set; }
        public Uri? LastRequestUri { get; private set; }
        public string? LastAccountHeader { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestCount++;
            LastRequestUri = request.RequestUri;
            LastAccountHeader = request.Headers.TryGetValues("ChatGPT-Account-Id", out var values)
                ? values.SingleOrDefault()
                : null;

            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
