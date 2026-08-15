using System.Diagnostics;
using System.IO;
using System.Management;

namespace ChatGPTRoster.Services;

public sealed record DesktopProcessSnapshot(bool WasRunning, string? LauncherPath);

public interface IDesktopProcessService
{
    Task<DesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default);
    Task CloseAsync(CancellationToken cancellationToken = default);
    Task ReopenAsync(DesktopProcessSnapshot snapshot, CancellationToken cancellationToken = default);
}

public sealed class DesktopProcessService : IDesktopProcessService
{
    public Task<DesktopProcessSnapshot> CaptureAsync(CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            var processes = QueryProcesses();
            var launcher = processes
                .Select(process => process.ExecutablePath)
                .FirstOrDefault(path => IsLauncherPath(path));
            return new DesktopProcessSnapshot(processes.Count > 0, launcher);
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
                process.Kill(entireProcessTree: true);
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

    public Task ReopenAsync(DesktopProcessSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!snapshot.WasRunning || string.IsNullOrWhiteSpace(snapshot.LauncherPath))
        {
            return Task.CompletedTask;
        }

        if (!IsLauncherPath(snapshot.LauncherPath) || !File.Exists(snapshot.LauncherPath))
        {
            throw new InvalidOperationException("The verified ChatGPT launcher could not be found after switching.");
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = snapshot.LauncherPath,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Normal
        });
        return Task.CompletedTask;
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

    private static bool IsLauncherPath(string? path)
    {
        if (!IsPackagedDesktopPath(path))
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
