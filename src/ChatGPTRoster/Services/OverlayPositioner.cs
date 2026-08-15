using System.Windows;

namespace ChatGPTRoster.Services;

public sealed record OverlayPlacement(
    double SelectorLeft,
    double SelectorTop,
    double SelectorWidth,
    double SelectorHeight,
    double DropdownLeft,
    double DropdownTop,
    double DropdownMaxHeight);

public static class OverlayPositioner
{
    public const double SelectorOffsetX = 84;
    public const double SelectorOffsetY = 42;
    public const double SelectorPreferredWidth = 122;
    public const double SelectorMinimumWidth = 68;
    public const double SelectorHeight = 34;
    public const double DropdownWidth = 408;
    public const double DropdownGap = 6;
    public const double WorkAreaMargin = 12;

    public static OverlayPlacement? Calculate(
        Rect chatWindowPixels,
        Rect workAreaPixels,
        uint dpi,
        double selectorWidth = SelectorPreferredWidth)
    {
        if (dpi == 0 || chatWindowPixels.Width < 560 || chatWindowPixels.Height < 360)
        {
            return null;
        }

        var scale = dpi / 96d;
        var window = ScaleDown(chatWindowPixels, scale);
        var workArea = ScaleDown(workAreaPixels, scale);
        var width = Math.Clamp(selectorWidth, SelectorMinimumWidth, SelectorPreferredWidth);
        var left = window.Left + SelectorOffsetX;
        var top = window.Top + SelectorOffsetY;
        var dropdownTop = top + SelectorHeight + DropdownGap;
        var dropdownLeft = Math.Clamp(
            left,
            workArea.Left + WorkAreaMargin,
            Math.Max(workArea.Left + WorkAreaMargin, workArea.Right - DropdownWidth - WorkAreaMargin));
        var maxHeight = Math.Max(220, workArea.Bottom - dropdownTop - WorkAreaMargin);

        return new OverlayPlacement(left, top, width, SelectorHeight, dropdownLeft, dropdownTop, maxHeight);
    }

    private static Rect ScaleDown(Rect value, double scale) =>
        new(value.Left / scale, value.Top / scale, value.Width / scale, value.Height / scale);
}
