using System.Runtime.InteropServices;

namespace ChatGPTRoster.Services;

/// <summary>
/// Reports mouse-down locations without blocking, modifying, or retaining input.
/// UI Automation hit-testing happens later and never runs inside the hook callback.
/// </summary>
internal sealed class ChatGptClickMonitor : IDisposable
{
    private readonly Action<int, int, nint> _clicked;
    private readonly NativeMethods.MouseHookDelegate _callback;
    private readonly MouseHookThread _hookThread;
    private nint _hook;
    private uint _targetProcessId;

    public ChatGptClickMonitor(Action<int, int, nint> clicked)
    {
        _clicked = clicked;
        _callback = OnMouseEvent;
        _hookThread = new MouseHookThread(InstallHook, UninstallHook);
    }

    public void UpdateTarget(nint target)
    {
        NativeMethods.GetWindowThreadProcessId(target, out var processId);
        Volatile.Write(ref _targetProcessId, processId);
    }

    public void Start() => _hookThread.Start();

    private void InstallHook()
    {
        if (_hook != nint.Zero)
        {
            return;
        }

        _hook = NativeMethods.SetWindowsHookEx(
            NativeMethods.WH_MOUSE_LL,
            _callback,
            NativeMethods.GetModuleHandle(null),
            0);
    }

    public void Stop() => _hookThread.Stop();

    private void UninstallHook()
    {
        if (_hook == nint.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = nint.Zero;
    }

    public void Dispose() => Stop();

    private nint OnMouseEvent(int code, nint message, nint data)
    {
        if (code >= 0 && IsButtonDown((uint)message))
        {
            var mouse = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(data);
            var target = NativeMethods.WindowFromPoint(mouse.Point);
            var root = NativeMethods.GetAncestor(target, NativeMethods.GA_ROOT);
            NativeMethods.GetWindowThreadProcessId(root, out var processId);
            if (processId != 0 && processId == Volatile.Read(ref _targetProcessId))
            {
                _clicked(mouse.Point.X, mouse.Point.Y, root);
            }
        }

        return NativeMethods.CallNextHookEx(_hook, code, message, data);
    }

    private static bool IsButtonDown(uint message) =>
        message is NativeMethods.WM_LBUTTONDOWN
            or NativeMethods.WM_RBUTTONDOWN
            or NativeMethods.WM_MBUTTONDOWN
            or NativeMethods.WM_XBUTTONDOWN;

    private static class NativeMethods
    {
        public const int WH_MOUSE_LL = 14;
        public const uint GA_ROOT = 2;
        public const uint WM_LBUTTONDOWN = 0x0201;
        public const uint WM_RBUTTONDOWN = 0x0204;
        public const uint WM_MBUTTONDOWN = 0x0207;
        public const uint WM_XBUTTONDOWN = 0x020B;

        public delegate nint MouseHookDelegate(int code, nint message, nint data);

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MSLLHOOKSTRUCT
        {
            public POINT Point;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public nuint ExtraInfo;
        }

        [DllImport("user32.dll")]
        public static extern nint SetWindowsHookEx(
            int hookType,
            MouseHookDelegate callback,
            nint module,
            uint threadId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWindowsHookEx(nint hook);

        [DllImport("user32.dll")]
        public static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

        [DllImport("user32.dll")]
        public static extern nint WindowFromPoint(POINT point);

        [DllImport("user32.dll")]
        public static extern nint GetAncestor(nint window, uint flags);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(nint window, out uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern nint GetModuleHandle(string? moduleName);
    }
}
