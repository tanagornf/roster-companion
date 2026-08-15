using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using ChatGPTRoster.Services;
using ChatGPTRoster.ViewModels;

namespace ChatGPTRoster;

public partial class DropdownWindow : Window
{
    private const double ShadowMargin = 10;
    private readonly MainViewModel _viewModel;
    private readonly IStartupRegistrationService _startupRegistration;
    private readonly Func<bool> _networkRefreshAllowed;
    private readonly OutsideClickMonitor _outsideClickMonitor;
    private OverlayPlacement? _placement;
    private bool _aboutOpen;

    public DropdownWindow(
        MainViewModel viewModel,
        IStartupRegistrationService startupRegistration,
        Func<bool> networkRefreshAllowed)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _startupRegistration = startupRegistration;
        _networkRefreshAllowed = networkRefreshAllowed;
        DataContext = viewModel;
        _outsideClickMonitor = new OutsideClickMonitor(Dispatcher, HideDropdown);
        _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        Closed += (_, _) => _outsideClickMonitor.Dispose();
    }

    public event EventHandler? RequestClose;

    public void ShowDropdown(OverlayPlacement placement)
    {
        UpdatePlacement(placement);
        if (!IsVisible)
        {
            Show();
        }

        Activate();
        ChatGptWindowTracker.KeepAboveChatGpt(this);
        _outsideClickMonitor.Start();
        _ = _viewModel.RefreshDueAccountsAsync(dropdownOpened: true, _networkRefreshAllowed());
    }

    public void UpdatePlacement(OverlayPlacement placement)
    {
        _placement = placement;
        Left = placement.DropdownLeft - ShadowMargin;
        Top = placement.DropdownTop - ShadowMargin;
        MaxHeight = placement.DropdownMaxHeight + ShadowMargin * 2;
        AccountsScroller.MaxHeight = Math.Max(132, Math.Min(480, placement.DropdownMaxHeight - 114));
    }

    public void HideDropdown()
    {
        if (!IsVisible)
        {
            return;
        }

        CollapseSearch(clear: true);
        _outsideClickMonitor.Stop();
        Hide();
    }

    private void SearchButton_Click(object sender, RoutedEventArgs e) => ExpandSearch();

    private void ClearSearch_Click(object sender, RoutedEventArgs e)
    {
        _viewModel.SearchText = string.Empty;
        SearchBox.Focus();
    }

    private void ExpandSearch()
    {
        HeaderAccountName.Visibility = Visibility.Collapsed;
        SearchButton.Visibility = Visibility.Collapsed;
        SearchPanel.Visibility = Visibility.Visible;
        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void CollapseSearch(bool clear)
    {
        if (clear)
        {
            _viewModel.SearchText = string.Empty;
        }

        SearchPanel.Visibility = Visibility.Collapsed;
        SearchButton.Visibility = Visibility.Visible;
        HeaderAccountName.Visibility = Visibility.Visible;
    }

    private void GearButton_Click(object sender, RoutedEventArgs e)
    {
        if (GearButton.ContextMenu is null)
        {
            return;
        }

        GearButton.ContextMenu.PlacementTarget = GearButton;
        GearButton.ContextMenu.IsOpen = true;
    }

    private void SettingsMenu_Opened(object sender, RoutedEventArgs e)
    {
        try
        {
            StartWithWindowsMenuItem.IsChecked = _startupRegistration.IsEnabled;
        }
        catch
        {
            StartWithWindowsMenuItem.IsChecked = false;
        }
    }

    private void StartWithWindows_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            _startupRegistration.SetEnabled(StartWithWindowsMenuItem.IsChecked);
        }
        catch (Exception exception)
        {
            CompanionDialog.Inform(this, "Startup setting could not be changed", exception.Message);
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        _aboutOpen = true;
        try
        {
            new AboutWindow { Owner = this }.ShowDialog();
        }
        finally
        {
            _aboutOpen = false;
        }
    }

    private void Exit_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();

    private void AccountMenuButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { ContextMenu: { } menu } button)
        {
            menu.PlacementTarget = button;
            menu.IsOpen = true;
        }
    }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.F && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
        {
            ExpandSearch();
            e.Handled = true;
            return;
        }

        if (e.Key != Key.Escape)
        {
            return;
        }

        if (SearchPanel.Visibility == Visibility.Visible)
        {
            CollapseSearch(clear: true);
        }
        else
        {
            RequestClose?.Invoke(this, EventArgs.Empty);
        }

        e.Handled = true;
    }

    private void Window_Deactivated(object? sender, EventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (!_aboutOpen && !(GearButton.ContextMenu?.IsOpen ?? false) && !IsActive)
            {
                HideDropdown();
            }
        }, DispatcherPriority.Background);
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsChangingAccount) && _viewModel.IsChangingAccount)
        {
            HideDropdown();
        }
    }
}
