using System.IO;

namespace ChatGPTRoster.Services;

public interface ICodexCliLocator
{
    string? FindExecutable();
}

public sealed class CodexCliLocator : ICodexCliLocator
{
    internal const string ForceDesktopEnrollmentVariable = "ROSTER_COMPANION_FORCE_DESKTOP_ENROLLMENT";
    private readonly string _localAppData;
    private readonly string _userProfile;
    private readonly string? _pathValue;

    public CodexCliLocator(
        string? localAppData = null,
        string? userProfile = null,
        string? pathValue = null)
    {
        _localAppData = localAppData
                        ?? Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        _userProfile = userProfile
                       ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        _pathValue = pathValue ?? Environment.GetEnvironmentVariable("PATH");
    }

    public string? FindExecutable()
    {
        if (IsDesktopEnrollmentForced)
        {
            return null;
        }

        foreach (var candidate in GetCandidates())
        {
            if (File.Exists(candidate))
            {
                return Path.GetFullPath(candidate);
            }
        }

        return null;
    }

    internal static bool IsDesktopEnrollmentForced
    {
        get
        {
            var value = Environment.GetEnvironmentVariable(ForceDesktopEnrollmentVariable);
            return value?.Equals("1", StringComparison.OrdinalIgnoreCase) == true
                   || value?.Equals("true", StringComparison.OrdinalIgnoreCase) == true;
        }
    }

    private IEnumerable<string> GetCandidates()
    {
        yield return Path.Combine(_localAppData, "Programs", "OpenAI", "Codex", "bin", "codex.exe");
        yield return Path.Combine(_localAppData, "OpenAI", "Codex", "bin", "codex.exe");
        yield return Path.Combine(_userProfile, ".bun", "bin", "codex.exe");
        yield return Path.Combine(_localAppData, "Microsoft", "WindowsApps", "codex.exe");

        if (string.IsNullOrWhiteSpace(_pathValue))
        {
            yield break;
        }

        foreach (var directory in _pathValue.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string candidate;
            try
            {
                candidate = Path.Combine(directory.Trim('"'), "codex.exe");
            }
            catch (ArgumentException)
            {
                continue;
            }

            yield return candidate;
        }
    }
}
