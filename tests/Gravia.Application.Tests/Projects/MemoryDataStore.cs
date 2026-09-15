using System.Collections.Concurrent;
using System.Text.Json;
using Gravia.Application.Storage;

namespace Gravia.Application.Tests.Projects;

internal sealed class MemoryDataStore : IDataStore, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, byte[]> _documents = new();
    public int DisposeCount { get; private set; }
    public Exception? WriteFailure { get; set; }
    public Func<string, CancellationToken, Task>? BeforeSave { get; set; }

    public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(_documents.ContainsKey(path));

    public Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
        => Task.FromResult(_documents.TryGetValue(path, out var data) ? JsonSerializer.Deserialize<T>(data) : null);

    public async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default) where T : class
    {
        if (BeforeSave is not null) await BeforeSave(path, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        if (WriteFailure is not null) throw WriteFailure;
        _documents[path] = JsonSerializer.SerializeToUtf8Bytes(value);
    }

    public Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken = default)
        => Task.FromResult(_documents.TryGetValue(path, out var data) ? data.ToArray() : null);

    public Task SaveBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
    {
        _documents[path] = data.ToArray();
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
    {
        _documents.TryRemove(path, out _);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        DisposeCount++;
        return ValueTask.CompletedTask;
    }
}
