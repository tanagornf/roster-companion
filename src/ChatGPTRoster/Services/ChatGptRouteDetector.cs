using System.Windows.Automation;
using System.Runtime.InteropServices;

namespace ChatGPTRoster.Services;

internal enum ChatGptRouteState
{
    Unknown,
    Main,
    Settings
}

internal static class ChatGptRouteDetector
{
    private static readonly Condition MainRouteCondition = new PropertyCondition(
        AutomationElement.NameProperty,
        "Switch mode, current mode: Codex");

    public static ChatGptRouteState Detect(nint chatGptWindow)
    {
        if (chatGptWindow == nint.Zero)
        {
            return ChatGptRouteState.Unknown;
        }

        try
        {
            // ChatGPT keeps the main Codex accessibility subtree mounted behind
            // Settings. Probe the visible search field before consulting that tree.
            if (HasVisibleSettingsSearch(chatGptWindow))
            {
                return ChatGptRouteState.Settings;
            }

            var root = AutomationElement.FromHandle(chatGptWindow);
            return root.FindFirst(TreeScope.Descendants, MainRouteCondition) is not null
                ? ChatGptRouteState.Main
                : ChatGptRouteState.Unknown;
        }
        catch (ElementNotAvailableException)
        {
            return ChatGptRouteState.Unknown;
        }
        catch (InvalidOperationException)
        {
            return ChatGptRouteState.Unknown;
        }
        catch (UnauthorizedAccessException)
        {
            return ChatGptRouteState.Unknown;
        }
    }

    private static bool HasVisibleSettingsSearch(nint chatGptWindow)
    {
        if (!NativeMethods.GetClientRect(chatGptWindow, out var client))
        {
            return false;
        }

        var origin = new NativeMethods.Point();
        if (!NativeMethods.ClientToScreen(chatGptWindow, ref origin))
        {
            return false;
        }

        var scale = NativeMethods.GetDpiForWindow(chatGptWindow) / 96d;
        var probe = new System.Windows.Point(
            origin.X + Math.Min(80 * scale, Math.Max(1, client.Right - 1)),
            origin.Y + Math.Min(98 * scale, Math.Max(1, client.Bottom - 1)));
        var element = AutomationElement.FromPoint(probe);
        return element.Current.AutomationId.Equals("settings-search", StringComparison.OrdinalIgnoreCase);
    }

    private static class NativeMethods
    {
        [StructLayout(LayoutKind.Sequential)]
        public struct Rect
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct Point
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(nint handle, out Rect value);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool ClientToScreen(nint handle, ref Point point);

        [DllImport("user32.dll")]
        public static extern uint GetDpiForWindow(nint handle);
    }
}
