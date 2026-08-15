using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Threading;

namespace ChatGPTRoster.Services;

/// <summary>
/// Converts the Win32 accessibility window event stream into coalesced callbacks
/// on the WPF dispatcher. This keeps the overlay attached without continuously polling.
/// </summary>
internal sealed class WindowEventMonitor : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Action<uint, nint> _changed;
    private readonly NativeMethods.WinEventDelegate _callback;
    private readonly List<nint> _hooks = [];
    private GCHandle _callbackHandle;
    private int _callbackPending;
    private bool _started;

    public WindowEventMonitor(Dispatcher dispatcher, Action<uint, nint> changed)
    {
        _dispatcher = dispatcher;
        _changed = changed;
        _callback = OnWinEvent;
    }

    public void Start()
    {
        if (_started)
        {
            return;
        }

        _started = true;
        _callbackHandle = GCHandle.Alloc(_callback);

        Install(NativeMethods.EVENT_SYSTEM_FOREGROUND);
        Install(NativeMethods.EVENT_SYSTEM_MENUSTART);
        Install(NativeMethods.EVENT_SYSTEM_MENUEND);
        Install(NativeMethods.EVENT_SYSTEM_MENUPOPUPSTART);
        Install(NativeMethods.EVENT_SYSTEM_MENUPOPUPEND);
        Install(NativeMethods.EVENT_SYSTEM_MOVESIZESTART);
        Install(NativeMethods.EVENT_SYSTEM_MOVESIZEEND);
        Install(NativeMethods.EVENT_SYSTEM_MINIMIZESTART);
        Install(NativeMethods.EVENT_SYSTEM_MINIMIZEEND);
        Install(NativeMethods.EVENT_OBJECT_DESTROY);
        Install(NativeMethods.EVENT_OBJECT_SHOW);
        Install(NativeMethods.EVENT_OBJECT_HIDE);
        Install(NativeMethods.EVENT_OBJECT_REORDER);
        Install(NativeMethods.EVENT_OBJECT_LOCATIONCHANGE);
    }

    public void Stop()
    {
        if (!_started)
        {
            return;
        }

        _started = false;
        foreach (var hook in _hooks)
        {
            NativeMethods.UnhookWinEvent(hook);
        }

        _hooks.Clear();
        if (_callbackHandle.IsAllocated)
        {
            _callbackHandle.Free();
        }
    }

    public void Dispose() => Stop();

    private void Install(uint eventId)
    {
        var hook = NativeMethods.SetWinEventHook(
            eventId,
            eventId,
            nint.Zero,
            _callback,
            0,
            0,
            NativeMethods.WINEVENT_OUTOFCONTEXT | NativeMethods.WINEVENT_SKIPOWNPROCESS);
        if (hook != nint.Zero)
        {
            _hooks.Add(hook);
        }
    }

    private void OnWinEvent(
        nint hook,
        uint eventId,
        nint window,
        int objectId,
        int childId,
        uint eventThread,
        uint eventTime)
    {
        if (!_started || window == nint.Zero)
        {
            return;
        }

        if (eventId >= NativeMethods.EVENT_OBJECT_DESTROY && objectId != NativeMethods.OBJID_WINDOW)
        {
            return;
        }

        if (Interlocked.Exchange(ref _callbackPending, 1) != 0)
        {
            return;
        }

        _dispatcher.BeginInvoke(() =>
        {
            Interlocked.Exchange(ref _callbackPending, 0);
            if (_started)
            {
                _changed(eventId, window);
            }
        }, DispatcherPriority.Render);
    }

    private static class NativeMethods
    {
        public const uint EVENT_SYSTEM_FOREGROUND = 0x0003;
        public const uint EVENT_SYSTEM_MENUSTART = 0x0004;
        public const uint EVENT_SYSTEM_MENUEND = 0x0005;
        public const uint EVENT_SYSTEM_MENUPOPUPSTART = 0x0006;
        public const uint EVENT_SYSTEM_MENUPOPUPEND = 0x0007;
        public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
        public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        public const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
        public const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        public const uint EVENT_OBJECT_DESTROY = 0x8001;
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const uint EVENT_OBJECT_HIDE = 0x8003;
        public const uint EVENT_OBJECT_REORDER = 0x8004;
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const int OBJID_WINDOW = 0;
        public const uint WINEVENT_OUTOFCONTEXT = 0;
        public const uint WINEVENT_SKIPOWNPROCESS = 0x0002;

        public delegate void WinEventDelegate(
            nint hook,
            uint eventId,
            nint window,
            int objectId,
            int childId,
            uint eventThread,
            uint eventTime);

        [DllImport("user32.dll")]
        public static extern nint SetWinEventHook(
            uint eventMin,
            uint eventMax,
            nint module,
            WinEventDelegate callback,
            uint processId,
            uint threadId,
            uint flags);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool UnhookWinEvent(nint hook);
    }
}
