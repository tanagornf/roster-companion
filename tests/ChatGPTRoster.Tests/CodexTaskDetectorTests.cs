using System.Text.Json;
using ChatGPTRoster.Services;

namespace ChatGPTRoster.Tests;

[TestClass]
public sealed class CodexTaskDetectorTests
{
    [TestMethod]
    public void ReadLatestLifecycle_DistinguishesRunningCompleteAndFinalAnswer()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path,
            [
                JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started" } }),
                JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "message" } }),
                JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_complete" } })
            ]);
            Assert.AreEqual(TaskActivityState.Idle, CodexTaskDetector.ReadLatestLifecycle(path));

            File.AppendAllText(path, JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started" } }) + Environment.NewLine);
            Assert.AreEqual(TaskActivityState.Running, CodexTaskDetector.ReadLatestLifecycle(path));

            File.AppendAllText(path, JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "agent_message", phase = "final_answer" } }) + Environment.NewLine);
            Assert.AreEqual(TaskActivityState.Idle, CodexTaskDetector.ReadLatestLifecycle(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadLatestLifecycle_SkipsPartiallyWrittenTrailingLine()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path,
            [
                JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started" } }),
                JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_complete" } }),
                "{\"type\":\"event_msg\",\"payload\":"
            ]);

            Assert.AreEqual(TaskActivityState.Idle, CodexTaskDetector.ReadLatestLifecycle(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadLatestLifecycle_AcceptsResponseItemFinalAnswer()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path,
            [
                JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started" } }),
                JsonSerializer.Serialize(new { type = "response_item", payload = new { type = "message", phase = "final_answer" } })
            ]);

            Assert.AreEqual(TaskActivityState.Idle, CodexTaskDetector.ReadLatestLifecycle(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ReadLatestLifecycle_ReadsSessionWhileCodexIsAppending()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllLines(path,
            [
                JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_started" } }),
                JsonSerializer.Serialize(new { type = "event_msg", payload = new { type = "task_complete" } })
            ]);

            using var writer = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            writer.Write("{\"type\":\"event_msg\",\"payload\":"u8);
            writer.Flush();

            Assert.AreEqual(TaskActivityState.Idle, CodexTaskDetector.ReadLatestLifecycle(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public async Task GetActivityState_FailsSafeWhenHeldLockHasNoSession()
    {
        var root = Path.Combine(Path.GetTempPath(), "ChatGPTRoster.TaskTests", Guid.NewGuid().ToString("N"));
        var locks = Path.Combine(root, "thread-writer-locks");
        Directory.CreateDirectory(locks);
        var lockPath = Path.Combine(locks, "thread-123.lock");
        await File.WriteAllTextAsync(lockPath, string.Empty);

        try
        {
            await using var held = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            var detector = new CodexTaskDetector(root);

            Assert.AreEqual(TaskActivityState.Unknown, await detector.GetActivityStateAsync());
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
