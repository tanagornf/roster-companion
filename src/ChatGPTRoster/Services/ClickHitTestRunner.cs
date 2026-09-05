namespace ChatGPTRoster.Services;

internal sealed class ClickHitTestRunner(Func<int, int, ChatGptControlHit> hitTest)
{
    private readonly object _sync = new();
    private Request? _next;
    private bool _running;
    private long _generation;

    public void Invalidate()
    {
        lock (_sync)
        {
            _generation++;
            _next?.Completion.TrySetResult(null);
            _next = null;
        }
    }

    public Task<ChatGptControlHit?> TryHitTestAsync(int x, int y)
    {
        // A provider may stop responding. Keep one active query and only the
        // latest waiting click, without blocking input or adding more workers.
        lock (_sync)
        {
            var request = new Request(x, y, ++_generation);
            if (_running)
            {
                _next?.Completion.TrySetResult(null);
                _next = request;
            }
            else
            {
                _running = true;
                _ = Task.Run(() => Run(request));
            }

            return request.Completion.Task;
        }
    }

    private void Run(Request request)
    {
        while (true)
        {
            ChatGptControlHit result = default;
            Exception? failure = null;
            try
            {
                result = hitTest(request.X, request.Y);
            }
            catch (Exception error)
            {
                failure = error;
            }

            lock (_sync)
            {
                if (request.Generation != _generation)
                    request.Completion.TrySetResult(null);
                else if (failure is not null)
                    request.Completion.TrySetException(failure);
                else
                    request.Completion.TrySetResult(result);

                if (_next is null)
                {
                    _running = false;
                    return;
                }

                request = _next;
                _next = null;
            }
        }
    }

    private sealed record Request(int X, int Y, long Generation)
    {
        public TaskCompletionSource<ChatGptControlHit?> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
