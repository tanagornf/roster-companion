using System.Diagnostics;
using System.IO;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;

namespace ChatGPTRoster.Services;

public sealed record DesktopProcessSnapshot(bool WasRunning, string? LauncherPath, string? ApplicationUserModelId = null);

public interface IDesktopProcessService
{
    Task<DesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task ReopenAsync(DesktopProcessSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed class DesktopProcessService : IDesktopProcessService
{
    private readonly Action<string> _activatePackage;
    private readonly Action<string> _launchExecutable;
    private readonly Func<Task<bool>> _hasDesktopWindow;
    private readonly Func<string, bool> _fileExists;
    private readonly TimeSpan _startupTimeout;

    public DesktopProcessService() : this(ActivatePackage, LaunchExecutable, HasDesktopWindowAsync, File.Exists, TimeSpan.FromSeconds(20)) { }

    internal DesktopProcessService(Action<string> activatePackage, Action<string> launchExecutable,
        Func<Task<bool>> hasDesktopWindow, Func<string, bool> fileExists, TimeSpan startupTimeout)
    {
        _activatePackage = activatePackage;
        _launchExecutable = launchExecutable;
        _hasDesktopWindow = hasDesktopWindow;
        _fileExists = fileExists;
        _startupTimeout = startupTimeout;
    }

    public Task<DesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var processes = QueryProcesses();
            var launcher = processes.FirstOrDefault(process => IsLauncherPath(process.ExecutablePath));
            return new DesktopProcessSnapshot(processes.Count > 0, launcher?.ExecutablePath,
                launcher is null ? null : ReadApplicationUserModelId(launcher.ProcessId));
        }, cancellationToken);

    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        var initial = await Task.Run(QueryProcesses, cancellationToken);
        foreach (var item in initial.Where(item => IsLauncherPath(item.ExecutablePath)))
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var process = Process.GetProcessById(item.ProcessId);
                process.CloseMainWindow();
            }
            catch (ArgumentException)
            {
            }
        }

        await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        var remaining = await Task.Run(QueryProcesses, cancellationToken);
        foreach (var item in remaining)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var process = Process.GetProcessById(item.ProcessId);
                TerminateDesktopProcess(process);
            }
            catch (ArgumentException)
            {
            }
        }

        await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        if ((await Task.Run(QueryProcesses, cancellationToken)).Count > 0)
        {
            throw new InvalidOperationException("The ChatGPT desktop process did not close. Exit it from the system tray and try again.");
        }
    }

    // A companion or unrelated tool may have been launched from the desktop.
    // QueryProcesses already limits shutdown to verified package executables.
    internal static void TerminateDesktopProcess(Process process) => process.Kill(entireProcessTree: false);

    public async Task ReopenAsync(DesktopProcessSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!snapshot.WasRunning)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.ApplicationUserModelId)
            && snapshot.ApplicationUserModelId.StartsWith("OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)
            && snapshot.ApplicationUserModelId.EndsWith("!App", StringComparison.OrdinalIgnoreCase))
        {
            _activatePackage(snapshot.ApplicationUserModelId);
        }
        else if (IsLauncherPath(snapshot.LauncherPath) && _fileExists(snapshot.LauncherPath!))
        {
            _launchExecutable(snapshot.LauncherPath!);
        }
        else
        {
            throw new InvalidOperationException("The verified ChatGPT launcher could not be found after switching.");
        }

        var elapsed = Stopwatch.StartNew();
        do
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await _hasDesktopWindow())
            {
                return;
            }

            if (elapsed.Elapsed >= _startupTimeout)
            {
                break;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken);
        } while (elapsed.Elapsed < _startupTimeout);

        throw new InvalidOperationException("ChatGPT did not open a desktop window after the launch request.");
    }

    private static void LaunchExecutable(string path)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = path,
            WorkingDirectory = Path.GetDirectoryName(path)!,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        });
    }

    private static Task<bool> HasDesktopWindowAsync() => Task.Run(() =>
    {
        foreach (var item in QueryProcesses().Where(item => IsLauncherPath(item.ExecutablePath)))
        {
            try
            {
                using var process = Process.GetProcessById(item.ProcessId);
                if (process.MainWindowHandle != nint.Zero) return true;
            }
            catch (ArgumentException) { }
            catch (InvalidOperationException) { }
        }
        return false;
    });

    private static string? ReadApplicationUserModelId(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            uint length = 0;
            if (GetApplicationUserModelId(process.Handle, ref length, null) != 122 || length == 0) return null;
            var id = new StringBuilder((int)length);
            return GetApplicationUserModelId(process.Handle, ref length, id) == 0 ? id.ToString() : null;
        }
        catch (ArgumentException) { return null; }
        catch (InvalidOperationException) { return null; }
        catch (System.ComponentModel.Win32Exception) { return null; }
    }

    private static void ActivatePackage(string appId)
    {
        var type = Type.GetTypeFromCLSID(new Guid("45BA127D-10A8-46EA-8AB7-56EA9078943C"), throwOnError: true)!;
        var manager = (IApplicationActivationManager)Activator.CreateInstance(type)!;
        try
        {
            Marshal.ThrowExceptionForHR(manager.ActivateApplication(appId, string.Empty, 0, out _));
        }
        finally
        {
            Marshal.ReleaseComObject(manager);
        }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder? appId);

    [ComImport, Guid("2E941141-7F97-4756-BA1D-9DECDE894A3D"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IApplicationActivationManager
    {
        [PreserveSig]
        int ActivateApplication([MarshalAs(UnmanagedType.LPWStr)] string appId,
            [MarshalAs(UnmanagedType.LPWStr)] string arguments, uint options, out uint processId);
    }

    public static bool IsPackagedDesktopPath(string? executablePath)
    {
        if (string.IsNullOrWhiteSpace(executablePath))
        {
            return false;
        }

        var normalized = executablePath.Replace('/', '\\');
        if (!normalized.Contains("\\OpenAI.Codex_", StringComparison.OrdinalIgnoreCase)
            || !normalized.Contains("\\app\\", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(normalized);
        return fileName.Equals("ChatGPT.exe", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("Codex.exe", StringComparison.OrdinalIgnoreCase)
               || fileName.Equals("codex.exe", StringComparison.OrdinalIgnoreCase)
                  && normalized.Contains("\\app\\resources\\", StringComparison.OrdinalIgnoreCase);
    }

    internal static bool IsLauncherPath(string? path)
    {
        if (!IsPackagedDesktopPath(path))
        {
            return false;
        }

        if (!string.Equals(Path.GetFileName(Path.GetDirectoryName(path)), "app", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(path);
        return string.Equals(fileName, "ChatGPT.exe", StringComparison.OrdinalIgnoreCase)
               || string.Equals(fileName, "Codex.exe", StringComparison.OrdinalIgnoreCase);
    }

    private static List<DesktopProcessInfo> QueryProcesses()
    {
        var results = new List<DesktopProcessInfo>();
        using var searcher = new ManagementObjectSearcher(
            "SELECT ProcessId, ExecutablePath FROM Win32_Process WHERE Name='ChatGPT.exe' OR Name='Codex.exe' OR Name='codex.exe'");
        using var collection = searcher.Get();
        foreach (ManagementObject item in collection)
        {
            var path = item["ExecutablePath"] as string;
            if (!IsPackagedDesktopPath(path))
            {
                continue;
            }

            results.Add(new DesktopProcessInfo(Convert.ToInt32((uint)item["ProcessId"]), path!));
        }

        return results;
    }

    private sealed record DesktopProcessInfo(int ProcessId, string ExecutablePath);
}
