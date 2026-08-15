using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace ChatGPTRoster.Services;

/// <summary>
/// Observes mouse-down messages while the dropdown is open. It never blocks or
/// modifies input and retains no pointer history.
/// </summary>
internal sealed class OutsideClickMonitor : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action _outsideClick;
    private readonly NativeMethods.MouseHookDelegate _callback;
    private readonly uint _currentProcessId = (uint)Environment.ProcessId;
    private nint _hook;
    private int _callbackPending;

    public OutsideClickMonitor(Dispatcher dispatcher, Action outsideClick)
    {
        _dispatcher = dispatcher;
        _outsideClick = outsideClick;
        _callback = OnMouseEvent;
    }

    public void Start()
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

    public void Stop()
    {
        if (_hook == nint.Zero)
        {
            return;
        }

        NativeMethods.UnhookWindowsHookEx(_hook);
        _hook = nint.Zero;
        Interlocked.Exchange(ref _callbackPending, 0);
    }

    public void Dispose() => Stop();

    private nint OnMouseEvent(int code, nint message, nint data)
    {
        if (code >= 0 && IsButtonDown((uint)message))
        {
            var mouse = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(data);
            var target = NativeMethods.WindowFromPoint(mouse.Point);
            NativeMethods.GetWindowThreadProcessId(target, out var targetProcessId);

            if (targetProcessId != _currentProcessId
                && Interlocked.Exchange(ref _callbackPending, 1) == 0)
            {
                _dispatcher.BeginInvoke(() =>
                {
                    Interlocked.Exchange(ref _callbackPending, 0);
                    _outsideClick();
                }, DispatcherPriority.Send);
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
        public static extern uint GetWindowThreadProcessId(nint window, out uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
        public static extern nint GetModuleHandle(string? moduleName);
    }
}
