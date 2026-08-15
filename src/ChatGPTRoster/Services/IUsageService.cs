using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public interface IUsageService
{
    Task<UsageSnapshot> GetUsageAsync(AccountProfile profile, CancellationToken cancellationToken = default);
}
