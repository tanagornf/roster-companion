namespace ChatGPTRoster.Services;

public sealed class UsageRefreshPolicy
{
    public static readonly TimeSpan ActiveInterval = TimeSpan.FromMinutes(10);
    public static readonly TimeSpan InactiveInterval = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan[] RetryIntervals =
    [
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(2),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(10),
        TimeSpan.FromMinutes(30)
    ];

    public static bool IsDue(DateTimeOffset? lastUpdatedAt, bool isActive, DateTimeOffset now) =>
        lastUpdatedAt is null || now - lastUpdatedAt >= (isActive ? ActiveInterval : InactiveInterval);

    public static TimeSpan RetryDelay(int consecutiveFailures) =>
        RetryIntervals[Math.Clamp(consecutiveFailures - 1, 0, RetryIntervals.Length - 1)];
}
