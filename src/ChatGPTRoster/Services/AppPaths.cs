using System.IO;

namespace ChatGPTRoster.Services;

public sealed class AppPaths
{
    public AppPaths(string? dataRoot = null, string? ambientCodexHome = null)
    {
        DataRoot = ResolvePath(
            dataRoot,
            "CHATGPT_ROSTER_DATA_ROOT",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "ChatGPTRoster"));
        AmbientCodexHome = ResolvePath(
            ambientCodexHome,
            "CHATGPT_ROSTER_CODEX_HOME",
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".codex"));
    }

    public string DataRoot { get; }
    public string ProfilesFile => Path.Combine(DataRoot, "profiles.json");
    public string SettingsFile => Path.Combine(DataRoot, "settings.json");
    public string ProfilesDirectory => Path.Combine(DataRoot, "profiles");
    public string BackupsDirectory => Path.Combine(DataRoot, "backups");
    public string AmbientCodexHome { get; }

    private static string ResolvePath(string? explicitPath, string environmentName, string fallback)
    {
        var value = explicitPath;
        if (string.IsNullOrWhiteSpace(value))
        {
            value = Environment.GetEnvironmentVariable(environmentName);
        }

        return Path.GetFullPath(string.IsNullOrWhiteSpace(value) ? fallback : value);
    }
}
