namespace ChatGPTRoster.Models;

public sealed class UsageSnapshot
{
    public string? Plan { get; set; }
    public UsageWindow? ShortTerm { get; set; }
    public UsageWindow? Weekly { get; set; }
    public DateTimeOffset? LastUpdatedAt { get; set; }
    public string? ErrorMessage { get; set; }
}
