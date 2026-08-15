using ChatGPTRoster.Models;

namespace ChatGPTRoster.Services;

public interface IAccountEnrollmentService
{
    Task<AccountProfile> EnrollAsync(
        AccountProfile? currentProfile = null,
        CancellationToken cancellationToken = default);
}

public class AccountEnrollmentException : Exception
{
    public AccountEnrollmentException(string message) : base(message)
    {
    }

    public AccountEnrollmentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

public sealed class CodexCliLaunchException : AccountEnrollmentException
{
    public CodexCliLaunchException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
