using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public sealed class WindowsAccountSwitcher : IAccountSwitcher
{
    private readonly IDesktopProcessService _desktopProcessService;
    private readonly IDesktopSessionService _desktopSessionService;
    private readonly ICredentialSwitcher _credentialSwitcher;

    public WindowsAccountSwitcher(
        IDesktopProcessService desktopProcessService,
        IDesktopSessionService desktopSessionService,
        ICredentialSwitcher credentialSwitcher)
    {
        _desktopProcessService = desktopProcessService;
        _desktopSessionService = desktopSessionService;
        _credentialSwitcher = credentialSwitcher;
    }

    public async Task SwitchAsync(
        AccountProfile currentProfile,
        AccountProfile targetProfile,
        CancellationToken cancellationToken = default)
    {
        var processSnapshot = await _desktopProcessService.CaptureAsync(cancellationToken);
        CredentialSwitchReceipt? receipt = null;
        var closed = false;
        AccountSwitchException? switchFailure = null;

        try
        {
            if (processSnapshot.WasRunning)
            {
                await _desktopProcessService.CloseAsync(cancellationToken);
                closed = true;
            }

            await _desktopSessionService.BackupAsync(currentProfile, cancellationToken);
            receipt = await _credentialSwitcher.ActivateAsync(currentProfile, targetProfile, cancellationToken);
            await _desktopSessionService.RestoreOrClearAsync(targetProfile, cancellationToken);
        }
        catch (Exception exception)
        {
            if (receipt is not null)
            {
                try
                {
                    await _credentialSwitcher.RollbackAsync(receipt, cancellationToken);
                    await _desktopSessionService.RestoreOrClearAsync(currentProfile, cancellationToken);
                }
                catch (Exception recoveryException)
                {
                    switchFailure = new AccountSwitchException(
                        "Account switching failed and automatic recovery was incomplete. The credential backup remains available locally.",
                        new AggregateException(exception, recoveryException));
                }
            }

            switchFailure ??= new AccountSwitchException(
                "Account switching failed. The previous account was preserved.",
                exception);
        }

        Exception? reopenFailure = null;
        if (closed)
        {
            try
            {
                await _desktopProcessService.ReopenAsync(processSnapshot, CancellationToken.None);
            }
            catch (Exception exception)
            {
                reopenFailure = exception;
            }
        }

        if (switchFailure is not null)
        {
            if (reopenFailure is not null)
            {
                throw new AccountSwitchException(
                    switchFailure.Message + " The ChatGPT desktop app also could not be reopened; open it manually.",
                    new AggregateException(switchFailure, reopenFailure));
            }

            throw switchFailure;
        }

        if (reopenFailure is not null)
        {
            throw new AccountSwitchException(
                "The account changed successfully, but the ChatGPT desktop app could not be reopened. Open it manually.",
                reopenFailure,
                accountChanged: true);
        }
    }
}
