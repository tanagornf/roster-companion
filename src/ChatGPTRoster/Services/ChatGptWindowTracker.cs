using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace ChatGPTRoster.Services;

public sealed record ChatGptWindowState(
    nint Handle,
    OverlayPlacement Placement,
    bool IsAnchorVisible,
    bool IsDarkTheme);

public sealed class ChatGptWindowTracker : IDisposable
{
    private static readonly TimeSpan FastPositionInterval = TimeSpan.FromMilliseconds(1000d / 60d);
    private static readonly TimeSpan IdlePositionInterval = TimeSpan.FromMilliseconds(100);
    private readonly DispatcherTimer _resyncTimer;
    private readonly DispatcherTimer _positionTimer;
    private readonly WindowEventMonitor _eventMonitor;
    private readonly ChatGptClickMonitor _clickMonitor;
    private readonly ClickHitTestRunner _hitTestRunner = new(ChatGptLayerDetector.HitTest);
    private readonly Dispatcher _dispatcher;
    private readonly int _currentProcessId = Environment.ProcessId;
    private nint _lastHandle;
    private ChatGptWindowState? _lastState;
    private string? _lastDiagnostic;
    private bool _clickMenuSuppressed;
    private bool _settingsRouteActive;
    private int _routeCheckGeneration;
    private int _trackingGeneration;
    private int _clickGeneration;
    private int _fastPositionFrames;
    private bool _started;

