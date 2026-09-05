using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class ClickHitTestRunnerTests
{
    [TestMethod]
    public async Task SlowProvider_DoesNotBlockCallerOrAccumulateQueries()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var runner = new ClickHitTestRunner((x, y) =>
        {
            Interlocked.Increment(ref calls);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return ChatGptControlHit.Settings;
        });

        // Use a separate caller so a regression cannot hang the test runner.
        Task<ChatGptControlHit?>? pending = null;
        Task<ChatGptControlHit?>? latest = null;
        var caller = Task.Run(() => { pending = runner.TryHitTestAsync(10, 20); });
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            await caller.WaitAsync(TimeSpan.FromSeconds(2));
            Assert.IsNotNull(pending);
            Assert.IsFalse(pending.IsCompleted);
            for (var i = 0; i < 100; i++)
            {
                var previous = latest;
                latest = runner.TryHitTestAsync(30, 40);
                if (previous is not null) Assert.IsNull(await previous);
            }

            Assert.IsNotNull(latest);
            Assert.IsFalse(latest.IsCompleted);
            Assert.AreEqual(1, calls);
        }
        finally
        {
            release.Set();
            await caller;
            if (pending is not null) await pending;
            if (latest is not null) await latest;
        }

        Assert.IsNull(await pending!); // A later click superseded this query.
        Assert.AreEqual(ChatGptControlHit.Settings, await latest!);
        Assert.AreEqual(2, calls);
    }

    [TestMethod]
    public async Task FailedProvider_ReleasesSlotForNextClick()
    {
        var calls = 0;
        var runner = new ClickHitTestRunner((x, y) =>
            Interlocked.Increment(ref calls) == 1
                ? throw new InvalidOperationException("Provider unavailable")
                : ChatGptControlHit.File);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(async () =>
            await runner.TryHitTestAsync(1, 2));
        Assert.AreEqual(ChatGptControlHit.File, await runner.TryHitTestAsync(3, 4));
    }

    [TestMethod]
    public async Task MenuDismissal_DiscardsActiveAndQueuedHits()
    {
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var calls = 0;
        var runner = new ClickHitTestRunner((x, y) =>
        {
            Interlocked.Increment(ref calls);
            entered.Set();
            release.Wait(TimeSpan.FromSeconds(10));
            return ChatGptControlHit.File;
        });
        var active = runner.TryHitTestAsync(1, 2);
        try
        {
            Assert.IsTrue(entered.Wait(TimeSpan.FromSeconds(5)));
            var queued = runner.TryHitTestAsync(3, 4);
            runner.Invalidate();
            Assert.IsNull(await queued.WaitAsync(TimeSpan.FromSeconds(2)));
        }
        finally
        {
            release.Set();
        }

        Assert.IsNull(await active);
        Assert.AreEqual(1, calls);
        Assert.AreEqual(ChatGptControlHit.File, await runner.TryHitTestAsync(5, 6));
    }
}
