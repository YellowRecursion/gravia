using System.Text.Json;
using Gravia.Application.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Application.Tests.Storage;

public sealed class KeyValueStoreTests
{
    [Fact]
    public async Task MissingKeysReturnDefaultsWithoutSavingThem()
    {
        var storage = new MemoryDataStore();
        using var values = new KeyValueStore(storage);
        Assert.Equal("ru", await values.GetAsync("gravia.language", "ru"));
        Assert.Equal(42, await values.GetAsync("number", 42));
        Assert.False(await values.GetAsync<bool>("flag"));
        Assert.False(await values.ExistsAsync("gravia.language"));
        await values.DeleteAsync("missing");
        Assert.Null(storage.Json);
        Assert.Equal(0, storage.Writes);
    }

    [Fact]
    public async Task ValuesRoundtripAcrossInstancesAsOrdinaryJson()
    {
        var storage = new MemoryDataStore();
        using (var values = new KeyValueStore(storage))
        {
            await values.SetAsync("integer", 42);
            await values.SetAsync("fraction", 1.25);
            await values.SetAsync("flag", true);
            await values.SetAsync("text", "Нефть");
            await values.SetAsync("object", new Example("example", 7));
        }
        using var restored = new KeyValueStore(storage);
        Assert.Equal(42, await restored.GetAsync<int>("integer"));
        Assert.Equal(1.25, await restored.GetAsync<double>("fraction"));
        Assert.True(await restored.GetAsync<bool>("flag"));
        Assert.Equal("Нефть", await restored.GetAsync<string>("text"));
        Assert.Equal(new Example("example", 7), await restored.GetAsync<Example>("object"));
        using var json = JsonDocument.Parse(storage.Json!);
        Assert.Equal(JsonValueKind.Number, json.RootElement.GetProperty("integer").ValueKind);
        Assert.Equal(JsonValueKind.Object, json.RootElement.GetProperty("object").ValueKind);
    }

    [Fact]
    public async Task NullIsPresentAndDoesNotUseTheFallback()
    {
        using var values = new KeyValueStore(new MemoryDataStore());
        await values.SetAsync<string?>("value", null);
        Assert.True(await values.ExistsAsync("value"));
        Assert.Null(await values.GetAsync<string?>("value", "fallback"));
        Assert.Null(await values.GetAsync<int?>("value", 42));
        await Assert.ThrowsAsync<JsonException>(() => values.GetAsync("value", 42));
    }

    [Fact]
    public async Task KeysAreOpaqueAndCaseSensitiveAndDeletionPreservesOtherKeys()
    {
        var storage = new MemoryDataStore();
        using var values = new KeyValueStore(storage);
        await values.SetAsync("plugin_id.example", 1);
        await values.SetAsync("PLUGIN_ID.example", 2);
        await values.SetAsync("some/path", 3);
        await values.DeleteAsync("plugin_id.example");
        var writes = storage.Writes;
        await values.DeleteAsync("plugin_id.example");
        Assert.Equal(writes, storage.Writes);
        Assert.False(await values.ExistsAsync("plugin_id.example"));
        Assert.Equal(2, await values.GetAsync<int>("PLUGIN_ID.example"));
        Assert.Equal(3, await values.GetAsync<int>("some/path"));
        await values.DeleteAsync("PLUGIN_ID.example");
        await values.DeleteAsync("some/path");
        Assert.Equal("{}", storage.Json);
    }

    [Fact]
    public async Task InvalidTypeAndMalformedDocumentsAreNotReplacedWithDefaults()
    {
        var storage = new MemoryDataStore { Json = "{\"value\":\"text\"}" };
        using var values = new KeyValueStore(storage);
        await Assert.ThrowsAsync<JsonException>(() => values.GetAsync("value", 42));
        storage.Json = "broken";
        await Assert.ThrowsAsync<JsonException>(() => values.GetAsync("value", 42));
        await Assert.ThrowsAsync<JsonException>(() => values.SetAsync("value", 42));
        await Assert.ThrowsAsync<JsonException>(() => values.DeleteAsync("value"));
        Assert.Equal("broken", storage.Json);
        Assert.Equal(0, storage.Writes);
    }

    [Fact]
    public async Task FailedSavePreservesValuesAndReleasesTheGate()
    {
        var storage = new MemoryDataStore();
        using var values = new KeyValueStore(storage);
        await values.SetAsync("value", 1);
        storage.WriteFailure = new IOException("Failed write");
        await Assert.ThrowsAsync<IOException>(() => values.SetAsync("value", 2));
        await Assert.ThrowsAsync<IOException>(() => values.DeleteAsync("value"));
        Assert.Equal(1, await values.GetAsync<int>("value"));
        storage.WriteFailure = null;
        await values.SetAsync("value", 3);
        Assert.Equal(3, await values.GetAsync<int>("value"));
    }

