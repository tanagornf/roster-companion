using System.IO;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public sealed class AdaptiveAccountEnrollmentService : IAccountEnrollmentService
{
    private static readonly TimeSpan DesktopLoginTimeout = TimeSpan.FromMinutes(10);
    private readonly AppPaths _paths;
    private readonly ICodexCliLocator _cliLocator;
    private readonly IAuthFileReader _authFileReader;
    private readonly IDesktopProcessService _desktopProcessService;
    private readonly IDesktopSessionService _desktopSessionService;
    private readonly Func<AccountProfile, bool> _confirmDesktopLogin;

    public AdaptiveAccountEnrollmentService(
        AppPaths paths,
        ICodexCliLocator cliLocator,
        IAuthFileReader authFileReader,
        IDesktopProcessService desktopProcessService,
        IDesktopSessionService desktopSessionService,
        Func<AccountProfile, bool> confirmDesktopLogin)
    {
        _paths = paths;
        _cliLocator = cliLocator;
        _authFileReader = authFileReader;
        _desktopProcessService = desktopProcessService;
        _desktopSessionService = desktopSessionService;
        _confirmDesktopLogin = confirmDesktopLogin;
    }

    public async Task<AccountProfile> EnrollAsync(
        AccountProfile? currentProfile = null,
        CancellationToken cancellationToken = default)
    {
        if (_cliLocator.FindExecutable() is not null)
        {
            try
            {
                return await new CodexAccountEnrollmentService(_paths, _cliLocator, _authFileReader)
                    .EnrollAsync(currentProfile, cancellationToken);
            }
            catch (CodexCliLaunchException)
            {
                if (currentProfile is null || !_confirmDesktopLogin(currentProfile))
                {
                    throw;
                }

                return await EnrollThroughDesktopAsync(currentProfile, cancellationToken);
            }
        }

        if (currentProfile is null)
        {
            throw new AccountEnrollmentException(
                "The Codex CLI is unavailable and Roster Companion could not identify an active account to back up. Open ChatGPT, sign in, and try again.");
        }

        if (!_confirmDesktopLogin(currentProfile))
        {
            throw new OperationCanceledException(cancellationToken);
        }

        return await EnrollThroughDesktopAsync(currentProfile, cancellationToken);
    }

    private async Task<AccountProfile> EnrollThroughDesktopAsync(
        AccountProfile currentProfile,
        CancellationToken cancellationToken)
    {
        var originalProcess = await _desktopProcessService.CaptureAsync(cancellationToken);
        var ambientAuth = Path.Combine(_paths.AmbientCodexHome, "auth.json");
        if (!File.Exists(ambientAuth))
        {
            throw new AccountEnrollmentException("The active ChatGPT credential could not be found, so desktop login was not started.");
        }

        Directory.CreateDirectory(_paths.BackupsDirectory);
        var recoveryAuth = Path.Combine(
            _paths.BackupsDirectory,
            $"enrollment-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json");
        var profileId = Guid.NewGuid().ToString("D");
        var profilePath = Path.Combine(_paths.ProfilesDirectory, profileId);
        var temporaryProfile = new AccountProfile { Id = profileId, ProfilePath = profilePath };
        var closedForPreparation = false;

        try
        {
            if (originalProcess.WasRunning)
            {
                await _desktopProcessService.CloseAsync(cancellationToken);
                closedForPreparation = true;
            }

            await _desktopSessionService.BackupAsync(currentProfile, cancellationToken);
            await AtomicFile.CopyAsync(ambientAuth, Path.Combine(currentProfile.ProfilePath, "auth.json"), cancellationToken);
            await AtomicFile.CopyAsync(ambientAuth, recoveryAuth, cancellationToken);

            Directory.CreateDirectory(profilePath);
            File.Delete(ambientAuth);
            await _desktopSessionService.RestoreOrClearAsync(temporaryProfile, cancellationToken);

            if (originalProcess.WasRunning)
            {
                await _desktopProcessService.ReopenAsync(originalProcess, cancellationToken);
                closedForPreparation = false;
            }

            var identity = await WaitForDifferentIdentityAsync(currentProfile, cancellationToken);

            var signedInProcess = await _desktopProcessService.CaptureAsync(cancellationToken);
            if (signedInProcess.WasRunning)
            {
                await _desktopProcessService.CloseAsync(cancellationToken);
            }

            await AtomicFile.CopyAsync(ambientAuth, Path.Combine(profilePath, "auth.json"), cancellationToken);
            var profile = new AccountProfile
            {
                Id = profileId,
                AccountId = identity.AccountId,
                Subject = identity.Subject,
                Email = identity.Email,
                Plan = PlanName.Normalize(identity.Plan),
                ProfilePath = profilePath,
                CreatedAt = DateTimeOffset.UtcNow,
                IsActive = true
            };
            await _desktopSessionService.BackupAsync(profile, cancellationToken);

            if (signedInProcess.WasRunning)
            {
                await _desktopProcessService.ReopenAsync(signedInProcess, cancellationToken);
            }

            return profile;
        }
        catch (Exception exception)
        {
            try
            {
                var currentProcess = await _desktopProcessService.CaptureAsync(CancellationToken.None);
                if (currentProcess.WasRunning)
                {
                    await _desktopProcessService.CloseAsync(CancellationToken.None);
                }

                if (!File.Exists(recoveryAuth))
                {
                    throw new IOException("The enrollment recovery credential is unavailable.");
                }

                await AtomicFile.CopyAsync(recoveryAuth, ambientAuth, CancellationToken.None);
                await _desktopSessionService.RestoreOrClearAsync(currentProfile, CancellationToken.None);
                if (originalProcess.WasRunning)
                {
                    await _desktopProcessService.ReopenAsync(originalProcess, CancellationToken.None);
                    closedForPreparation = false;
                }
            }
            catch (Exception recoveryException)
            {
                throw new AccountEnrollmentException(
                    $"Account login failed and automatic recovery was incomplete. The recovery backup remains at {recoveryAuth}.",
                    new AggregateException(exception, recoveryException));
            }
            finally
            {
                DeleteIncompleteProfile(profilePath);
            }

            if (exception is OperationCanceledException)
            {
                throw;
            }

            throw new AccountEnrollmentException(
                "The new account was not added. The previous ChatGPT account and session were restored.",
                exception);
        }
        finally
        {
            if (closedForPreparation && originalProcess.WasRunning)
            {
                try
                {
                    await _desktopProcessService.ReopenAsync(originalProcess, CancellationToken.None);
                }
                catch
                {
                }
            }
        }
    }

    private async Task<AuthIdentity> WaitForDifferentIdentityAsync(
        AccountProfile currentProfile,
        CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow + DesktopLoginTimeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                var identity = await _authFileReader.ReadIdentityAsync(_paths.AmbientCodexHome, cancellationToken);
                if (!Matches(currentProfile, identity))
                {
                    return identity;
                }
            }
            catch (AuthFileException)
            {
            }

            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
        }

        throw new AccountEnrollmentException("ChatGPT sign-in did not finish within ten minutes.");
    }

    private void DeleteIncompleteProfile(string profilePath)
    {
        var root = Path.GetFullPath(_paths.ProfilesDirectory).TrimEnd(Path.DirectorySeparatorChar)
                   + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(profilePath);
        if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }

    private static bool Matches(AccountProfile profile, AuthIdentity identity) =>
        profile.AccountId.Equals(identity.AccountId, StringComparison.OrdinalIgnoreCase)
        && (string.IsNullOrWhiteSpace(profile.Subject)
            || string.IsNullOrWhiteSpace(identity.Subject)
            || profile.Subject.Equals(identity.Subject, StringComparison.Ordinal));
}
