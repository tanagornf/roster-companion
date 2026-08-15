using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using ChatGPTRoster.Models;
using ChatGPTRoster.Services;
using ChatGPTRoster.ViewModels;

namespace ChatGPTRoster;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ChatGptWindowTracker _windowTracker;
    private readonly DropdownWindow _dropdown;
    private readonly DispatcherTimer _minuteTimer;
    private readonly IStartupRegistrationService _startupRegistration;
    private bool _initialized;
    private bool _initializing;
    private bool? _darkTheme;
    private bool _previewCaptured;

    public MainWindow()
    {
        InitializeComponent();

        var paths = new AppPaths();
        new WindowsSecurityService(paths).EnsureSecureDataDirectories();
        var profileStore = new JsonProfileStore(paths);
        var authFileReader = new AuthFileReader();
        var usageService = new CodexUsageService(authFileReader);
        var profileFileService = new ProfileFileService(paths);
        var desktopProcessService = new DesktopProcessService();
        var desktopSessionService = new DesktopSessionService();
        var enrollmentService = new AdaptiveAccountEnrollmentService(
            paths,
            new CodexCliLocator(),
            authFileReader,
            desktopProcessService,
            desktopSessionService,
            ConfirmDesktopEnrollment);
        var credentialSwitcher = new CredentialSwitcher(paths, authFileReader);
        var accountSwitcher = new WindowsAccountSwitcher(desktopProcessService, desktopSessionService, credentialSwitcher);
        var taskDetector = new CodexTaskDetector(paths.AmbientCodexHome);
        var activeAccountDiscoveryService = new ActiveAccountDiscoveryService(paths, authFileReader);
        var settingsService = new AppSettingsService(paths);
        _startupRegistration = new StartupRegistrationService();

        _viewModel = new MainViewModel(
            profileStore,
            enrollmentService,
            usageService,
            profileFileService,
            accountSwitcher,
            taskDetector,
            activeAccountDiscoveryService,
            settingsService,
            RequestAlias,
            ConfirmRemove,
            ConfirmSwitch,
            ConfirmTaskInterruption,
            ConfirmInitialAccount,
            ShowMessage);
        DataContext = _viewModel;

        _windowTracker = new ChatGptWindowTracker();
        _windowTracker.StateChanged += WindowTracker_StateChanged;
        _dropdown = new DropdownWindow(_viewModel, _startupRegistration, IsNetworkRefreshAllowed);
        _dropdown.RequestClose += (_, _) => _dropdown.HideDropdown();

        _minuteTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMinutes(1)
        };
        _minuteTimer.Tick += async (_, _) =>
            await _viewModel.RefreshDueAccountsAsync(dropdownOpened: false, IsNetworkRefreshAllowed());

        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Closing += (_, _) =>
        {
            _viewModel.CancelPendingOperations();
            _windowTracker.Dispose();
            _minuteTimer.Stop();
        };
    }

    public void Start()
    {
        try
        {
            if (!string.Equals(
                    Environment.GetEnvironmentVariable("ROSTER_COMPANION_DISABLE_AUTOSTART"),
                    "1",
                    StringComparison.Ordinal))
            {
                _startupRegistration.EnsureRegistered();
            }
        }
        catch
        {
            // Startup registration is convenient, but it must never prevent the companion from running.
        }

        _windowTracker.Start();
        _minuteTimer.Start();
    }

    private Window DialogOwner => _dropdown.IsVisible ? _dropdown : this;

    private async void WindowTracker_StateChanged(object? sender, ChatGptWindowState? state)
    {
        var forceVisible = string.Equals(
            Environment.GetEnvironmentVariable("ROSTER_COMPANION_FORCE_VISIBLE"),
            "1",
            StringComparison.Ordinal);
        if (state is null || (!state.IsAnchorVisible && !_viewModel.IsChangingAccount && !forceVisible))
        {
            if (!_viewModel.IsChangingAccount)
            {
                _dropdown.HideDropdown();
                Hide();
            }

            return;
        }

        ApplyTheme(state.IsDarkTheme);
        Left = state.Placement.SelectorLeft;
        Top = state.Placement.SelectorTop;
        Width = state.Placement.SelectorWidth;
        Height = state.Placement.SelectorHeight;
        _dropdown.UpdatePlacement(state.Placement);

        if (!IsVisible)
        {
            Show();
            _dropdown.Owner ??= this;
        }

        ChatGptWindowTracker.KeepAboveChatGpt(this);
        if (!_initialized && !_initializing)
        {
            _initializing = true;
            try
            {
                await _viewModel.InitializeAsync();
                _initialized = true;
                await CapturePreviewIfRequestedAsync(state.Placement);
            }
            finally
            {
                _initializing = false;
            }
        }
    }

    private async Task CapturePreviewIfRequestedAsync(OverlayPlacement placement)
    {
        var directory = Environment.GetEnvironmentVariable("ROSTER_COMPANION_PREVIEW_DIR");
        if (_previewCaptured || string.IsNullOrWhiteSpace(directory))
        {
            return;
        }

        _previewCaptured = true;
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        VisualSnapshotService.Save(this, Path.Combine(directory, "selector.png"));
        _dropdown.ShowDropdown(placement);
        await Dispatcher.Yield(DispatcherPriority.ApplicationIdle);
        VisualSnapshotService.Save(_dropdown, Path.Combine(directory, "dropdown.png"));
    }

    private void SelectorButton_Click(object sender, RoutedEventArgs e)
    {
        if (_dropdown.IsVisible)
        {
            _dropdown.HideDropdown();
            return;
        }

        if (_windowTracker.CurrentState is { } state)
        {
            _dropdown.ShowDropdown(state.Placement);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsChangingAccount) && _viewModel.IsChangingAccount)
        {
            _dropdown.HideDropdown();
        }
    }

    private bool IsNetworkRefreshAllowed() =>
        _windowTracker.CurrentState is not null || _viewModel.IsChangingAccount;

    private string? RequestAlias(AccountProfile profile)
    {
        var dialog = new AliasDialog(profile.Alias) { Owner = DialogOwner };
        return dialog.ShowDialog() == true ? dialog.Alias : null;
    }

    private bool ConfirmInitialAccount(AccountProfile profile)
    {
        return CompanionDialog.Confirm(
            this,
            "Add current ChatGPT account",
            $"Roster Companion found the ChatGPT account currently signed in as {profile.Email}.\n\nAdd this account to your local roster? Its profile and recovery data will be stored only on this Windows device.",
            "Add account");
    }

    private bool ConfirmDesktopEnrollment(AccountProfile currentProfile)
    {
        var introduction = CodexCliLocator.IsDesktopEnrollmentForced
            ? "Desktop sign-in test mode is enabled, so Roster Companion will use the ChatGPT sign-in screen instead of the installed Codex CLI."
            : "The Codex CLI is not available, so adding an account requires the supported ChatGPT sign-in screen.";
        return CompanionDialog.Confirm(
            DialogOwner,
            "Add account through ChatGPT",
            $"{introduction} The newly signed-in account will become active.\n\nChatGPT/Codex will close during the account change.\n\nRoster Companion will back up the current account and session before continuing. Continue?",
            "Continue");
    }

    private bool ConfirmRemove(AccountProfile profile)
    {
        return CompanionDialog.Confirm(
            DialogOwner,
            "Remove local profile",
            $"Remove {profile.DisplayName} from Roster Companion?\n\nThis removes its locally saved profile, credentials, and session backup. It does not delete the OpenAI account.",
            "Remove account",
            destructive: true);
    }

    private bool ConfirmSwitch(AccountProfile profile)
    {
        return CompanionDialog.Confirm(
            DialogOwner,
            "Change ChatGPT account",
            $"Change to {profile.DisplayName}?\n\nChatGPT/Codex will close during the account change.",
            "Change Account");
    }

    private bool ConfirmTaskInterruption(TaskActivityState state)
    {
        var lead = state == TaskActivityState.Running
            ? "Codex is currently working on at least one task."
            : "Roster Companion could not confirm whether a Codex task is still running.";
        return CompanionDialog.Confirm(
            DialogOwner,
            "Codex task may be interrupted",
            $"{lead}\n\nChanging accounts may stop work in progress and discard output. Change accounts anyway?",
            "Change Account");
    }

    private void ShowMessage(string title, string message) =>
        CompanionDialog.Inform(DialogOwner, title, message);

    private void ApplyTheme(bool dark)
    {
        if (_darkTheme == dark)
        {
            return;
        }

        _darkTheme = dark;
        var resources = Application.Current.Resources;
        SetThemeColor(resources, "WindowBackground", dark ? "#202020" : "#F7F7F7");
        SetThemeColor(resources, "Surface", dark ? "#292929" : "#FFFFFF");
        SetThemeColor(resources, "Hover", dark ? "#303030" : "#EAEAE8");
        SetThemeColor(resources, "PrimaryText", dark ? "#ECECEC" : "#2D2D2D");
        SetThemeColor(resources, "SelectorText", dark ? "#C5C5C5" : "#484848");
        SetThemeColor(resources, "SecondaryText", dark ? "#A8A8A8" : "#696969");
        SetThemeColor(resources, "Border", dark ? "#383838" : "#D8D8D5");
        SetThemeColor(resources, "Danger", dark ? "#F05252" : "#D00E17");
        SetThemeColor(resources, "DangerHover", dark ? "#3A2222" : "#FBE8E8");
    }

    private static void SetThemeColor(ResourceDictionary resources, string name, string value)
    {
        var color = ColorFrom(value);
        resources[$"{name}Color"] = color;
        if (resources[$"{name}Brush"] is SolidColorBrush brush)
        {
            if (brush.IsFrozen)
            {
                resources[$"{name}Brush"] = new SolidColorBrush(color);
            }
            else
            {
                brush.Color = color;
            }
        }
    }

    private static Color ColorFrom(string value) => (Color)ColorConverter.ConvertFromString(value);
}
