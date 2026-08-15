using System.IO;
using System.Text;
using System.Text.Json;

namespace ChatGPTRoster.Services;

public enum TaskActivityState
{
    Idle,
    Running,
    Unknown
}

public interface ICodexTaskDetector
{
    Task<TaskActivityState> GetActivityStateAsync(CancellationToken cancellationToken = default);
}

public sealed class CodexTaskDetector : ICodexTaskDetector
{
    private readonly string _codexHome;

    public CodexTaskDetector(string codexHome)
    {
        _codexHome = codexHome;
    }

    public async Task<TaskActivityState> GetActivityStateAsync(CancellationToken cancellationToken = default)
    {
        var first = await InspectAsync(cancellationToken);
        if (first != TaskActivityState.Running)
        {
            return first;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(400), cancellationToken);
        return await InspectAsync(cancellationToken);
    }

    private Task<TaskActivityState> InspectAsync(CancellationToken cancellationToken)
    {
        return Task.Run(() =>
        {
            try
            {
                var locksDirectory = Path.Combine(_codexHome, "thread-writer-locks");
                if (!Directory.Exists(locksDirectory))
                {
                    return TaskActivityState.Idle;
                }

                var heldThreadIds = new List<string>();
                foreach (var lockPath in Directory.EnumerateFiles(locksDirectory, "*.lock", SearchOption.TopDirectoryOnly))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (Path.GetFileName(lockPath).Equals(".coordination.lock", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (IsHeld(lockPath))
                    {
                        heldThreadIds.Add(Path.GetFileNameWithoutExtension(lockPath));
                    }
                }

                if (heldThreadIds.Count == 0)
                {
                    return TaskActivityState.Idle;
                }

                var sessionsDirectory = Path.Combine(_codexHome, "sessions");
                if (!Directory.Exists(sessionsDirectory))
                {
                    return TaskActivityState.Unknown;
                }

                var sawUnknown = false;
                foreach (var threadId in heldThreadIds)
                {
                    var sessionPath = Directory
                        .EnumerateFiles(sessionsDirectory, $"*{threadId}*.jsonl", SearchOption.AllDirectories)
                        .OrderByDescending(File.GetLastWriteTimeUtc)
                        .FirstOrDefault();
                    if (sessionPath is null)
                    {
                        sawUnknown = true;
                        continue;
                    }

                    var lifecycle = ReadLatestLifecycle(sessionPath);
                    if (lifecycle == TaskActivityState.Running)
                    {
                        return TaskActivityState.Running;
                    }

                    sawUnknown |= lifecycle == TaskActivityState.Unknown;
                }

                return sawUnknown ? TaskActivityState.Unknown : TaskActivityState.Idle;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return TaskActivityState.Unknown;
            }
        }, cancellationToken);
    }

    internal static TaskActivityState ReadLatestLifecycle(string sessionPath)
    {
        try
        {
            foreach (var line in ReadLinesBackwards(sessionPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var document = JsonDocument.Parse(line);
                    var root = document.RootElement;
                    if (!root.TryGetProperty("payload", out var payload) || payload.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    var type = payload.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String
                        ? typeValue.GetString()
                        : null;
                    if (type == "task_complete")
                    {
                        return TaskActivityState.Idle;
                    }

                    var isFinalMessage = (type is "agent_message" or "message")
                                         && payload.TryGetProperty("phase", out var phase)
                                         && phase.ValueKind == JsonValueKind.String
                                         && phase.GetString() == "final_answer";
                    if (isFinalMessage)
                    {
                        return TaskActivityState.Idle;
                    }

                    if (type == "task_started")
                    {
                        return TaskActivityState.Running;
                    }
                }
                catch (JsonException)
                {
                    // Codex may be in the middle of appending the newest JSONL line.
                    // Skip that partial line and continue to the last complete lifecycle event.
                }
            }

            return TaskActivityState.Unknown;
        }
        catch
        {
            return TaskActivityState.Unknown;
        }
    }

    private static IEnumerable<string> ReadLinesBackwards(string path)
    {
        const int bufferSize = 64 * 1024;
        using var stream = new FileStream(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete,
            bufferSize,
            FileOptions.SequentialScan);

        var buffer = new byte[bufferSize];
        var reversedLine = new List<byte>();
        var position = stream.Length;

        while (position > 0)
        {
            var bytesToRead = (int)Math.Min(buffer.Length, position);
            position -= bytesToRead;
            stream.Position = position;

            var bytesRead = 0;
            while (bytesRead < bytesToRead)
            {
                var read = stream.Read(buffer, bytesRead, bytesToRead - bytesRead);
                if (read == 0)
                {
                    break;
                }

                bytesRead += read;
            }

            for (var index = bytesRead - 1; index >= 0; index--)
            {
                if (buffer[index] == (byte)'\n')
                {
                    if (reversedLine.Count > 0)
                    {
                        yield return DecodeReversedLine(reversedLine);
                        reversedLine.Clear();
                    }

                    continue;
                }

                reversedLine.Add(buffer[index]);
            }
        }

        if (reversedLine.Count > 0)
        {
            yield return DecodeReversedLine(reversedLine);
        }
    }

    private static string DecodeReversedLine(List<byte> reversedLine)
    {
        var bytes = reversedLine.ToArray();
        Array.Reverse(bytes);
        return Encoding.UTF8.GetString(bytes).TrimEnd('\r');
    }

    private static bool IsHeld(string lockPath)
    {
        try
        {
            using var stream = new FileStream(lockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            return false;
        }
        catch (IOException)
        {
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }
}
