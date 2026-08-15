using System.Windows.Input;
using ChatGPTRoster.Models;

namespace ChatGPTRoster.ViewModels;

public sealed class AccountRowViewModel : ObservableObject
{
    private readonly Action<AccountRowViewModel> _switchAccount;
    private readonly Action<AccountRowViewModel> _renameAlias;
    private readonly Action<AccountRowViewModel> _removeAccount;

    public AccountRowViewModel(
        AccountProfile profile,
        Action<AccountRowViewModel> switchAccount,
        Action<AccountRowViewModel> renameAlias,
        Action<AccountRowViewModel> removeAccount)
    {
        Profile = profile;
        _switchAccount = switchAccount;
        _renameAlias = renameAlias;
        _removeAccount = removeAccount;

        SwitchAccountCommand = new RelayCommand(_ => _switchAccount(this), _ => !IsActive);
        RenameAliasCommand = new RelayCommand(_ => _renameAlias(this));
        RemoveAccountCommand = new RelayCommand(_ => _removeAccount(this), _ => CanRemove);
    }

    public AccountProfile Profile { get; }
    public string DisplayName => Profile.DisplayName;
    public string Email => Profile.Email;
    public string Plan => string.IsNullOrWhiteSpace(Profile.Plan) ? "Unknown" : Profile.Plan;
    public bool IsActive => Profile.IsActive;
    public string ActionLabel => IsActive ? "Active" : "Change Account";
    public bool CanRemove => !IsActive;

    public double ShortTermRemaining => Profile.Usage.ShortTerm?.RemainingPercent ?? 0;
    public double WeeklyRemaining => Profile.Usage.Weekly?.RemainingPercent ?? 0;
    public string ShortTermPercentText => FormatPercent(Profile.Usage.ShortTerm);
    public string WeeklyPercentText => FormatPercent(Profile.Usage.Weekly);
    public string ShortTermResetText => FormatReset(Profile.Usage.ShortTerm);
    public string WeeklyResetText => FormatReset(Profile.Usage.Weekly);
    public bool HasShortTermUsage => Profile.Usage.ShortTerm is not null;
    public bool HasWeeklyUsage => Profile.Usage.Weekly is not null;
    public string CompactUsageText => $"5h: {FormatCompactPercent(Profile.Usage.ShortTerm)} · Weekly: {FormatCompactPercent(Profile.Usage.Weekly)}";
    public string WeeklyResetCompactText => FormatCompactReset(Profile.Usage.Weekly);
    public bool IsUsageStale => !string.IsNullOrWhiteSpace(Profile.Usage.ErrorMessage)
                                || Profile.Usage.LastUpdatedAt is null;
    public string UsageFreshnessText
    {
        get
        {
            if (!IsUsageStale)
            {
                return string.Empty;
            }

            return Profile.Usage.LastUpdatedAt is { } updated
                ? $"Stale · Updated {updated.ToLocalTime():MMM d, h:mm tt}"
                : "Stale · Usage not refreshed";
        }
    }

    public ICommand SwitchAccountCommand { get; }
    public ICommand RenameAliasCommand { get; }
    public ICommand RemoveAccountCommand { get; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(Plan));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(ActionLabel));
        OnPropertyChanged(nameof(ShortTermRemaining));
        OnPropertyChanged(nameof(WeeklyRemaining));
        OnPropertyChanged(nameof(ShortTermPercentText));
        OnPropertyChanged(nameof(WeeklyPercentText));
        OnPropertyChanged(nameof(ShortTermResetText));
        OnPropertyChanged(nameof(WeeklyResetText));
        OnPropertyChanged(nameof(HasShortTermUsage));
        OnPropertyChanged(nameof(HasWeeklyUsage));
        OnPropertyChanged(nameof(CompactUsageText));
        OnPropertyChanged(nameof(WeeklyResetCompactText));
        OnPropertyChanged(nameof(IsUsageStale));
        OnPropertyChanged(nameof(UsageFreshnessText));
        OnPropertyChanged(nameof(CanRemove));
        (SwitchAccountCommand as RelayCommand)?.RaiseCanExecuteChanged();
        (RemoveAccountCommand as RelayCommand)?.RaiseCanExecuteChanged();
    }

    public void RefreshCountdown()
    {
        OnPropertyChanged(nameof(ShortTermResetText));
        OnPropertyChanged(nameof(WeeklyResetText));
        OnPropertyChanged(nameof(WeeklyResetCompactText));
        OnPropertyChanged(nameof(IsUsageStale));
        OnPropertyChanged(nameof(UsageFreshnessText));
    }

    private static string FormatCompactPercent(UsageWindow? window) =>
        window is null ? "—" : $"{Math.Clamp(window.RemainingPercent, 0, 100):0}%";

    private static string FormatCompactReset(UsageWindow? window)
    {
        if (window?.ResetsAt is null)
        {
            return "Weekly reset unavailable";
        }

        var localReset = window.ResetsAt.Value.ToLocalTime();
        return $"Weekly resets {localReset:dddd 'at' h:mm tt}";
    }

    private static string FormatPercent(UsageWindow? window) =>
        window is null ? "Usage unavailable" : $"{Math.Clamp(window.RemainingPercent, 0, 100):0}% remaining";

    private static string FormatReset(UsageWindow? window)
    {
        if (window?.ResetsAt is null)
        {
            return "Reset time unavailable";
        }

        var localReset = window.ResetsAt.Value.ToLocalTime();
        var remaining = localReset - DateTimeOffset.Now;
        var countdown = remaining <= TimeSpan.Zero
            ? "refresh due"
            : remaining.TotalDays >= 1
                ? $"in {(int)remaining.TotalDays}d {remaining.Hours}h"
                : remaining.TotalHours >= 1
                    ? $"in {(int)remaining.TotalHours}h {remaining.Minutes}m"
                    : $"in {Math.Max(0, remaining.Minutes)}m";

        return $"Resets {localReset:ddd, MMM d h:mm tt} ({countdown})";
    }
}
