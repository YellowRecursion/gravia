using System.Text.Json;
using Gravia.Application.Storage;

namespace Gravia.Infrastructure.Storage;

/// <summary>Stores JSON documents and raw bytes under a fixed root directory.</summary>
/// <remarks>
/// Paths map directly to files; extensions are not added automatically.
/// Saves replace files only after writing a temporary file in the same directory.
/// Operations are serialized within this instance. Dispose after all operations finish.
/// </remarks>
public sealed class FileDataStore : IDataStore, IDisposable, IAsyncDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _directory;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public FileDataStore(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Path.IsPathFullyQualified(directory))
            throw new ArgumentException("The storage directory must be an absolute path.", nameof(directory));

        _directory = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var file = ResolvePath(path);
            try { return (File.GetAttributes(file) & FileAttributes.Directory) == 0; }
            catch (FileNotFoundException) { return false; }
            catch (DirectoryNotFoundException) { return false; }
        }
        finally { _gate.Release(); }
    }

    public async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = OpenRead(ResolvePath(path));
            if (stream is null) return null;

            return await JsonSerializer.DeserializeAsync<T>(stream, JsonOptions, cancellationToken)
                ?? throw new JsonException("A stored document must not be null.");
        }
        finally { _gate.Release(); }
    }

    public Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default) where T : class
    {
        ArgumentNullException.ThrowIfNull(value);
        return WriteAsync(path, (stream, token) => JsonSerializer.SerializeAsync(stream, value, JsonOptions, token), cancellationToken);
    }

    public async Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await using var stream = OpenRead(ResolvePath(path));
            if (stream is null) return null;

            var data = new byte[checked((int)stream.Length)];
            await stream.ReadExactlyAsync(data, cancellationToken);
            return data;
        }
        finally { _gate.Release(); }
    }

    public Task SaveBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        return WriteAsync(path, (stream, token) => stream.WriteAsync(data.AsMemory(), token).AsTask(), cancellationToken);
    }

    public async Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var file = ResolvePath(path);
            cancellationToken.ThrowIfCancellationRequested();
            try { File.Delete(file); }
            catch (DirectoryNotFoundException) { }
        }
        finally { _gate.Release(); }
    }

    private async Task WriteAsync(string path, Func<Stream, CancellationToken, Task> write, CancellationToken token)
    {
        await _gate.WaitAsync(token);
        string? ownedTemporary = null;
        try
        {
            var file = ResolvePath(path);
            var parent = Path.GetDirectoryName(file)!;
            Directory.CreateDirectory(parent);
            var temporary = Path.Combine(parent, $".gravia-{Guid.NewGuid():N}.tmp");
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous))
            {
                ownedTemporary = temporary;
                await write(stream, token);
                await stream.FlushAsync(token);
                stream.Flush(flushToDisk: true);
            }
            token.ThrowIfCancellationRequested();
            if (File.Exists(file)) File.Replace(temporary, file, destinationBackupFileName: null);
            else File.Move(temporary, file);
            ownedTemporary = null;
        }
        finally
        {
            if (ownedTemporary is not null)
            {
                // Cleanup must not hide the original write failure.
                try { File.Delete(ownedTemporary); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
            _gate.Release();
        }
    }

    private static FileStream? OpenRead(string file)
    {
        try
        {
            return new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete,
                4096, FileOptions.Asynchronous | FileOptions.SequentialScan);
        }
        catch (FileNotFoundException) { return null; }
        catch (DirectoryNotFoundException) { return null; }
    }

    private string ResolvePath(string path)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var segments = path.Split('/');
        if (Path.IsPathRooted(path) || path.Contains('\\') ||
            segments.Any(segment => segment.Length == 0 || segment is "." or ".." ||
                segment.EndsWith('.') || segment.EndsWith(' ') ||
                segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || segment.Contains(':')))
            throw new ArgumentException("The document path must contain valid relative path segments separated by '/'.", nameof(path));

        var file = _directory;
        // Do not follow an existing link or junction into an unrelated directory.
        // This is a path guard, not a sandbox against external filesystem changes.
        foreach (var segment in segments.Prepend(string.Empty))
        {
            file = Path.Combine(file, segment);
            try
            {
                if ((File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0)
                    throw new IOException("Storage paths through symbolic links or junctions are not supported.");
            }
            catch (FileNotFoundException) { }
            catch (DirectoryNotFoundException) { }
        }
        return file;
    }

    public void Dispose()
    {
        _gate.Wait();
        try { _disposed = true; }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { _disposed = true; }
        finally { _gate.Release(); }
        // Wait for active I/O; queued operations still need the gate to report disposal.
    }
}
