using System.IO;
using System.Text;
using System.Text.Json;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public sealed class AuthFileException : Exception
{
    public AuthFileException(string message) : base(message)
    {
    }
}

public sealed record LocalCredentials(
    string AccessToken,
    string? RefreshToken,
    string? IdToken,
    string? AccountId,
    DateTimeOffset? LastRefresh);

public interface IAuthFileReader
{
    Task<AuthIdentity> ReadIdentityAsync(string profilePath, CancellationToken cancellationToken = default);
    Task<LocalCredentials> ReadCredentialsAsync(string profilePath, CancellationToken cancellationToken = default);
}

public sealed class AuthFileReader : IAuthFileReader
{
    public async Task<AuthIdentity> ReadIdentityAsync(
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        var credentials = await ReadCredentialsAsync(profilePath, cancellationToken);
        if (string.IsNullOrWhiteSpace(credentials.IdToken))
        {
            throw new AuthFileException("The login did not provide the identity information required for this profile.");
        }

        using var payload = ParseJwtPayload(credentials.IdToken);
        var root = payload.RootElement;
        var authClaim = TryGetObject(root, "https://api.openai.com/auth");
        var profileClaim = TryGetObject(root, "https://api.openai.com/profile");

        var email = GetString(root, "email") ?? GetString(profileClaim, "email");
        var accountId = credentials.AccountId
                        ?? GetString(authClaim, "chatgpt_account_id")
                        ?? GetString(root, "chatgpt_account_id");

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(accountId))
        {
            throw new AuthFileException("The login identity is missing an email address or provider account ID.");
        }

        return new AuthIdentity(
            email,
            accountId,
            GetString(root, "sub"),
            GetString(authClaim, "chatgpt_plan_type") ?? GetString(root, "chatgpt_plan_type"));
    }

    public async Task<LocalCredentials> ReadCredentialsAsync(
        string profilePath,
        CancellationToken cancellationToken = default)
    {
        var authPath = Path.Combine(profilePath, "auth.json");
        if (!File.Exists(authPath))
        {
            throw new AuthFileException("No auth.json was created for this account.");
        }

        try
        {
            await using var stream = new FileStream(
                authPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            var root = document.RootElement;

            if (GetString(root, "OPENAI_API_KEY") is not null)
            {
                throw new AuthFileException("API-key profiles do not expose ChatGPT subscription usage.");
            }

            if (!root.TryGetProperty("tokens", out var tokens) || tokens.ValueKind != JsonValueKind.Object)
            {
                throw new AuthFileException("The authentication file is missing its token container.");
            }

            var accessToken = GetString(tokens, "access_token");
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                throw new AuthFileException("The authentication file is missing its access credential.");
            }

            return new LocalCredentials(
                accessToken,
                GetString(tokens, "refresh_token"),
                GetString(tokens, "id_token"),
                GetString(tokens, "account_id"),
                ParseTimestamp(GetString(root, "last_refresh")));
        }
        catch (JsonException)
        {
            throw new AuthFileException("The authentication file is not valid JSON.");
        }
        catch (IOException)
        {
            throw new AuthFileException("The authentication file could not be read.");
        }
    }

    private static JsonDocument ParseJwtPayload(string token)
    {
        try
        {
            var parts = token.Split('.');
            if (parts.Length < 2)
            {
                throw new FormatException();
            }

            var encodedPayload = parts[1]
                .Replace('-', '+')
                .Replace('_', '/');
            encodedPayload = encodedPayload.PadRight(
                encodedPayload.Length + ((4 - encodedPayload.Length % 4) % 4),
                '=');

            var bytes = Convert.FromBase64String(encodedPayload);
            return JsonDocument.Parse(Encoding.UTF8.GetString(bytes));
        }
        catch (Exception exception) when (exception is FormatException or JsonException)
        {
            throw new AuthFileException("The identity credential is malformed.");
        }
    }

    private static JsonElement? TryGetObject(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(propertyName, out var value)
            && value.ValueKind == JsonValueKind.Object)
        {
            return value;
        }

        return null;
    }

    private static string? GetString(JsonElement? element, string propertyName)
    {
        if (element is not { ValueKind: JsonValueKind.Object } objectElement
            || !objectElement.TryGetProperty(propertyName, out var value)
            || value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var result = value.GetString()?.Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    private static DateTimeOffset? ParseTimestamp(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        // DateTimeOffset accepts up to seven fractional digits. Codex may write nanoseconds.
        var dotIndex = value.IndexOf('.');
        if (dotIndex >= 0)
        {
            var offsetIndex = value.IndexOfAny(['Z', '+', '-'], dotIndex + 1);
            var fractionEnd = offsetIndex >= 0 ? offsetIndex : value.Length;
            var fractionLength = fractionEnd - dotIndex - 1;
            if (fractionLength > 7)
            {
                value = value.Remove(dotIndex + 1 + 7, fractionLength - 7);
            }
        }

        return DateTimeOffset.TryParse(value, out var timestamp) ? timestamp : null;
    }
}