    [Fact]
    public async Task ReadsSeeChangesMadeThroughTheUnderlyingStore()
    {
        var storage = new MemoryDataStore();
        using var values = new KeyValueStore(storage);
        await values.SetAsync("value", 1);
        await storage.SaveAsync(KeyValueStore.FileName, new Dictionary<string, int> { ["value"] = 2 });
        Assert.Equal(2, await values.GetAsync<int>("value"));
    }

    [Fact]
    public async Task ConcurrentReadModifyWriteOperationsDoNotLoseKeys()
    {
        using var values = new KeyValueStore(new MemoryDataStore());
        await Task.WhenAll(Enumerable.Range(0, 100).Select(i => values.SetAsync($"key.{i}", i)));
        await Task.WhenAll(Enumerable.Range(0, 50).SelectMany(i => new[]
        {
            values.DeleteAsync($"key.{i}"), values.SetAsync($"new.{i}", i)
        }));
        for (var i = 0; i < 50; i++)
        {
            Assert.False(await values.ExistsAsync($"key.{i}"));
            Assert.Equal(i, await values.GetAsync<int>($"new.{i}"));
            Assert.Equal(i + 50, await values.GetAsync<int>($"key.{i + 50}"));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task InvalidKeysFailBeforeStorageAccess(string? key)
    {
        var storage = new MemoryDataStore();
        using var values = new KeyValueStore(storage);
        await Assert.ThrowsAnyAsync<ArgumentException>(() => values.GetAsync<int>(key!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => values.SetAsync(key!, 1));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => values.ExistsAsync(key!));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => values.DeleteAsync(key!));
        Assert.Equal(0, storage.Reads);
    }

    [Fact]
    public async Task CancellationFailsBeforeStorageAccess()
    {
        var storage = new MemoryDataStore();
        using var values = new KeyValueStore(storage);
        var token = new CancellationToken(true);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => values.GetAsync("key", 1, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => values.SetAsync("key", 1, token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => values.ExistsAsync("key", token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => values.DeleteAsync("key", token));
        Assert.Equal(0, storage.Reads);
        await values.SetAsync("key", 2);
        Assert.Equal(2, await values.GetAsync<int>("key"));
    }

    [Fact]
    public async Task ApplicationSingletonUsesTheRegisteredDataStore()
    {
        var storage = new MemoryDataStore();
        var services = new ServiceCollection();
        services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application, storage);
        services.AddApplicationServices();
        services.AddApplicationServices();
        services.AddSingleton<Gravia.Application.Projects.Interfaces.IProjectSessionFactory,
            Gravia.Application.Tests.Projects.UnusedProjectSessionFactory>();
        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var values = provider.GetRequiredKeyedService<KeyValueStore>(DataStoreKey.Application);
        Assert.Same(values, scope.ServiceProvider.GetRequiredKeyedService<KeyValueStore>(DataStoreKey.Application));
        Assert.Throws<InvalidOperationException>(() => provider.GetRequiredService<KeyValueStore>());
        Assert.Equal(0, storage.Reads);
        await values.SetAsync("test", 7);
        Assert.Equal(1, storage.Writes);
    }

    [Fact]
    public async Task DisposalDoesNotDisposeTheSuppliedStore()
    {
        var storage = new MemoryDataStore();
        var values = new KeyValueStore(storage);
        values.Dispose();
        Assert.False(storage.IsDisposed);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => values.GetAsync<int>("key"));
    }

    public sealed record Example(string Name, int Number);

    private sealed class MemoryDataStore : IDataStore, IDisposable
    {
        public string? Json { get; set; }
        public int Reads { get; private set; }
        public int Writes { get; private set; }
        public Exception? WriteFailure { get; set; }
        public bool IsDisposed { get; private set; }

        public async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
        {
            Assert.Equal(KeyValueStore.FileName, path);
            Reads++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            return Json is null ? null : JsonSerializer.Deserialize<T>(Json) ?? throw new JsonException();
        }

        public async Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default) where T : class
        {
            Assert.Equal(KeyValueStore.FileName, path);
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (WriteFailure is not null) throw WriteFailure;
            Json = JsonSerializer.Serialize(value);
            Writes++;
        }

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task DeleteAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task SaveBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public void Dispose() => IsDisposed = true;
    }
}
