namespace ChatGPTRoster.Services;

public static class ChatGptThemeDetector
{
    public static bool IsDark(IReadOnlyCollection<double> luminances, bool fallback = true)
    {
        if (luminances.Count == 0)
        {
            return fallback;
        }

        var ordered = luminances.Order().ToArray();
        var middle = ordered.Length / 2;
        var median = ordered.Length % 2 == 0
            ? (ordered[middle - 1] + ordered[middle]) / 2
            : ordered[middle];
        return median < 150;
    }

    public static double Luminance(uint colorRef)
    {
        var red = colorRef & 0xff;
        var green = (colorRef >> 8) & 0xff;
        var blue = (colorRef >> 16) & 0xff;
        return red * 0.2126 + green * 0.7152 + blue * 0.0722;
    }
}
