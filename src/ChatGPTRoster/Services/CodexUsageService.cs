using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public sealed class UsageServiceException : Exception
{
    public UsageServiceException(string message) : base(message)
    {
    }
}

public sealed partial class CodexUsageService : IUsageService
{
    private static readonly Uri DefaultUsageUri = new("https://chatgpt.com/backend-api/wham/usage");
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "chatgpt.com",
        "chat.openai.com"
    };

    private readonly HttpClient _httpClient;
    private readonly IAuthFileReader _authFileReader;

    public CodexUsageService(IAuthFileReader authFileReader, HttpClient? httpClient = null)
    {
        _authFileReader = authFileReader;
        _httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
    }

    public async Task<UsageSnapshot> GetUsageAsync(
        AccountProfile profile,
        CancellationToken cancellationToken = default)
    {
        var credentials = await _authFileReader.ReadCredentialsAsync(profile.ProfilePath, cancellationToken);
        var usageUri = await ResolveUsageUriAsync(profile.ProfilePath, cancellationToken);
        EnsureAllowedUsageUri(usageUri);

        using var request = new HttpRequestMessage(HttpMethod.Get, usageUri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", credentials.AccessToken);
        request.Headers.UserAgent.ParseAdd("codex-cli");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true, NoStore = true };
        if (!string.IsNullOrWhiteSpace(credentials.AccountId ?? profile.AccountId))
        {
            request.Headers.TryAddWithoutValidation("ChatGPT-Account-Id", credentials.AccountId ?? profile.AccountId);
        }

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (HttpRequestException)
        {
            throw new UsageServiceException("Usage could not be refreshed because the network request failed.");
        }
        catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new UsageServiceException("The usage request timed out.");
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new UsageServiceException("This account must sign in again before its usage can be refreshed.");
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new UsageServiceException($"The usage service returned status {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            try
            {
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
                return ParseUsage(document.RootElement);
            }
            catch (JsonException)
            {
                throw new UsageServiceException("The usage service returned an unexpected response.");
            }
        }
    }

    public static void EnsureAllowedUsageUri(Uri uri)
    {
        if (!uri.IsAbsoluteUri
            || !uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || !AllowedHosts.Contains(uri.Host))
        {
            throw new UsageServiceException("Refusing to send account credentials to an unapproved host.");
        }
    }

    private static UsageSnapshot ParseUsage(JsonElement root)
    {
        var snapshot = new UsageSnapshot
        {
            Plan = PlanName.Normalize(GetString(root, "plan_type")),
            LastUpdatedAt = DateTimeOffset.UtcNow
        };

        if (!root.TryGetProperty("rate_limit", out var rateLimit) || rateLimit.ValueKind != JsonValueKind.Object)
        {
            snapshot.ErrorMessage = "Usage windows were not included in the response.";
            return snapshot;
        }

        var limitReached = GetBoolean(rateLimit, "limit_reached") is true;
        var first = ParseWindow(rateLimit, "primary_window", limitReached);
        var second = ParseWindow(rateLimit, "secondary_window", limitReached);

        foreach (var window in new[] { first, second }.Where(window => window is not null))
        {
            if (window!.Label == "Weekly")
            {
                snapshot.Weekly ??= window;
            }
            else
            {
                snapshot.ShortTerm ??= window;
            }
        }

        return snapshot;
    }

    private static UsageWindow? ParseWindow(JsonElement container, string propertyName, bool limitReached)
    {
        if (!container.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var usedPercent = GetDouble(element, "used_percent");
        var windowSeconds = GetDouble(element, "limit_window_seconds");
        var resetAt = ParseResetAt(element);
        if (usedPercent is null && resetAt is null)
        {
            return null;
        }

        var label = windowSeconds is >= 604000 and <= 605000 ? "Weekly" : "5-hour";
        var remaining = limitReached ? 0 : 100 - (usedPercent ?? 100);
        return new UsageWindow
        {
            Label = label,
            RemainingPercent = Math.Clamp(remaining, 0, 100),
            ResetsAt = resetAt
        };
    }

    private static DateTimeOffset? ParseResetAt(JsonElement element)
    {
        if (!element.TryGetProperty("reset_at", out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var epochSeconds))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeSeconds(epochSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        if (value.ValueKind == JsonValueKind.String
            && DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var timestamp))
        {
            return timestamp;
        }

        return null;
    }

    private static string? GetString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static bool? GetBoolean(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? value.GetBoolean()
            : null;

    private static double? GetDouble(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var result)
            ? result
            : null;

    private static async Task<Uri> ResolveUsageUriAsync(string profilePath, CancellationToken cancellationToken)
    {
        var configPath = Path.Combine(profilePath, "config.toml");
        if (!File.Exists(configPath))
        {
            return DefaultUsageUri;
        }

        string config;
        try
        {
            config = await File.ReadAllTextAsync(configPath, cancellationToken);
        }
        catch (IOException)
        {
            throw new UsageServiceException("The account configuration could not be read safely.");
        }

        var match = ChatGptBaseUrlPattern().Match(config);
        if (!match.Success)
        {
            return DefaultUsageUri;
        }

        if (!Uri.TryCreate(match.Groups[1].Value.TrimEnd('/'), UriKind.Absolute, out var configuredBase))
        {
            throw new UsageServiceException("The configured ChatGPT base URL is malformed.");
        }

        EnsureAllowedUsageUri(configuredBase);
        var baseText = configuredBase.AbsoluteUri.TrimEnd('/');
        if (!configuredBase.AbsolutePath.Contains("backend-api", StringComparison.OrdinalIgnoreCase))
        {
            baseText += "/backend-api";
        }

        return new Uri(baseText + "/wham/usage");
    }

    [GeneratedRegex("(?m)^\\s*chatgpt_base_url\\s*=\\s*[\\\"']([^\\\"']+)[\\\"']\\s*(?:#.*)?$")]
    private static partial Regex ChatGptBaseUrlPattern();
}
