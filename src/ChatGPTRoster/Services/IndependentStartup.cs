using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace ChatGPTRoster.Services;

internal static class IndependentStartup
{
    // Codex owns a job containing tools it launches. Closing ChatGPT tears down
    // that job, even when individual desktop processes are killed without /T.
    // Use Explorer as the parent to inherit its lifetime instead, while retaining
    // our environment (including isolated test data) and command line.
    public static bool RelaunchOutsideJob()
    {
        if (!IsProcessInJob(GetCurrentProcess(), nint.Zero, out var inJob))
            throw new Win32Exception(Marshal.GetLastWin32Error());
        if (!inJob) return false;

        GetWindowThreadProcessId(GetShellWindow(), out var shellId);
        var shell = OpenProcess(0x0080 | 0x1000, false, shellId);
        if (shell == nint.Zero) throw new Win32Exception(Marshal.GetLastWin32Error());

        nint attributes = nint.Zero;
        nint parentValue = nint.Zero;
        var initialized = false;
        try
        {
            if (!IsProcessInJob(shell, nint.Zero, out var shellInJob))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            if (shellInJob) throw new InvalidOperationException("Windows Explorer is not available for an independent Roster launch.");

            nuint size = 0;
            InitializeProcThreadAttributeList(nint.Zero, 1, 0, ref size);
            attributes = Marshal.AllocHGlobal(checked((int)size));
            if (!InitializeProcThreadAttributeList(attributes, 1, 0, ref size))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            initialized = true;
            parentValue = Marshal.AllocHGlobal(nint.Size);
            Marshal.WriteIntPtr(parentValue, shell);
            if (!UpdateProcThreadAttribute(attributes, 0, 0x00020000, parentValue, (nuint)nint.Size, nint.Zero, nint.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            var startup = new StartupInfoEx
            {
                StartupInfo = new StartupInfo { Size = Marshal.SizeOf<StartupInfoEx>(), Flags = 1, ShowWindow = 0 },
                AttributeList = attributes
            };
            if (!CreateProcess(Environment.ProcessPath!, new StringBuilder(Environment.CommandLine),
                    nint.Zero, nint.Zero, false, 0x00080000, nint.Zero, Environment.CurrentDirectory,
                    ref startup, out var process))
                throw new Win32Exception(Marshal.GetLastWin32Error());

            CloseHandle(process.Thread);
            CloseHandle(process.Process);
            return true;
        }
        finally
        {
            if (initialized) DeleteProcThreadAttributeList(attributes);
            if (attributes != nint.Zero) Marshal.FreeHGlobal(attributes);
            if (parentValue != nint.Zero) Marshal.FreeHGlobal(parentValue);
            CloseHandle(shell);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfo
    {
        public int Size;
        public nint Reserved, Desktop, Title;
        public int X, Y, Width, Height, XChars, YChars, FillAttribute, Flags;
        public short ShowWindow, ReservedSize;
        public nint ReservedData, StandardInput, StandardOutput, StandardError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct StartupInfoEx { public StartupInfo StartupInfo; public nint AttributeList; }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation { public nint Process, Thread; public uint ProcessId, ThreadId; }

    [DllImport("kernel32.dll")] private static extern nint GetCurrentProcess();
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool IsProcessInJob(nint process, nint job, out bool inJob);
    [DllImport("user32.dll")] private static extern nint GetShellWindow();
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(nint window, out uint processId);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern nint OpenProcess(uint access, bool inherit, uint processId);
    [DllImport("kernel32.dll")] private static extern bool CloseHandle(nint handle);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(nint attributes, int count, int flags, ref nuint size);
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(nint attributes, uint flags, nuint attribute,
        nint value, nuint size, nint previous, nint returnSize);
    [DllImport("kernel32.dll")] private static extern void DeleteProcThreadAttributeList(nint attributes);
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CreateProcess(string application, StringBuilder commandLine,
        nint processAttributes, nint threadAttributes, bool inheritHandles, uint creationFlags,
        nint environment, string directory, ref StartupInfoEx startup, out ProcessInformation process);
}
