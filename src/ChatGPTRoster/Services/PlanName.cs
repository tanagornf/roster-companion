namespace ChatGPTRoster.Services;

public static class PlanName
{
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "Unknown";
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "free" => "Free",
            "plus" => "Plus",
            "pro" => "Pro",
            "team" => "Team",
            "business" => "Business",
            "enterprise" => "Enterprise",
            "edu" => "Edu",
            _ => value.Trim()
        };
    }
}
