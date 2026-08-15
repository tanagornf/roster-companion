using System.IO;
using System.Text.Json;

namespace ChatGPTRoster.Services;

public sealed class AppSettings
{
    public bool InitialAccountPromptCompleted { get; set; }
    public bool InitialAccountImportAccepted { get; set; }
}

public interface IAppSettingsService
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default);
}

public sealed class AppSettingsService : IAppSettingsService
{
    private readonly AppPaths _paths;
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    public AppSettingsService(AppPaths paths)
    {
        _paths = paths;
    }

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_paths.SettingsFile))
        {
            return new AppSettings();
        }

        try
        {
            await using var stream = File.OpenRead(_paths.SettingsFile);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken)
                   ?? new AppSettings();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(_paths.DataRoot);
        await AtomicFile.WriteTextAsync(
            _paths.SettingsFile,
            JsonSerializer.Serialize(settings, Options) + Environment.NewLine,
            cancellationToken);
    }
}
