using System.IO;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public interface IActiveAccountDiscoveryService
{
    Task<IReadOnlyList<AccountProfile>> SynchronizeAsync(
        IReadOnlyCollection<AccountProfile> profiles,
        CancellationToken cancellationToken = default);
}

public sealed class ActiveAccountDiscoveryService : IActiveAccountDiscoveryService
{
    private readonly AppPaths _paths;
    private readonly IAuthFileReader _authFileReader;

    public ActiveAccountDiscoveryService(AppPaths paths, IAuthFileReader authFileReader)
    {
        _paths = paths;
        _authFileReader = authFileReader;
    }

    public async Task<IReadOnlyList<AccountProfile>> SynchronizeAsync(
        IReadOnlyCollection<AccountProfile> profiles,
        CancellationToken cancellationToken = default)
    {
        var synchronized = profiles.ToList();
        foreach (var profile in synchronized)
        {
            profile.IsActive = false;
        }

        var ambientAuthPath = Path.Combine(_paths.AmbientCodexHome, "auth.json");
        if (!File.Exists(ambientAuthPath))
        {
            return synchronized;
        }

        AuthIdentity activeIdentity;
        try
        {
            activeIdentity = await _authFileReader.ReadIdentityAsync(_paths.AmbientCodexHome, cancellationToken);
        }
        catch (AuthFileException)
        {
            return synchronized;
        }

        var match = synchronized.FirstOrDefault(profile => Matches(profile, activeIdentity));
        if (match is not null)
        {
            match.IsActive = true;
            match.Email = activeIdentity.Email;
            match.Plan = PlanName.Normalize(activeIdentity.Plan ?? match.Plan);
            match.Subject ??= activeIdentity.Subject;
            return synchronized;
        }

        Directory.CreateDirectory(_paths.ProfilesDirectory);
        var profileId = Guid.NewGuid().ToString("D");
        var profilePath = Path.Combine(_paths.ProfilesDirectory, profileId);
        Directory.CreateDirectory(profilePath);
        try
        {
            await AtomicFile.CopyAsync(
                ambientAuthPath,
                Path.Combine(profilePath, "auth.json"),
                cancellationToken);
            synchronized.Add(new AccountProfile
            {
                Id = profileId,
                AccountId = activeIdentity.AccountId,
                Subject = activeIdentity.Subject,
                Email = activeIdentity.Email,
                Plan = PlanName.Normalize(activeIdentity.Plan),
                ProfilePath = profilePath,
                CreatedAt = DateTimeOffset.UtcNow,
                IsActive = true
            });
            return synchronized;
        }
        catch
        {
            if (Directory.Exists(profilePath))
            {
                Directory.Delete(profilePath, recursive: true);
            }

            throw;
        }
    }

    private static bool Matches(AccountProfile profile, AuthIdentity identity)
    {
        if (!profile.AccountId.Equals(identity.AccountId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(profile.Subject)
               || string.IsNullOrWhiteSpace(identity.Subject)
               || profile.Subject.Equals(identity.Subject, StringComparison.Ordinal);
    }
}
