using System.IO;
using System.Text.Json;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public sealed class JsonProfileStore : IProfileStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private readonly AppPaths _paths;

    public JsonProfileStore(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<IReadOnlyList<AccountProfile>> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.ProfilesFile))
        {
            return Array.Empty<AccountProfile>();
        }

        await using var stream = new FileStream(
            _paths.ProfilesFile,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            4096,
            FileOptions.Asynchronous | FileOptions.SequentialScan);

        return await JsonSerializer.DeserializeAsync<List<AccountProfile>>(
                   stream,
                   JsonOptions,
                   cancellationToken)
               ?? new List<AccountProfile>();
    }

    public async Task SaveAsync(
        IReadOnlyCollection<AccountProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataRoot);
        var temporaryPath = _paths.ProfilesFile + ".tmp";
        var backupPath = _paths.ProfilesFile + ".bak";

        try
        {
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Create,
                             FileAccess.Write,
                             FileShare.None,
                             4096,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(stream, profiles, JsonOptions, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            if (File.Exists(_paths.ProfilesFile))
            {
                File.Copy(_paths.ProfilesFile, backupPath, overwrite: true);
            }

            File.Move(temporaryPath, _paths.ProfilesFile, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
