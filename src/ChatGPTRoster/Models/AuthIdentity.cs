namespace ChatGPTRoster.Models;

public sealed record AuthIdentity(
    string Email,
    string AccountId,
    string? Subject,
    string? Plan);
