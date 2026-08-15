using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public interface IAccountSwitcher
{
    Task SwitchAsync(AccountProfile currentProfile, AccountProfile targetProfile, CancellationToken cancellationToken = default);
}

public sealed class AccountSwitchException : Exception
{
    public AccountSwitchException(
        string message,
        Exception? innerException = null,
        bool accountChanged = false) : base(message, innerException)
    {
        AccountChanged = accountChanged;
    }

    public bool AccountChanged { get; }
}
