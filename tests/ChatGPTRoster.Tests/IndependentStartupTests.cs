using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class IndependentStartupTests
{
    [TestMethod]
    public async Task Startup_EscapesTheLaunchingCodexJob()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "RosterCompanion.exe");
        var root = Path.Combine(Path.GetTempPath(), "RosterStartup", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true };
        start.Environment["CHATGPT_ROSTER_DATA_ROOT"] = root;
        start.Environment["CHATGPT_ROSTER_CODEX_HOME"] = Path.Combine(root, "ambient");
        start.Environment["ROSTER_COMPANION_DISABLE_AUTOSTART"] = "1";
        start.Environment["ROSTER_COMPANION_SMOKE_INSTANCE"] = Guid.NewGuid().ToString("D");
        var startedAt = DateTime.Now;
        using var original = Process.Start(start)!;
        var candidates = new List<Process>();
        try
        {
            var deadline = Stopwatch.StartNew();
            bool detached = false;
            do
            {
                foreach (var process in Process.GetProcessesByName("RosterCompanion"))
                {
                    try
                    {
                        if (process.StartTime < startedAt.AddSeconds(-1)
                            || !string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase))
                        {
                            process.Dispose();
                            continue;
                        }

                        candidates.Add(process);
                        Assert.IsTrue(IsProcessInJob(process.Handle, nint.Zero, out var inJob));
                        detached |= !inJob;
                    }
                    catch (InvalidOperationException) { process.Dispose(); }
                    catch (System.ComponentModel.Win32Exception) { process.Dispose(); }
                }

                if (detached) break;
                await Task.Delay(100);
            } while (deadline.Elapsed < TimeSpan.FromSeconds(5));

            Assert.IsTrue(detached,
                "Roster must run outside the launching Codex job so closing ChatGPT cannot terminate the switch.");
        }
        finally
        {
            foreach (var process in candidates)
            {
                try { if (!process.HasExited) process.Kill(); }
                catch (InvalidOperationException) { }
                process.Dispose();
            }
            if (!original.HasExited) original.Kill();
            await original.WaitForExitAsync();
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(nint process, nint job, [MarshalAs(UnmanagedType.Bool)] out bool inJob);
}
