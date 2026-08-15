using System.IO;

namespace ChatGPTRoster.Services;

internal static class AtomicFile
{
    public static async Task CopyAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken = default)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
                                   ?? throw new InvalidOperationException("The destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");

        try
        {
            await using (var source = new FileStream(
                             sourcePath,
                             FileMode.Open,
                             FileAccess.Read,
                             FileShare.Read,
                             81920,
                             FileOptions.Asynchronous | FileOptions.SequentialScan))
            await using (var destination = new FileStream(
                             temporaryPath,
                             FileMode.CreateNew,
                             FileAccess.Write,
                             FileShare.None,
                             81920,
                             FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await source.CopyToAsync(destination, cancellationToken);
                await destination.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    public static async Task WriteTextAsync(
        string destinationPath,
        string content,
        CancellationToken cancellationToken = default)
    {
        var destinationDirectory = Path.GetDirectoryName(destinationPath)
                                   ?? throw new InvalidOperationException("The destination has no parent directory.");
        Directory.CreateDirectory(destinationDirectory);
        var temporaryPath = destinationPath + ".tmp." + Guid.NewGuid().ToString("N");

        try
        {
            await File.WriteAllTextAsync(temporaryPath, content, cancellationToken);
            await using (var stream = new FileStream(
                             temporaryPath,
                             FileMode.Open,
                             FileAccess.ReadWrite,
                             FileShare.None,
                             4096,
                             FileOptions.WriteThrough))
            {
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, destinationPath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
