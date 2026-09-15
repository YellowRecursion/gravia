using System.Text.Json;

namespace Gravia.Application.Storage;

/// <summary>Persists named JSON values in one document using an existing data store.</summary>
/// <remarks>
/// Keys are opaque, case-sensitive strings, not paths. Missing keys return the supplied default.
/// Each operation reads the current document; values are not cached between calls.
/// Use one instance per underlying store. Dispose after all operations finish.
/// This class does not own or dispose the underlying data store.
/// </remarks>
public sealed class KeyValueStore : IDisposable, IAsyncDisposable
{
    public const string FileName = "key-values.json";

    private readonly IDataStore _storage;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public KeyValueStore(IDataStore storage)
        => _storage = storage ?? throw new ArgumentNullException(nameof(storage));

    /// <summary>
    /// Returns the value or defaultValue when the key is absent, without saving the default.
    /// Stored null remains null for nullable types. Incompatible values and read failures are errors.
    /// </summary>
    public async Task<T> GetAsync<T>(string key, T defaultValue = default!, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var values = await ReadValuesAsync(cancellationToken);
            return values.TryGetValue(key, out var value) ? value.Deserialize<T>()! : defaultValue;
        }
        finally { _gate.Release(); }
    }

    /// <summary>Saves one value immediately, preserving all other keys.</summary>
    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var values = new Dictionary<string, JsonElement>(await ReadValuesAsync(cancellationToken), StringComparer.Ordinal)
            {
                [key] = JsonSerializer.SerializeToElement(value)
            };
            await _storage.SaveAsync(FileName, values, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    /// <summary>Checks whether a key exists, including keys with a stored null value.</summary>
    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _gate.WaitAsync(cancellationToken);
        try { return (await ReadValuesAsync(cancellationToken)).ContainsKey(key); }
        finally { _gate.Release(); }
    }

    /// <summary>Removes one key and saves the remaining values. A missing key is a no-op.</summary>
    public async Task DeleteAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var values = new Dictionary<string, JsonElement>(await ReadValuesAsync(cancellationToken), StringComparer.Ordinal);
            if (values.Remove(key)) await _storage.SaveAsync(FileName, values, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<Dictionary<string, JsonElement>> ReadValuesAsync(CancellationToken token)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return await _storage.ReadAsync<Dictionary<string, JsonElement>>(FileName, token) ?? new(StringComparer.Ordinal);
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
        // Keep the managed gate usable so queued operations can observe disposal safely.
    }
}