    public ChatGptWindowTracker(TimeSpan? interval = null)
    {
        _dispatcher = Dispatcher.CurrentDispatcher;
        _resyncTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = interval ?? TimeSpan.FromSeconds(1)
        };
        _resyncTimer.Tick += (_, _) => Poll();
        _positionTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = IdlePositionInterval
        };
        _positionTimer.Tick += (_, _) => TrackPosition();
        _eventMonitor = new WindowEventMonitor(Dispatcher.CurrentDispatcher, OnWindowEvent);
        _clickMonitor = new ChatGptClickMonitor(OnChatGptClick);
    }

    public event EventHandler<ChatGptWindowState?>? StateChanged;

    public ChatGptWindowState? CurrentState => _lastState;

    public void Start()
    {
        Interlocked.Increment(ref _trackingGeneration);
        _hitTestRunner.Invalidate();
        _started = true;
        Poll();
        _eventMonitor.Start();
        _clickMonitor.Start();
        _positionTimer.Start();
        _resyncTimer.Start();
        ScheduleRouteRefresh(_lastHandle, TimeSpan.FromMilliseconds(100));
    }

    public void Stop()
    {
        Interlocked.Increment(ref _trackingGeneration);
        _hitTestRunner.Invalidate();
        _started = false;
        Interlocked.Increment(ref _routeCheckGeneration);
        _eventMonitor.Stop();
        _clickMonitor.Stop();
        _positionTimer.Stop();
        _resyncTimer.Stop();
    }

    public bool IsCompanionForeground()
    {
        var foreground = NativeMethods.GetForegroundWindow();
        if (foreground == nint.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(foreground, out var processId);
        return processId == _currentProcessId;
    }

    public void Dispose()
    {
        Stop();
        _clickMonitor.Dispose();
    }

    private void OnWindowEvent(uint eventId, nint eventWindow)
    {
        if (eventId is NativeMethods.EVENT_SYSTEM_MENUSTART or NativeMethods.EVENT_SYSTEM_MENUPOPUPSTART)
        {
            return;
        }

        if (eventId is NativeMethods.EVENT_SYSTEM_MENUEND or NativeMethods.EVENT_SYSTEM_MENUPOPUPEND)
        {
            // A late File/Edit hit must not hide the overlay again after dismissal.
            _clickGeneration++;
            _hitTestRunner.Invalidate();
            if (_clickMenuSuppressed
                && (eventWindow == nint.Zero || IsPackagedChatGptProcess(eventWindow)))
            {
                _clickMenuSuppressed = false;
                Poll();
            }

            return;
        }

        if (eventId is NativeMethods.EVENT_SYSTEM_MOVESIZESTART or NativeMethods.EVENT_OBJECT_LOCATIONCHANGE)
        {
            _fastPositionFrames = 30;
            _positionTimer.Interval = FastPositionInterval;
            TrackPosition();
            return;
        }

        if (eventId == NativeMethods.EVENT_SYSTEM_MOVESIZEEND)
        {
            _fastPositionFrames = 0;
            _positionTimer.Interval = IdlePositionInterval;
            Poll();
            return;
        }

        Poll();
    }

    private void OnChatGptClick(int x, int y, nint eventWindow)
    {
        // Called by the global mouse hook: never query UI Automation here.
        var generation = Volatile.Read(ref _trackingGeneration);
        _dispatcher.BeginInvoke(() => ProcessChatGptClickAsync(x, y, eventWindow, generation));
    }

    private async Task ProcessChatGptClickAsync(int x, int y, nint eventWindow, int generation)
    {
        if (!_started || eventWindow != _lastHandle || generation != _trackingGeneration)
        {
            return;
        }

        try
        {
            var clickGeneration = ++_clickGeneration;
            var hit = await _hitTestRunner.TryHitTestAsync(x, y);
            if (hit is { } value && _started && generation == _trackingGeneration
                && clickGeneration == _clickGeneration)
            {
                HandleChatGptClick(value, eventWindow);
            }
        }
        catch (Exception error)
        {
            // Accessibility is best effort; an unavailable provider must not
            // bring down the companion or escape into a native input callback.
            Trace($"ChatGPT click hit-test failed: {error.GetType().Name}.");
        }
    }

    private void HandleChatGptClick(ChatGptControlHit hit, nint eventWindow)
    {
        if (!_started || eventWindow != _lastHandle)
        {
            return;
        }

        switch (hit)
        {
            case ChatGptControlHit.File:
            case ChatGptControlHit.Edit:
                _clickMenuSuppressed = true;
                if (_lastState is { IsAnchorVisible: true } menuState)
                {
                    Publish(menuState with { IsAnchorVisible = false });
                }

                break;
            case ChatGptControlHit.Settings:
                _settingsRouteActive = true;
                if (_lastState is { IsAnchorVisible: true } settingsState)
                {
                    Publish(settingsState with { IsAnchorVisible = false });
                }

                break;
            case ChatGptControlHit.ReturnToMain:
                break;
            default:
                if (_clickMenuSuppressed)
                {
                    _clickMenuSuppressed = false;
                    Poll();
                }

                break;
        }

        ScheduleRouteRefresh(eventWindow, TimeSpan.FromMilliseconds(180));
    }

    private void ScheduleRouteRefresh(nint handle, TimeSpan delay)
    {
        if (!_started || handle == nint.Zero)
        {
            return;
        }

        var generation = Interlocked.Increment(ref _routeCheckGeneration);
        _ = RefreshRouteStateAsync(handle, delay, generation);
    }

    private async Task RefreshRouteStateAsync(nint handle, TimeSpan delay, int generation)
    {
        await Task.Delay(delay);
        if (!_started || generation != _routeCheckGeneration)
        {
            return;
        }

        var route = await Task.Run(() => ChatGptRouteDetector.Detect(handle));
        if (route == ChatGptRouteState.Unknown)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(220));
            if (!_started || generation != _routeCheckGeneration)
            {
                return;
            }

            route = await Task.Run(() => ChatGptRouteDetector.Detect(handle));
        }

        if (!_started || generation != _routeCheckGeneration || handle != _lastHandle)
        {
            return;
        }

        switch (route)
        {
            case ChatGptRouteState.Settings:
                _settingsRouteActive = true;
                if (_lastState is { IsAnchorVisible: true } settingsState)
                {
                    Publish(settingsState with { IsAnchorVisible = false });
                }

                break;
            case ChatGptRouteState.Main:
                if (_settingsRouteActive)
                {
                    _settingsRouteActive = false;
                    Poll();
                }

                break;
        }
    }

    private void TrackPosition()
    {
        if (_lastState is not { } state)
        {
            return;
        }

        var handle = state.Handle;
        if (NativeMethods.IsIconic(handle)
            || !NativeMethods.IsWindowVisible(handle)
            || !TryGetWindowRect(handle, out var bounds)
            || !TryGetWorkArea(handle, out var workArea))
        {
            Publish(null);
            return;
        }

        var dpi = NativeMethods.GetDpiForWindow(handle);
        var placement = OverlayPositioner.Calculate(bounds, workArea, dpi);
        if (placement is null)
        {
            Publish(null);
            return;
        }

        if (placement != state.Placement)
        {
            _fastPositionFrames = 30;
            _positionTimer.Interval = FastPositionInterval;
            Publish(state with { Placement = placement });
        }
        else if (_fastPositionFrames > 0 && --_fastPositionFrames == 0)
        {
            _positionTimer.Interval = IdlePositionInterval;
        }
    }

    private void Poll()
    {
        var handle = FindMainWindow();
        if (handle == nint.Zero
            || NativeMethods.IsIconic(handle)
            || !NativeMethods.IsWindowVisible(handle)
            || !TryGetWindowRect(handle, out var bounds)
            || !TryGetWorkArea(handle, out var workArea))
        {
            Trace("No usable ChatGPT main window was found.");
            Publish(null);
            return;
        }

        var dpi = NativeMethods.GetDpiForWindow(handle);
        var placement = OverlayPositioner.Calculate(bounds, workArea, dpi);
        if (placement is null)
        {
            Trace($"ChatGPT window {handle} is too small for the companion.");
            Publish(null);
            return;
        }

        var anchorX = (int)Math.Round((placement.SelectorLeft + placement.SelectorWidth / 2) * dpi / 96d);
        var anchorY = (int)Math.Round((placement.SelectorTop + placement.SelectorHeight / 2) * dpi / 96d);
        var isAnchorVisible = !_clickMenuSuppressed
            && !_settingsRouteActive
            && !IsCoveredAtPoint(handle, anchorX, anchorY);
        var isDark = isAnchorVisible
            ? SampleDarkTheme(handle, bounds, dpi, _lastState?.IsDarkTheme ?? true)
            : _lastState?.IsDarkTheme ?? true;
        Trace($"ChatGPT window {handle}; anchorVisible={isAnchorVisible}; bounds={bounds}; dpi={dpi}.");
        Publish(new ChatGptWindowState(handle, placement, isAnchorVisible, isDark));
    }

    private bool IsCoveredAtPoint(nint chatHandle, int x, int y)
    {
        for (var candidate = NativeMethods.GetTopWindow(nint.Zero);
             candidate != nint.Zero;
             candidate = NativeMethods.GetWindow(candidate, NativeMethods.GW_HWNDNEXT))
        {
            if (candidate == chatHandle)
            {
                return false;
            }

            if (!NativeMethods.IsWindowVisible(candidate) || NativeMethods.IsIconic(candidate))
            {
                continue;
            }

            NativeMethods.GetWindowThreadProcessId(candidate, out var processId);
            if (processId == _currentProcessId || IsCloaked(candidate))
            {
                continue;
            }

            if (!NativeMethods.GetWindowRect(candidate, out var rect)
                || x < rect.Left || x >= rect.Right || y < rect.Top || y >= rect.Bottom)
            {
                continue;
            }

            var extendedStyle = NativeMethods.GetWindowLongPtr(candidate, NativeMethods.GWL_EXSTYLE).ToInt64();
            if ((extendedStyle & NativeMethods.WS_EX_TRANSPARENT) != 0)
            {
                continue;
            }

            return true;
        }

        return true;
    }

    private static bool IsCloaked(nint handle) =>
        NativeMethods.DwmGetWindowAttribute(
            handle,
            NativeMethods.DWMWA_CLOAKED,
            out int cloaked,
            sizeof(int)) == 0 && cloaked != 0;

    private void Trace(string message)
    {
        if (message == _lastDiagnostic)
        {
            return;
        }

        _lastDiagnostic = message;
        var path = Environment.GetEnvironmentVariable("ROSTER_COMPANION_DEBUG_LOG");
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
        }
        catch
        {
        }
    }

    private void Publish(ChatGptWindowState? state)
    {
        if (Equals(_lastState, state))
        {
            return;
        }

        _lastState = state;
        StateChanged?.Invoke(this, state);
    }

    private nint FindMainWindow()
    {
        if (_lastHandle != nint.Zero && IsVerifiedMainWindow(_lastHandle))
        {
            return _lastHandle;
        }

        nint result = nint.Zero;
        NativeMethods.EnumWindows((handle, _) =>
        {
            if (!IsVerifiedMainWindow(handle))
            {
                return true;
            }

            result = handle;
            return false;
        }, nint.Zero);
        _lastHandle = result;
        _clickMonitor.UpdateTarget(_lastHandle);
        return result;
    }

    private static bool IsVerifiedMainWindow(nint handle)
    {
        if (!NativeMethods.IsWindowVisible(handle))
        {
            return false;
        }

        var title = new StringBuilder(128);
        NativeMethods.GetWindowText(handle, title, title.Capacity);
        if (!title.ToString().Equals("ChatGPT", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return IsPackagedChatGptProcess(handle);
    }

    private static bool IsPackagedChatGptProcess(nint handle)
    {
        NativeMethods.GetWindowThreadProcessId(handle, out var processId);
        if (processId == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById((int)processId);
            var capacity = 2048;
            var path = new StringBuilder(capacity);
            if (!NativeMethods.QueryFullProcessImageName(process.Handle, 0, path, ref capacity))
            {
                return false;
            }

            return DesktopProcessService.IsPackagedDesktopPath(path.ToString());
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    private static bool TryGetWindowRect(nint handle, out Rect bounds)
    {
        if (NativeMethods.GetClientRect(handle, out var client))
        {
            var origin = new NativeMethods.POINT();
            if (NativeMethods.ClientToScreen(handle, ref origin))
            {
                bounds = new Rect(
                    origin.X,
                    origin.Y,
                    client.Right - client.Left,
                    client.Bottom - client.Top);
                if (bounds.Width > 0 && bounds.Height > 0)
                {
                    return true;
                }
            }
        }

        NativeMethods.RECT native;
        if (NativeMethods.DwmGetWindowAttribute(
                handle,
                NativeMethods.DWMWA_EXTENDED_FRAME_BOUNDS,
                out native,
                Marshal.SizeOf<NativeMethods.RECT>()) != 0
            && !NativeMethods.GetWindowRect(handle, out native))
        {
            bounds = Rect.Empty;
            return false;
        }

        bounds = new Rect(native.Left, native.Top, native.Right - native.Left, native.Bottom - native.Top);
        return bounds.Width > 0 && bounds.Height > 0;
    }

    private static bool TryGetWorkArea(nint handle, out Rect workArea)
    {
        var monitor = NativeMethods.MonitorFromWindow(handle, NativeMethods.MONITOR_DEFAULTTONEAREST);
        var info = new NativeMethods.MONITORINFO { Size = Marshal.SizeOf<NativeMethods.MONITORINFO>() };
        if (monitor == nint.Zero || !NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            workArea = Rect.Empty;
            return false;
        }

        workArea = new Rect(
            info.WorkArea.Left,
            info.WorkArea.Top,
            info.WorkArea.Right - info.WorkArea.Left,
            info.WorkArea.Bottom - info.WorkArea.Top);
        return true;
    }

    private static bool SampleDarkTheme(nint handle, Rect bounds, uint dpi, bool fallback)
    {
        var forcedTheme = Environment.GetEnvironmentVariable("ROSTER_COMPANION_FORCE_THEME");
        if (forcedTheme?.Equals("dark", StringComparison.OrdinalIgnoreCase) == true)
        {
            return true;
        }

        if (forcedTheme?.Equals("light", StringComparison.OrdinalIgnoreCase) == true)
        {
            return false;
        }

        var dc = NativeMethods.GetWindowDC(nint.Zero);
        if (dc == nint.Zero)
        {
            return fallback;
        }

        try
        {
            var scale = dpi / 96d;
            var centers = new (double X, double Y)[]
            {
                (16, 16),
                (250, 92),
                (18, Math.Max(140, bounds.Height / scale * 0.35)),
                (250, Math.Max(210, bounds.Height / scale * 0.55)),
                (18, Math.Max(280, bounds.Height / scale * 0.78))
            };
            var luminances = new List<double>(centers.Length * 9);
            foreach (var center in centers)
            {
                foreach (var offsetX in new[] { -4, 0, 4 })
                {
                    foreach (var offsetY in new[] { -4, 0, 4 })
                    {
                        var x = (int)Math.Clamp(bounds.Left + (center.X + offsetX) * scale, bounds.Left + 1, bounds.Right - 2);
                        var y = (int)Math.Clamp(bounds.Top + (center.Y + offsetY) * scale, bounds.Top + 1, bounds.Bottom - 2);
                        var color = NativeMethods.GetPixel(dc, x, y);
                        if (color != uint.MaxValue)
                        {
                            luminances.Add(ChatGptThemeDetector.Luminance(color));
                        }
                    }
                }
            }

            return ChatGptThemeDetector.IsDark(luminances, fallback);
        }
        finally
        {
            NativeMethods.ReleaseDC(nint.Zero, dc);
        }
    }

    public static void KeepAboveChatGpt(Window window)
    {
        var handle = new WindowInteropHelper(window).Handle;
        if (handle != nint.Zero)
        {
            NativeMethods.SetWindowPos(
                handle,
                NativeMethods.HWND_TOPMOST,
                0,
                0,
                0,
                0,
                NativeMethods.SWP_NOMOVE | NativeMethods.SWP_NOSIZE | NativeMethods.SWP_NOACTIVATE);
        }
    }

    private static class NativeMethods
    {
        public const uint GW_OWNER = 4;
        public const uint GW_HWNDNEXT = 2;
        public const uint EVENT_SYSTEM_MENUSTART = 0x0004;
        public const uint EVENT_SYSTEM_MENUEND = 0x0005;
        public const uint EVENT_SYSTEM_MENUPOPUPSTART = 0x0006;
        public const uint EVENT_SYSTEM_MENUPOPUPEND = 0x0007;
        public const uint EVENT_SYSTEM_MOVESIZESTART = 0x000A;
        public const uint EVENT_SYSTEM_MOVESIZEEND = 0x000B;
        public const uint EVENT_OBJECT_SHOW = 0x8002;
        public const uint EVENT_OBJECT_HIDE = 0x8003;
        public const uint EVENT_OBJECT_LOCATIONCHANGE = 0x800B;
        public const int GWL_EXSTYLE = -20;
        public const long WS_EX_TRANSPARENT = 0x00000020L;
        public const uint MONITOR_DEFAULTTONEAREST = 2;
        public const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
        public const int DWMWA_CLOAKED = 14;
        public const uint SWP_NOSIZE = 0x0001;
        public const uint SWP_NOMOVE = 0x0002;
        public const uint SWP_NOACTIVATE = 0x0010;
        public static readonly nint HWND_TOPMOST = new(-1);

        public delegate bool EnumWindowsProc(nint handle, nint parameter);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct POINT
        {
            public int X;
            public int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool EnumWindows(EnumWindowsProc callback, nint parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsWindowVisible(nint handle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool IsIconic(nint handle);

        [DllImport("user32.dll")]
        public static extern nint GetWindow(nint handle, uint command);

        [DllImport("user32.dll")]
        public static extern nint GetTopWindow(nint handle);

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
        public static extern nint GetWindowLongPtr(nint handle, int index);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        public static extern int GetWindowText(nint handle, StringBuilder text, int capacity);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(nint handle, out uint processId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool QueryFullProcessImageName(
            nint process,
            int flags,
            StringBuilder path,
            ref int capacity);

        [DllImport("user32.dll")]
        public static extern nint GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(nint handle);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(nint handle, int attribute, out RECT value, int size);

        [DllImport("dwmapi.dll")]
        public static extern int DwmGetWindowAttribute(nint handle, int attribute, out int value, int size);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetWindowRect(nint handle, out RECT value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(nint handle, out RECT value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(nint handle, ref POINT point);

        [DllImport("user32.dll")]
        public static extern nint MonitorFromWindow(nint handle, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetMonitorInfo(nint monitor, ref MONITORINFO info);

        [DllImport("user32.dll")]
        public static extern nint GetWindowDC(nint handle);

        [DllImport("gdi32.dll")]
        public static extern uint GetPixel(nint dc, int x, int y);

        [DllImport("user32.dll")]
        public static extern int ReleaseDC(nint handle, nint dc);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool SetWindowPos(
            nint handle,
            nint insertAfter,
            int x,
            int y,
            int width,
            int height,
            uint flags);
    }
}
