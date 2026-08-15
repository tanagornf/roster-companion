using System.IO;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public interface IProfileFileService
{
    Task RemoveManagedProfileAsync(AccountProfile profile, CancellationToken cancellationToken = default);
}

public sealed class ProfileFileService : IProfileFileService
{
    private readonly AppPaths _paths;

    public ProfileFileService(AppPaths paths)
    {
        _paths = paths;
    }

    public Task RemoveManagedProfileAsync(
        AccountProfile profile,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (string.IsNullOrWhiteSpace(profile.ProfilePath) || !Directory.Exists(profile.ProfilePath))
        {
            return Task.CompletedTask;
        }

        var root = Path.GetFullPath(_paths.ProfilesDirectory)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(profile.ProfilePath);
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Refusing to remove a profile outside Roster Companion's managed data directory.");
        }

        Directory.Delete(target, recursive: true);
        return Task.CompletedTask;
    }
}
