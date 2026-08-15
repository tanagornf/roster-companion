using System.Diagnostics;
using System.IO;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public sealed class CodexAccountEnrollmentService : IAccountEnrollmentService
{
    private static readonly TimeSpan LoginTimeout = TimeSpan.FromMinutes(5);

    private readonly AppPaths _paths;
    private readonly ICodexCliLocator _cliLocator;
    private readonly IAuthFileReader _authFileReader;

    public CodexAccountEnrollmentService(
        AppPaths paths,
        ICodexCliLocator cliLocator,
        IAuthFileReader authFileReader)
    {
        _paths = paths;
        _cliLocator = cliLocator;
        _authFileReader = authFileReader;
    }

    public async Task<AccountProfile> EnrollAsync(
        AccountProfile? currentProfile = null,
        CancellationToken cancellationToken = default)
    {
        var executable = _cliLocator.FindExecutable();
        if (executable is null)
        {
            throw new AccountEnrollmentException(
                "The official Codex CLI was not found. Install or repair the ChatGPT desktop app, then try again.");
        }

        Directory.CreateDirectory(_paths.ProfilesDirectory);
        var profileId = Guid.NewGuid().ToString("D");
        var profilePath = Path.Combine(_paths.ProfilesDirectory, profileId);
        Directory.CreateDirectory(profilePath);

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = "login",
                UseShellExecute = false,
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
            startInfo.Environment["CODEX_HOME"] = profilePath;

            Process process;
            try
            {
                process = Process.Start(startInfo)
                          ?? throw new InvalidOperationException("No Codex login process was returned.");
            }
            catch (Exception exception) when (exception is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                throw new CodexCliLaunchException(
                    "The detected Codex CLI could not be launched. The incomplete isolated profile was removed.",
                    exception);
            }

            using (process)
            {
                var standardOutput = process.StandardOutput.ReadToEndAsync(cancellationToken);
                var standardError = process.StandardError.ReadToEndAsync(cancellationToken);

                try
                {
                    await process.WaitForExitAsync(cancellationToken).WaitAsync(LoginTimeout, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    TryStop(process);
                    throw;
                }
                catch (TimeoutException)
                {
                    TryStop(process);
                    throw new AccountEnrollmentException("The login did not finish within five minutes. No account was added.");
                }

                await Task.WhenAll(standardOutput, standardError);
                if (process.ExitCode != 0)
                {
                    throw new AccountEnrollmentException("The official Codex login did not complete successfully. No account was added.");
                }

                var identity = await _authFileReader.ReadIdentityAsync(profilePath, cancellationToken);
                return new AccountProfile
                {
                    Id = profileId,
                    AccountId = identity.AccountId,
                    Subject = identity.Subject,
                    Email = identity.Email,
                    Alias = string.Empty,
                    Plan = PlanName.Normalize(identity.Plan),
                    ProfilePath = profilePath,
                    CreatedAt = DateTimeOffset.UtcNow
                };
            }
        }
        catch
        {
            DeleteNewProfile(profilePath);
            throw;
        }
    }

    private static void TryStop(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }

        try
        {
            process.WaitForExit(2000);
        }
        catch (InvalidOperationException)
        {
        }
    }

    private void DeleteNewProfile(string profilePath)
    {
        var root = Path.GetFullPath(_paths.ProfilesDirectory)
            .TrimEnd(Path.DirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(profilePath);
        if (target.StartsWith(root, StringComparison.OrdinalIgnoreCase) && Directory.Exists(target))
        {
            Directory.Delete(target, recursive: true);
        }
    }
}
