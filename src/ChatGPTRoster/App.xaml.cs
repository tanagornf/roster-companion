using System.Threading;
using System.Windows;

namespace ChatGPTRoster;

public partial class App : Application
{
    private Mutex? _singleInstance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        _singleInstance = new Mutex(initiallyOwned: true, GetSingleInstanceName(), out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        try
        {
            var companion = new MainWindow();
            MainWindow = companion;
            companion.Start();
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                $"Roster Companion could not start. No account credentials were changed.\n\n{exception.Message}",
                "Roster Companion",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    private static string GetSingleInstanceName()
    {
        const string defaultName = "Local\\RosterCompanion.tanagornf";
        var smokeInstance = Environment.GetEnvironmentVariable("ROSTER_COMPANION_SMOKE_INSTANCE");
        var isolatedDataRoot = Environment.GetEnvironmentVariable("CHATGPT_ROSTER_DATA_ROOT");
        var autostartDisabled = Environment.GetEnvironmentVariable("ROSTER_COMPANION_DISABLE_AUTOSTART");
        if (Guid.TryParse(smokeInstance, out var identifier)
            && !string.IsNullOrWhiteSpace(isolatedDataRoot)
            && string.Equals(autostartDisabled, "1", StringComparison.OrdinalIgnoreCase))
        {
            return $"{defaultName}.Smoke.{identifier:N}";
        }

        return defaultName;
    }
}
