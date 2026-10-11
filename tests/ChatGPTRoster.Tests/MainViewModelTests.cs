using System.ComponentModel;
using System.Windows.Threading;
using ChatGPTRoster.Models;
using ChatGPTRoster.Services;
using ChatGPTRoster.ViewModels;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class MainViewModelTests
{
    [TestMethod]
    public Task Switch_WaitingForTaskDetectionBlocksOtherAccountOperations() => RunOnDispatcherAsync(async () =>
    {
        var fixture = new Fixture();
        var viewModel = fixture.ViewModel;
        await viewModel.InitializeAsync();
        var target = viewModel.Accounts.Single(account => !account.IsActive);
        var detection = new TaskCompletionSource<TaskActivityState>();
        fixture.Detector.Result = detection.Task;

        target.SwitchAccountCommand.Execute(null);

        Assert.IsTrue(viewModel.IsBusy);
        Assert.IsFalse(viewModel.AddAccountCommand.CanExecute(null));
        Assert.IsFalse(viewModel.RefreshCommand.CanExecute(null));
        Assert.IsFalse(target.SwitchAccountCommand.CanExecute(null));
        Assert.IsFalse(target.RemoveAccountCommand.CanExecute(null));
        target.SwitchAccountCommand.Execute(null);
        target.RemoveAccountCommand.Execute(null);
        viewModel.AddAccountCommand.Execute(null);
        var usageCalls = fixture.Usage.Calls;
        await viewModel.RefreshDueAccountsAsync(dropdownOpened: true, networkAllowed: true);
        Assert.AreEqual(usageCalls, fixture.Usage.Calls);
        Assert.AreEqual(0, fixture.Files.Removals);
        Assert.AreEqual(0, fixture.Enrollment.Calls);

        var completed = WhenIdleAsync(viewModel);
        detection.SetResult(TaskActivityState.Idle);
        await completed;

        Assert.AreEqual(1, fixture.Switcher.Calls);
        Assert.IsTrue(target.IsActive);
        Assert.IsTrue(viewModel.AddAccountCommand.CanExecute(null));
    });

    [TestMethod]
    public Task BackgroundRefresh_BlocksRemovalUntilUsageReadCompletes() => RunOnDispatcherAsync(async () =>
    {
        var fixture = new Fixture();
        var viewModel = fixture.ViewModel;
        await viewModel.InitializeAsync();
        var target = viewModel.Accounts.Single(account => !account.IsActive);
        target.Profile.Usage.LastUpdatedAt = null;
        var usage = new TaskCompletionSource<UsageSnapshot>();
        fixture.Usage.Read = _ => usage.Task;

        var refreshing = viewModel.RefreshDueAccountsAsync(dropdownOpened: true, networkAllowed: true);
        Assert.IsTrue(viewModel.IsBusy);
        Assert.IsFalse(target.RemoveAccountCommand.CanExecute(null));
        target.RemoveAccountCommand.Execute(null);
        target.SwitchAccountCommand.Execute(null);
        viewModel.AddAccountCommand.Execute(null);

        usage.SetResult(new UsageSnapshot { LastUpdatedAt = DateTimeOffset.UtcNow });
        await refreshing;

        Assert.AreEqual(2, viewModel.Accounts.Count);
        Assert.AreEqual(0, fixture.Files.Removals);
        Assert.AreEqual(0, fixture.Switcher.Calls);
        Assert.AreEqual(0, fixture.Enrollment.Calls);
        Assert.IsFalse(viewModel.IsBusy);
        Assert.IsTrue(target.RemoveAccountCommand.CanExecute(null));
    });

    [TestMethod]
    public Task BackgroundRefresh_WhenNothingIsDuePreservesStatusAndDoesNotSave() => RunOnDispatcherAsync(async () =>
    {
        var fixture = new Fixture();
        await fixture.ViewModel.InitializeAsync();
        var saves = fixture.Store.Saves;
        var usageCalls = fixture.Usage.Calls;
        var status = fixture.ViewModel.StatusText;

        await fixture.ViewModel.RefreshDueAccountsAsync(dropdownOpened: true, networkAllowed: true);

        Assert.AreEqual(saves, fixture.Store.Saves);
        Assert.AreEqual(usageCalls, fixture.Usage.Calls);
        Assert.AreEqual(status, fixture.ViewModel.StatusText);
    });

    [TestMethod]
    public Task Enrollment_CanStillBeCancelledWhileOtherCommandsAreDisabled() => RunOnDispatcherAsync(async () =>
    {
        var fixture = new Fixture();
        var viewModel = fixture.ViewModel;
        await viewModel.InitializeAsync();
        fixture.Enrollment.Enroll = async token =>
        {
            await Task.Delay(Timeout.Infinite, token);
            return new AccountProfile();
        };

        viewModel.AddAccountCommand.Execute(null);

        Assert.IsTrue(viewModel.IsEnrolling);
        Assert.IsTrue(viewModel.AddAccountCommand.CanExecute(null));
        Assert.IsFalse(viewModel.RefreshCommand.CanExecute(null));
        Assert.IsTrue(viewModel.Accounts.All(account => !account.RenameAliasCommand.CanExecute(null)));
        var completed = WhenIdleAsync(viewModel);
        viewModel.AddAccountCommand.Execute(null);
        await completed;

        Assert.AreEqual(1, fixture.Enrollment.Calls);
        Assert.IsFalse(viewModel.IsEnrolling);
        Assert.AreEqual("Account login cancelled", viewModel.StatusText);
        Assert.IsTrue(viewModel.RefreshCommand.CanExecute(null));
    });

    [TestMethod]
    public Task Enrollment_UnexpectedFileFailureIsReportedAndReleasesCommands() => RunOnDispatcherAsync(async () =>
    {
        var fixture = new Fixture();
        await fixture.ViewModel.InitializeAsync();
        fixture.Enrollment.Enroll = _ => Task.FromException<AccountProfile>(new IOException("Synthetic file failure"));

        fixture.ViewModel.AddAccountCommand.Execute(null);
        await WhenIdleAsync(fixture.ViewModel);

        Assert.IsFalse(fixture.ViewModel.IsEnrolling);
        Assert.AreEqual("Account was not added", fixture.ViewModel.StatusText);
        CollectionAssert.Contains(fixture.Messages, "Account login failed");
        Assert.IsTrue(fixture.ViewModel.RefreshCommand.CanExecute(null));
    });

    [TestMethod]
    public Task RenameActiveAlias_UpdatesSelectorAndEmptySearchState() => RunOnDispatcherAsync(async () =>
    {
        var fixture = new Fixture();
        var viewModel = fixture.ViewModel;
        await viewModel.InitializeAsync();
        viewModel.SearchText = "Original";
        var changes = new List<string?>();
        viewModel.PropertyChanged += (_, args) => changes.Add(args.PropertyName);

        viewModel.ActiveAccount!.RenameAliasCommand.Execute(null);
        await WhenIdleAsync(viewModel);

        Assert.AreEqual("Renamed", viewModel.SelectorLabel);
        Assert.IsFalse(viewModel.HasSearchResults);
        CollectionAssert.Contains(changes, nameof(MainViewModel.SelectorLabel));
        CollectionAssert.Contains(changes, nameof(MainViewModel.HasSearchResults));
    });

    private static Task WhenIdleAsync(MainViewModel viewModel)
    {
        if (!viewModel.IsBusy)
        {
            return Task.CompletedTask;
        }

        var completion = new TaskCompletionSource();
        PropertyChangedEventHandler? handler = null;
        handler = (_, args) =>
        {
            if (args.PropertyName == nameof(MainViewModel.IsBusy) && !viewModel.IsBusy)
            {
                viewModel.PropertyChanged -= handler;
                completion.SetResult();
            }
        };
        viewModel.PropertyChanged += handler;
        return completion.Task;
    }

    private static async Task RunOnDispatcherAsync(Func<Task> test)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            dispatcher.InvokeAsync(async () =>
            {
                try
                {
                    await test();
                    completion.SetResult();
                }
                catch (Exception exception)
                {
                    completion.SetException(exception);
                }
                finally
                {
                    dispatcher.BeginInvokeShutdown(DispatcherPriority.Background);
                }
            });
            Dispatcher.Run();
        })
        { IsBackground = true };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private sealed class Fixture
    {
        public FakeStore Store { get; } = new();
        public FakeUsage Usage { get; } = new();
        public FakeEnrollment Enrollment { get; } = new();
        public FakeFiles Files { get; } = new();
        public FakeSwitcher Switcher { get; } = new();
        public FakeDetector Detector { get; } = new();
        public List<string> Messages { get; } = [];
        public MainViewModel ViewModel { get; }

        public Fixture()
        {
            ViewModel = new MainViewModel(Store, Enrollment, Usage, Files, Switcher, Detector,
                new FakeDiscovery(), new FakeSettings(), _ => "Renamed", _ => true, _ => true,
                _ => true, _ => true, (title, _) => Messages.Add(title));
        }
    }

    private sealed class FakeStore : IProfileStore
    {
        public int Saves { get; private set; }
        public Task<IReadOnlyList<AccountProfile>> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountProfile>>([
                new() { Id = "current", AccountId = "current", Alias = "Original", IsActive = true },
                new() { Id = "target", AccountId = "target", Alias = "Target" }
            ]);
        public Task SaveAsync(IReadOnlyCollection<AccountProfile> profiles, CancellationToken cancellationToken = default)
        {
            Saves++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeUsage : IUsageService
    {
        public int Calls { get; private set; }
        public Func<AccountProfile, Task<UsageSnapshot>> Read { get; set; } =
            _ => Task.FromResult(new UsageSnapshot { LastUpdatedAt = DateTimeOffset.UtcNow });
        public Task<UsageSnapshot> GetUsageAsync(AccountProfile profile, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Read(profile);
        }
    }

    private sealed class FakeEnrollment : IAccountEnrollmentService
    {
        public int Calls { get; private set; }
        public Func<CancellationToken, Task<AccountProfile>> Enroll { get; set; } =
            _ => Task.FromResult(new AccountProfile { AccountId = "new" });
        public Task<AccountProfile> EnrollAsync(AccountProfile? currentProfile = null, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Enroll(cancellationToken);
        }
    }

    private sealed class FakeFiles : IProfileFileService
    {
        public int Removals { get; private set; }
        public Task RemoveManagedProfileAsync(AccountProfile profile, CancellationToken cancellationToken = default)
        {
            Removals++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSwitcher : IAccountSwitcher
    {
        public int Calls { get; private set; }
        public Task SwitchAsync(AccountProfile currentProfile, AccountProfile targetProfile, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeDetector : ICodexTaskDetector
    {
        public Task<TaskActivityState> Result { get; set; } = Task.FromResult(TaskActivityState.Idle);
        public Task<TaskActivityState> GetActivityStateAsync(CancellationToken cancellationToken = default) => Result;
    }

    private sealed class FakeDiscovery : IActiveAccountDiscoveryService
    {
        public Task<IReadOnlyList<AccountProfile>> SynchronizeAsync(
            IReadOnlyCollection<AccountProfile> profiles, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<AccountProfile>>(profiles.ToArray());
    }

    private sealed class FakeSettings : IAppSettingsService
    {
        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default) => Task.FromResult(new AppSettings());
        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
