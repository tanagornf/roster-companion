namespace ChatGPTRoster.Models;

public sealed class UsageWindow
{
    public string Label { get; set; } = string.Empty;
    public double RemainingPercent { get; set; }
    public DateTimeOffset? ResetsAt { get; set; }
}
