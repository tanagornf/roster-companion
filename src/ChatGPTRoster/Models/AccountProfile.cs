using System.Text.Json.Serialization;

namespace ChatGPTRoster.Models;

public sealed class AccountProfile
{
    public string Id { get; set; } = Guid.NewGuid().ToString("D");
    public string AccountId { get; set; } = string.Empty;
    public string? Subject { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Alias { get; set; } = string.Empty;
    public string Plan { get; set; } = "Unknown";
    public string ProfilePath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsActive { get; set; }
    public UsageSnapshot Usage { get; set; } = new();

    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Alias) ? Email : Alias;
}
