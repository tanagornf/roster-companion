using System.IO;
using System.Text.Json;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public interface IDesktopSessionService
{
    Task BackupAsync(AccountProfile profile, CancellationToken cancellationToken = default);
    Task RestoreOrClearAsync(AccountProfile profile, CancellationToken cancellationToken = default);
}

public sealed class DesktopSessionService : IDesktopSessionService
{
    private static readonly string[] LegacyEntries =
    [
        "blob_storage", "DIPS", "DIPS-wal", "Local State", "Local Storage", "Network",
        "Partitions", "Preferences", "Session Storage", "SharedStorage", "SharedStorage-wal", "shared_proto_db"
    ];

    private static readonly string[] ModernEntries =
    [
        "Local State",
        "Default\\DIPS", "Default\\DIPS-wal", "Default\\Local Storage", "Default\\Network",
        "Default\\Partitions", "Default\\Preferences", "Default\\Session Storage",
        "Default\\SharedStorage", "Default\\SharedStorage-wal", "Default\\shared_proto_db",
        "codex-browser-app\\Local Storage", "codex-browser-app\\Network",
        "codex-browser-app\\Preferences", "codex-browser-app\\Session Storage",
        "codex-browser-app\\SharedStorage", "codex-browser-app\\SharedStorage-wal",
        "codex-browser-app\\shared_proto_db"
    ];

    private readonly string _packagesRoot;

    public DesktopSessionService(string? localAppData = null)
    {
        _packagesRoot = Path.Combine(
            localAppData ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages");
    }

    public Task BackupAsync(AccountProfile profile, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var adapter = FindAdapter();
            if (adapter is null)
            {
                return;
            }

            var snapshotPath = Path.Combine(profile.ProfilePath, "desktop-session");
            var stagingPath = snapshotPath + ".tmp." + Guid.NewGuid().ToString("N");
            var previousPath = snapshotPath + ".previous";
            try
            {
                Directory.CreateDirectory(stagingPath);
                foreach (var relativePath in adapter.Entries)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    CopyEntry(adapter.RootPath, stagingPath, relativePath);
                }

                File.WriteAllText(
                    Path.Combine(stagingPath, "adapter.json"),
                    JsonSerializer.Serialize(new { adapter = adapter.Id, createdAt = DateTimeOffset.UtcNow }));

                if (Directory.Exists(previousPath))
                {
                    Directory.Delete(previousPath, recursive: true);
                }

                if (Directory.Exists(snapshotPath))
                {
                    Directory.Move(snapshotPath, previousPath);
                }

                try
                {
                    Directory.Move(stagingPath, snapshotPath);
                    if (Directory.Exists(previousPath))
                    {
                        Directory.Delete(previousPath, recursive: true);
                    }
                }
                catch
                {
                    if (!Directory.Exists(snapshotPath) && Directory.Exists(previousPath))
                    {
                        Directory.Move(previousPath, snapshotPath);
                    }

                    throw;
                }
            }
            finally
            {
                if (Directory.Exists(stagingPath))
                {
                    Directory.Delete(stagingPath, recursive: true);
                }
            }
        }, cancellationToken);

    public Task RestoreOrClearAsync(AccountProfile profile, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var adapter = FindAdapter();
            if (adapter is null)
            {
                return;
            }

            var snapshotPath = Path.Combine(profile.ProfilePath, "desktop-session");
            var snapshotMatches = SnapshotMatchesAdapter(snapshotPath, adapter.Id);

            foreach (var relativePath in adapter.Entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                DeleteEntry(adapter.RootPath, relativePath);
                if (snapshotMatches)
                {
                    CopyEntry(snapshotPath, adapter.RootPath, relativePath);
                }
            }
        }, cancellationToken);

    private SessionAdapter? FindAdapter()
    {
        if (!Directory.Exists(_packagesRoot))
        {
            return null;
        }

        foreach (var package in Directory.EnumerateDirectories(_packagesRoot, "OpenAI.Codex*"))
        {
            var legacyRoot = Path.Combine(package, "LocalCache", "Roaming", "Codex");
            var modernRoot = Path.Combine(legacyRoot, "web", "Codex");
            if (Directory.Exists(modernRoot))
            {
                return new SessionAdapter("chromium-v2", modernRoot, ModernEntries);
            }

            if (Directory.Exists(legacyRoot))
            {
                return new SessionAdapter("electron-v1", legacyRoot, LegacyEntries);
            }
        }

        return null;
    }

    private static bool SnapshotMatchesAdapter(string snapshotPath, string adapterId)
    {
        var metadataPath = Path.Combine(snapshotPath, "adapter.json");
        if (!File.Exists(metadataPath))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(metadataPath));
            return document.RootElement.TryGetProperty("adapter", out var value)
                   && value.ValueKind == JsonValueKind.String
                   && value.GetString() == adapterId;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void CopyEntry(string sourceRoot, string destinationRoot, string relativePath)
    {
        var source = Path.Combine(sourceRoot, relativePath);
        if (File.Exists(source))
        {
            var destination = Path.Combine(destinationRoot, relativePath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(source, destination, overwrite: true);
            return;
        }

        if (!Directory.Exists(source))
        {
            return;
        }

        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, directory);
            Directory.CreateDirectory(Path.Combine(destinationRoot, relativePath, relative));
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(destinationRoot, relativePath, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }
    }

    private static void DeleteEntry(string root, string relativePath)
    {
        var target = Path.Combine(root, relativePath);
        if (File.Exists(target))
        {
            File.Delete(target);
        }
        else if (Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }

    private sealed record SessionAdapter(string Id, string RootPath, IReadOnlyList<string> Entries);
}
