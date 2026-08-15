using Microsoft.Win32;

namespace ChatGPTRoster.Services;

public interface IStartupRegistrationService
{
    bool IsEnabled { get; }
    void EnsureRegistered();
    void SetEnabled(bool enabled);
}

public sealed class StartupRegistrationService : IStartupRegistrationService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Roster Companion";

    public bool IsEnabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            if (key?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            using var approved = Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
                writable: false);
            return approved?.GetValue(ValueName) is not byte[] state || state.Length == 0 || state[0] != 3;
        }
    }

    public void EnsureRegistered()
    {
        var expectedValue = GetStartupCommand();
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        if (key?.GetValue(ValueName) is not string value || string.IsNullOrWhiteSpace(value))
        {
            SetEnabled(true);
            return;
        }

        if (!string.Equals(value, expectedValue, StringComparison.OrdinalIgnoreCase))
        {
            key.SetValue(ValueName, expectedValue, RegistryValueKind.String);
        }
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath, writable: true);
        if (!enabled)
        {
            key.DeleteValue(ValueName, throwOnMissingValue: false);
            return;
        }

        key.SetValue(ValueName, GetStartupCommand(), RegistryValueKind.String);
        using var approved = Registry.CurrentUser.CreateSubKey(
            @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run",
            writable: true);
        approved.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    private static string GetStartupCommand()
    {
        var executable = Environment.ProcessPath
                         ?? throw new InvalidOperationException("The Roster Companion executable path is unavailable.");
        return $"\"{executable}\" --background";
    }
}
