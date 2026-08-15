using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using System.Windows.Input;
using ChatGPTRoster.Models;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly IProfileStore _profileStore;
    private readonly IAccountEnrollmentService _enrollmentService;
    private readonly IUsageService _usageService;
    private readonly IProfileFileService _profileFileService;
    private readonly IAccountSwitcher _accountSwitcher;
    private readonly ICodexTaskDetector _taskDetector;
    private readonly IActiveAccountDiscoveryService _activeAccountDiscoveryService;
    private readonly IAppSettingsService _settingsService;
    private readonly Func<AccountProfile, string?> _requestAlias;
    private readonly Func<AccountProfile, bool> _confirmRemove;
    private readonly Func<AccountProfile, bool> _confirmSwitch;
    private readonly Func<TaskActivityState, bool> _confirmTaskInterruption;
    private readonly Func<AccountProfile, bool> _confirmInitialAccount;
    private readonly Action<string, string> _showMessage;
    private string _searchText = string.Empty;
    private string _statusText = "Ready";
    private bool _isRefreshing;
    private bool _isEnrolling;
    private bool _isChangingAccount;
    private CancellationTokenSource? _enrollmentCancellation;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly Dictionary<string, int> _refreshFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, DateTimeOffset> _retryAfter = new(StringComparer.OrdinalIgnoreCase);

    public MainViewModel(
        IProfileStore profileStore,
        IAccountEnrollmentService enrollmentService,
        IUsageService usageService,
        IProfileFileService profileFileService,
        IAccountSwitcher accountSwitcher,
        ICodexTaskDetector taskDetector,
        IActiveAccountDiscoveryService activeAccountDiscoveryService,
        IAppSettingsService settingsService,
        Func<AccountProfile, string?> requestAlias,
        Func<AccountProfile, bool> confirmRemove,
        Func<AccountProfile, bool> confirmSwitch,
        Func<TaskActivityState, bool> confirmTaskInterruption,
        Func<AccountProfile, bool> confirmInitialAccount,
        Action<string, string> showMessage)
    {
        _profileStore = profileStore;
        _enrollmentService = enrollmentService;
        _usageService = usageService;
        _profileFileService = profileFileService;
        _accountSwitcher = accountSwitcher;
        _taskDetector = taskDetector;
        _activeAccountDiscoveryService = activeAccountDiscoveryService;
        _settingsService = settingsService;
        _requestAlias = requestAlias;
        _confirmRemove = confirmRemove;
        _confirmSwitch = confirmSwitch;
        _confirmTaskInterruption = confirmTaskInterruption;
        _confirmInitialAccount = confirmInitialAccount;
        _showMessage = showMessage;

        AccountsView = CollectionViewSource.GetDefaultView(Accounts);
        AccountsView.Filter = MatchesSearch;

        AddAccountCommand = new RelayCommand(_ => ToggleEnrollment());
        RefreshCommand = new AsyncRelayCommand(RefreshAsync, () => Accounts.Count > 0);
    }

    public ObservableCollection<AccountRowViewModel> Accounts { get; } = [];
    public ICollectionView AccountsView { get; }

    public string SearchText
    {
        get => _searchText;
        set
        {
            if (SetProperty(ref _searchText, value))
            {
                AccountsView.Refresh();
                OnPropertyChanged(nameof(HasSearchResults));
            }
        }
    }

    public string StatusText
    {
        get => _statusText;
        private set => SetProperty(ref _statusText, value);
    }

    public bool IsRefreshing
    {
        get => _isRefreshing;
        private set => SetProperty(ref _isRefreshing, value);
    }

    public bool IsChangingAccount
    {
        get => _isChangingAccount;
        private set
        {
            if (SetProperty(ref _isChangingAccount, value))
            {
                OnPropertyChanged(nameof(SelectorLabel));
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    public bool IsEnrolling
    {
        get => _isEnrolling;
        private set
        {
            if (SetProperty(ref _isEnrolling, value))
            {
                OnPropertyChanged(nameof(AddAccountLabel));
                OnPropertyChanged(nameof(AddAccountGlyph));
                OnPropertyChanged(nameof(IsBusy));
            }
        }
    }

    public bool HasAccounts => Accounts.Count > 0;
    public bool HasSearchResults => !HasAccounts || AccountsView.Cast<object>().Any();
    public AccountRowViewModel? ActiveAccount => Accounts.FirstOrDefault(account => account.IsActive);
    public string SelectorLabel => IsChangingAccount ? "Changing account…" : ActiveAccount?.DisplayName ?? "Add account";
    public bool IsBusy => IsRefreshing || IsEnrolling || IsChangingAccount;
    public string AddAccountLabel => IsEnrolling ? "Cancel login" : "Add account";
    public string AddAccountGlyph => IsEnrolling ? "×" : "+";
    public RelayCommand AddAccountCommand { get; }
    public AsyncRelayCommand RefreshCommand { get; }

    public async Task InitializeAsync()
    {
        try
        {
            var loadedProfiles = await _profileStore.LoadAsync();
            var profiles = (await _activeAccountDiscoveryService.SynchronizeAsync(loadedProfiles)).ToList();
            var settings = await _settingsService.LoadAsync();
            var imported = profiles.FirstOrDefault(profile =>
                profile.IsActive && loadedProfiles.All(existing => !IsSameIdentity(existing, profile)));
            if (imported is not null && loadedProfiles.Count == 0)
            {
                var keepImported = settings.InitialAccountPromptCompleted
                    ? settings.InitialAccountImportAccepted
                    : _confirmInitialAccount(imported);

                if (!settings.InitialAccountPromptCompleted)
                {
                    settings.InitialAccountPromptCompleted = true;
                    settings.InitialAccountImportAccepted = keepImported;
                    await _settingsService.SaveAsync(settings);
                }

                if (!keepImported)
                {
                    await _profileFileService.RemoveManagedProfileAsync(imported);
                    profiles.Remove(imported);
                }
            }
            Accounts.Clear();
            foreach (var profile in profiles.OrderByDescending(profile => profile.IsActive).ThenBy(profile => profile.DisplayName))
            {
                Accounts.Add(CreateRow(profile));
            }

            StatusText = Accounts.Count == 0
                ? "No accounts added yet"
                : $"{Accounts.Count} account{(Accounts.Count == 1 ? string.Empty : "s")} loaded";
            await _profileStore.SaveAsync(Accounts.Select(account => account.Profile).ToArray());
        }
        catch (Exception exception)
        {
            StatusText = "Could not load local profiles";
            _showMessage("Profile load failed", exception.Message);
        }

        NotifyCollectionChanged();
        if (Accounts.Count > 0)
        {
            await RefreshAsync();
        }
    }

    private AccountRowViewModel CreateRow(AccountProfile profile) =>
        new(profile, SwitchAccount, RenameAlias, RemoveAccount);

    private bool MatchesSearch(object item)
    {
        if (item is not AccountRowViewModel account || string.IsNullOrWhiteSpace(SearchText))
        {
            return true;
        }

        return account.DisplayName.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase)
               || account.Email.Contains(SearchText, StringComparison.CurrentCultureIgnoreCase);
    }

    private async Task AddAccountAsync()
    {
        using var cancellation = new CancellationTokenSource();
        _enrollmentCancellation = cancellation;
        IsEnrolling = true;
        StatusText = "Waiting for the official account sign-in…";
        try
        {
            var profile = await _enrollmentService.EnrollAsync(ActiveAccount?.Profile, cancellation.Token);
            var duplicate = Accounts.FirstOrDefault(account => IsSameIdentity(account.Profile, profile));
            if (duplicate is not null)
            {
                await _profileFileService.RemoveManagedProfileAsync(profile);
                if (profile.IsActive)
                {
                    foreach (var account in Accounts)
                    {
                        account.Profile.IsActive = ReferenceEquals(account, duplicate);
                        account.Refresh();
                    }

                    await SaveAsync($"{duplicate.DisplayName} is now active");
                }

                StatusText = "That account is already in the roster";
                _showMessage("Account already added", $"{duplicate.DisplayName} is already available in Roster Companion.");
                return;
            }

            if (profile.IsActive)
            {
                foreach (var account in Accounts)
                {
                    account.Profile.IsActive = false;
                    account.Refresh();
                }
            }
            var row = CreateRow(profile);
            Accounts.Add(row);
            NotifyCollectionChanged();
            await RefreshAccountAsync(row);
            await SaveAsync("Account added");
        }
        catch (OperationCanceledException)
        {
            StatusText = "Account login cancelled";
        }
        catch (Exception exception) when (exception is AccountEnrollmentException or AuthFileException)
        {
            StatusText = "Account was not added";
            _showMessage("Account login failed", exception.Message);
        }
        finally
        {
            if (ReferenceEquals(_enrollmentCancellation, cancellation))
            {
                _enrollmentCancellation = null;
            }

            IsEnrolling = false;
        }
    }

    private void ToggleEnrollment()
    {
        if (IsEnrolling)
        {
            StatusText = "Cancelling account login…";
            _enrollmentCancellation?.Cancel();
            return;
        }

        _ = AddAccountAsync();
    }

    public void CancelPendingOperations()
    {
        _enrollmentCancellation?.Cancel();
    }

    private async void SwitchAccount(AccountRowViewModel target)
    {
        var current = Accounts.FirstOrDefault(account => account.IsActive);
        if (current is null)
        {
            _showMessage("No active account", "Roster Companion could not identify the account currently in use.");
            return;
        }

        if (!_confirmSwitch(target.Profile))
        {
            StatusText = "Account change cancelled";
            return;
        }

        TaskActivityState taskState;
        try
        {
            StatusText = "Checking for active Codex tasks…";
            taskState = await _taskDetector.GetActivityStateAsync();
        }
        catch
        {
            taskState = TaskActivityState.Unknown;
        }

        if (taskState != TaskActivityState.Idle && !_confirmTaskInterruption(taskState))
        {
            StatusText = "Account change cancelled";
            return;
        }

        try
        {
            IsChangingAccount = true;
            StatusText = $"Changing to {target.DisplayName}…";
            await _accountSwitcher.SwitchAsync(current.Profile, target.Profile);
            foreach (var account in Accounts)
            {
                account.Profile.IsActive = ReferenceEquals(account, target);
                account.Refresh();
            }

            AccountsView.Refresh();
            await SaveAsync($"{target.DisplayName} is now active");
            await RefreshAccountAsync(target);
        }
        catch (AccountSwitchException exception)
        {
            if (exception.AccountChanged)
            {
                foreach (var account in Accounts)
                {
                    account.Profile.IsActive = ReferenceEquals(account, target);
                    account.Refresh();
                }

                AccountsView.Refresh();
                await SaveAsync("Account changed; open the ChatGPT desktop app manually");
                _showMessage("Account changed", exception.Message);
            }
            else
            {
                StatusText = "Account change failed";
                _showMessage("Could not change account", exception.Message);
            }
        }
        finally
        {
            IsChangingAccount = false;
            NotifyActiveAccountChanged();
        }
    }

    private async void RenameAlias(AccountRowViewModel account)
    {
        var alias = _requestAlias(account.Profile);
        if (alias is null)
        {
            return;
        }

        account.Profile.Alias = alias.Trim();
        account.Refresh();
        AccountsView.Refresh();
        await SaveAsync("Alias updated");
    }

    private async void RemoveAccount(AccountRowViewModel account)
    {
        if (account.IsActive)
        {
            _showMessage("Active account", "Change to another account before removing this profile.");
            return;
        }

        if (!_confirmRemove(account.Profile))
        {
            return;
        }

        try
        {
            await _profileFileService.RemoveManagedProfileAsync(account.Profile);
            Accounts.Remove(account);
            AccountsView.Refresh();
            NotifyCollectionChanged();
            await SaveAsync("Local profile removed");
        }
        catch (Exception exception)
        {
            StatusText = "Profile removal failed";
            _showMessage("Could not remove account", exception.Message);
        }
    }

    private async Task RefreshAsync()
    {
        if (!await _refreshGate.WaitAsync(0))
        {
            return;
        }

        IsRefreshing = true;
        OnPropertyChanged(nameof(IsBusy));
        StatusText = "Refreshing account information…";
        try
        {
            var failures = 0;
            foreach (var account in Accounts)
            {
                if (!await RefreshAccountAsync(account))
                {
                    failures++;
                }
            }

            await SaveAsync(failures == 0
                ? $"Usage refreshed at {DateTimeOffset.Now:h:mm tt}"
                : $"Usage refreshed with {failures} unavailable account{(failures == 1 ? string.Empty : "s")}");
        }
        finally
        {
            IsRefreshing = false;
            OnPropertyChanged(nameof(IsBusy));
            _refreshGate.Release();
        }
    }

    public async Task RefreshDueAccountsAsync(bool dropdownOpened, bool networkAllowed)
    {
        foreach (var account in Accounts)
        {
            account.RefreshCountdown();
        }

        if (!networkAllowed || !await _refreshGate.WaitAsync(0))
        {
            return;
        }

        try
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var account in Accounts)
            {
                var due = UsageRefreshPolicy.IsDue(account.Profile.Usage.LastUpdatedAt, account.IsActive, now);
                var staleInactiveOnOpen = dropdownOpened && !account.IsActive && due;
                if (!due && !staleInactiveOnOpen)
                {
                    continue;
                }

                if (_retryAfter.TryGetValue(account.Profile.Id, out var retryAt) && now < retryAt)
                {
                    continue;
                }

                await RefreshAccountAsync(account);
            }

            await SaveAsync("Usage updated");
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private async Task<bool> RefreshAccountAsync(AccountRowViewModel account)
    {
        try
        {
            var usage = await _usageService.GetUsageAsync(account.Profile);
            usage.LastUpdatedAt ??= DateTimeOffset.UtcNow;
            usage.ErrorMessage = null;
            account.Profile.Usage = usage;
            if (!string.IsNullOrWhiteSpace(usage.Plan) && usage.Plan != "Unknown")
            {
                account.Profile.Plan = PlanName.Normalize(usage.Plan);
            }

            account.Refresh();
            _refreshFailures.Remove(account.Profile.Id);
            _retryAfter.Remove(account.Profile.Id);
            return true;
        }
        catch (Exception exception) when (exception is UsageServiceException or AuthFileException)
        {
            account.Profile.Usage.ErrorMessage = exception.Message;
            var failures = _refreshFailures.TryGetValue(account.Profile.Id, out var count) ? count + 1 : 1;
            _refreshFailures[account.Profile.Id] = failures;
            _retryAfter[account.Profile.Id] = DateTimeOffset.UtcNow + UsageRefreshPolicy.RetryDelay(failures);
            account.Refresh();
            return false;
        }
    }

    private static bool IsSameIdentity(AccountProfile left, AccountProfile right)
    {
        if (!left.AccountId.Equals(right.AccountId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(left.Subject)
               || string.IsNullOrWhiteSpace(right.Subject)
               || left.Subject.Equals(right.Subject, StringComparison.Ordinal);
    }

    private async Task SaveAsync(string successStatus)
    {
        try
        {
            await _profileStore.SaveAsync(Accounts.Select(account => account.Profile).ToArray());
            StatusText = successStatus;
        }
        catch (Exception exception)
        {
            StatusText = "Could not save profile changes";
            _showMessage("Profile save failed", exception.Message);
        }
    }

    private void NotifyCollectionChanged()
    {
        OnPropertyChanged(nameof(HasAccounts));
        OnPropertyChanged(nameof(HasSearchResults));
        RefreshCommand.RaiseCanExecuteChanged();
        AddAccountCommand.RaiseCanExecuteChanged();
        NotifyActiveAccountChanged();
    }

    private void NotifyActiveAccountChanged()
    {
        OnPropertyChanged(nameof(ActiveAccount));
        OnPropertyChanged(nameof(SelectorLabel));
    }
}
