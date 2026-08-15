using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;

namespace ChatGPTRoster.Services;

public interface IWindowsSecurityService
{
    void EnsureSecureDataDirectories();
}

public sealed class WindowsSecurityService : IWindowsSecurityService
{
    private readonly AppPaths _paths;

    public WindowsSecurityService(AppPaths paths)
    {
        _paths = paths;
    }

    public void EnsureSecureDataDirectories()
    {
        Directory.CreateDirectory(_paths.DataRoot);
        Directory.CreateDirectory(_paths.ProfilesDirectory);
        Directory.CreateDirectory(_paths.BackupsDirectory);

        var currentUser = WindowsIdentity.GetCurrent().User
                          ?? throw new InvalidOperationException("The current Windows user identity could not be determined.");
        var inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
        var security = new DirectorySecurity();
        security.SetAccessRuleProtection(isProtected: true, preserveInheritance: false);
        security.AddAccessRule(new FileSystemAccessRule(
            currentUser,
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null),
            FileSystemRights.FullControl,
            inheritance,
            PropagationFlags.None,
            AccessControlType.Allow));

        new DirectoryInfo(_paths.DataRoot).SetAccessControl(security);
    }
}
