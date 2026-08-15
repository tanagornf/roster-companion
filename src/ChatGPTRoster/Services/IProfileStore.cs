using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public interface IProfileStore
{
    Task<IReadOnlyList<AccountProfile>> LoadAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(IReadOnlyCollection<AccountProfile> profiles, CancellationToken cancellationToken = default);
}
