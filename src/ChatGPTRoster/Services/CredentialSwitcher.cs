using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public sealed record CredentialSwitchReceipt(
    string? BackupPath,
    string AmbientAuthPath,
    bool AmbientCredentialPreviouslyExisted);

public interface ICredentialSwitcher
{
    Task<CredentialSwitchReceipt> ActivateAsync(
        AccountProfile currentProfile,
        AccountProfile targetProfile,
        CancellationToken cancellationToken = default);

    Task RollbackAsync(CredentialSwitchReceipt receipt, CancellationToken cancellationToken = default);
}

public sealed class CredentialSwitcher : ICredentialSwitcher
{
    private readonly AppPaths _paths;
    private readonly IAuthFileReader _authFileReader;

    public CredentialSwitcher(AppPaths paths, IAuthFileReader authFileReader)
    {
        _paths = paths;
        _authFileReader = authFileReader;
    }

    public async Task<CredentialSwitchReceipt> ActivateAsync(
        AccountProfile currentProfile,
        AccountProfile targetProfile,
        CancellationToken cancellationToken = default)
    {
        var targetAuthPath = Path.Combine(targetProfile.ProfilePath, "auth.json");
        await _authFileReader.ReadIdentityAsync(targetProfile.ProfilePath, cancellationToken);

        Directory.CreateDirectory(_paths.AmbientCodexHome);
        Directory.CreateDirectory(_paths.BackupsDirectory);
        var ambientAuthPath = Path.Combine(_paths.AmbientCodexHome, "auth.json");
        string? backupPath = null;

        var ambientCredentialPreviouslyExisted = File.Exists(ambientAuthPath);
        if (ambientCredentialPreviouslyExisted)
        {
            var activeIdentity = await _authFileReader.ReadIdentityAsync(_paths.AmbientCodexHome, cancellationToken);
            if (Matches(currentProfile, activeIdentity))
            {
                await AtomicFile.CopyAsync(
                    ambientAuthPath,
                    Path.Combine(currentProfile.ProfilePath, "auth.json"),
                    cancellationToken);
            }

            backupPath = Path.Combine(
                _paths.BackupsDirectory,
                $"auth-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
            await AtomicFile.CopyAsync(ambientAuthPath, backupPath, cancellationToken);
        }

        await AtomicFile.CopyAsync(targetAuthPath, ambientAuthPath, cancellationToken);
        try
        {
            await UpdateGlobalStateAsync(currentProfile.AccountId, targetProfile.AccountId, cancellationToken);
        }
        catch
        {
            await RestoreAmbientCredentialAsync(
                backupPath,
                ambientAuthPath,
                ambientCredentialPreviouslyExisted,
                CancellationToken.None);
            throw;
        }

        return new CredentialSwitchReceipt(
            backupPath,
            ambientAuthPath,
            ambientCredentialPreviouslyExisted);
    }

    public async Task RollbackAsync(
        CredentialSwitchReceipt receipt,
        CancellationToken cancellationToken = default)
    {
        await RestoreAmbientCredentialAsync(
            receipt.BackupPath,
            receipt.AmbientAuthPath,
            receipt.AmbientCredentialPreviouslyExisted,
            cancellationToken);
    }

    private static async Task RestoreAmbientCredentialAsync(
        string? backupPath,
        string ambientAuthPath,
        bool previouslyExisted,
        CancellationToken cancellationToken)
    {
        if (previouslyExisted)
        {
            if (string.IsNullOrWhiteSpace(backupPath) || !File.Exists(backupPath))
            {
                throw new IOException("The credential backup required for recovery is unavailable.");
            }

            await AtomicFile.CopyAsync(backupPath, ambientAuthPath, cancellationToken);
            return;
        }

        if (File.Exists(ambientAuthPath))
        {
            File.Delete(ambientAuthPath);
        }
    }

    private async Task UpdateGlobalStateAsync(
        string previousAccountId,
        string targetAccountId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetAccountId))
        {
            return;
        }

        foreach (var fileName in new[] { ".codex-global-state.json", ".codex-global-state.json.bak" })
        {
            var path = Path.Combine(_paths.AmbientCodexHome, fileName);
            if (!File.Exists(path))
            {
                continue;
            }

            JsonNode? root;
            try
            {
                root = JsonNode.Parse(await File.ReadAllTextAsync(path, cancellationToken));
            }
            catch (Exception exception) when (exception is JsonException or IOException or UnauthorizedAccessException)
            {
                continue;
            }

            var environment = root?["electron-persisted-atom-state"]?["environment"] as JsonObject;
            var creatorId = environment?["creator_id"]?.GetValue<string>();
            var updated = UpdateCreatorId(creatorId, previousAccountId, targetAccountId);
            if (environment is null || updated is null || updated == creatorId)
            {
                continue;
            }

            environment["creator_id"] = updated;
            try
            {
                await AtomicFile.WriteTextAsync(
                    path,
                    root!.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + Environment.NewLine,
                    cancellationToken);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                // Global state is a compatibility aid. Credential activation remains authoritative.
            }
        }
    }

    internal static string? UpdateCreatorId(string? creatorId, string? previousAccountId, string targetAccountId)
    {
        if (string.IsNullOrWhiteSpace(creatorId))
        {
            return null;
        }

        var value = creatorId.Trim();
        if (value.Equals(targetAccountId, StringComparison.OrdinalIgnoreCase)
            || value.EndsWith("__" + targetAccountId, StringComparison.OrdinalIgnoreCase))
        {
            return value;
        }

        if (!string.IsNullOrWhiteSpace(previousAccountId)
            && value.Contains(previousAccountId, StringComparison.OrdinalIgnoreCase))
        {
            var index = value.IndexOf(previousAccountId, StringComparison.OrdinalIgnoreCase);
            return value.Remove(index, previousAccountId.Length).Insert(index, targetAccountId);
        }

        if (Guid.TryParse(value, out _))
        {
            return targetAccountId;
        }

        var separatorIndex = value.LastIndexOf("__", StringComparison.Ordinal);
        if (separatorIndex >= 0 && Guid.TryParse(value[(separatorIndex + 2)..], out _))
        {
            return value[..(separatorIndex + 2)] + targetAccountId;
        }

        return null;
    }

    private static bool Matches(AccountProfile profile, AuthIdentity identity)
    {
        if (!profile.AccountId.Equals(identity.AccountId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(profile.Subject)
               || string.IsNullOrWhiteSpace(identity.Subject)
               || profile.Subject.Equals(identity.Subject, StringComparison.Ordinal);
    }
}
