using System.Windows.Threading;

namespace ChatGPTRoster.Services;

/// <summary>
/// Owns a low-level hook's message loop independently of the WPF UI thread.
/// Windows waits for that loop even for mouse messages the callback ignores.
/// Installation and removal must both happen on the owning thread.
/// </summary>
internal sealed class MouseHookThread(Action install, Action uninstall) : IDisposable
{
    private Thread? _thread;
    private Dispatcher? _dispatcher;

    public void Start()
    {
        if (_thread is not null) return;

        var ready = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            var installed = false;
            try
            {
                install();
                installed = true;
                ready.SetResult(dispatcher);
                Dispatcher.Run();
            }
            catch (Exception error)
            {
                ready.TrySetException(error);
            }
            finally
            {
                if (installed) uninstall();
            }
        })
        {
            IsBackground = true,
            Name = "Roster mouse hook"
        };
        _thread.SetApartmentState(ApartmentState.STA);
        _thread.Start();

        try
        {
            _dispatcher = ready.Task.GetAwaiter().GetResult();
        }
        catch
        {
            _thread.Join();
            _thread = null;
            throw;
        }
    }

    public void Stop()
    {
        if (_thread is null) return;
        _dispatcher!.BeginInvokeShutdown(DispatcherPriority.Send);
        _thread.Join();
        _thread = null;
        _dispatcher = null;
    }

    public void Dispose() => Stop();
}
