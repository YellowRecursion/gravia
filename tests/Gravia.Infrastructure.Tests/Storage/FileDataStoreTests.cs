using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gravia.Application.Storage;
using Gravia.Infrastructure.Storage;

namespace Gravia.Infrastructure.Tests.Storage;

public sealed class FileDataStoreTests : IDisposable
{
    private readonly DirectoryInfo _temporary = Directory.CreateTempSubdirectory("gravia-storage-tests-");
    private readonly FileDataStore _store;
    private string Root => Path.Combine(_temporary.FullName, "storage");

    public FileDataStoreTests() => _store = new FileDataStore(Root);

    [Fact]
    public async Task KeyValuesPersistInOneFileAndRemainIndependentBetweenStores()
    {
        using var application = new KeyValueStore(_store);
        using var projectStore = new FileDataStore(Path.Combine(_temporary.FullName, "project"));
        using var project = new KeyValueStore(projectStore);
        await _store.SaveBytesAsync("unrelated.bin", [1, 2]);
        await application.SetAsync("plugin.example", 42);
        await project.SetAsync("plugin.example", 7);
        using var reopenedStore = new FileDataStore(Root);
        using var reopened = new KeyValueStore(reopenedStore);
        Assert.Equal(42, await reopened.GetAsync<int>("plugin.example"));
        Assert.Equal(7, await project.GetAsync<int>("plugin.example"));
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "key-values.json")));
        Assert.Equal(42, json.RootElement.GetProperty("plugin.example").GetInt32());
        Assert.Equal(new byte[] { 1, 2 }, await _store.ReadBytesAsync("unrelated.bin"));
    }

    [Fact]
    public async Task MissingDocumentsDoNotCreateDirectories()
    {
        Assert.False(await _store.ExistsAsync("nested/document.json"));
        Assert.Null(await _store.ReadAsync<Document>("nested/document.json"));
        Assert.Null(await _store.ReadBytesAsync("nested/document.bin"));
        await _store.DeleteAsync("nested/document.json");
        Assert.False(Directory.Exists(Root));
    }

    [Fact]
    public async Task JsonRoundtripsAcrossInstancesAndUsesTheExactPath()
    {
        var original = new Document(42, "Нефть");
        await _store.SaveAsync("settings/general.json", original);
        using var second = new FileDataStore(Root);
        Assert.Equal(original, await second.ReadAsync<Document>("settings/general.json"));
        var file = Path.Combine(Root, "settings", "general.json");
        using var json = JsonDocument.Parse(await File.ReadAllTextAsync(file));
        Assert.Equal(42, json.RootElement.GetProperty("Number").GetInt32());
        Assert.Equal("Нефть", json.RootElement.GetProperty("Name").GetString());
        Assert.Equal(file, Assert.Single(Directory.GetFiles(Root, "*", SearchOption.AllDirectories)));
        Assert.True(await _store.ExistsAsync("settings/general.json"));
        Assert.False(await _store.ExistsAsync("settings"));
    }

    [Fact]
    public async Task RawBytesAreNotEncodedAndShareTheSamePathsAsJson()
    {
        byte[] bytes = [0, 255, 1, 128, 13, 10];
        await _store.SaveBytesAsync("cache/data.bin", bytes);
        Assert.Equal(bytes, await File.ReadAllBytesAsync(Path.Combine(Root, "cache", "data.bin")));
        Assert.Equal(bytes, await _store.ReadBytesAsync("cache/data.bin"));
        Assert.True(await _store.ExistsAsync("cache/data.bin"));
        await _store.SaveBytesAsync("cache/data.bin", []);
        Assert.Empty((await _store.ReadBytesAsync("cache/data.bin"))!);
        await _store.DeleteAsync("cache/data.bin");
        Assert.False(await _store.ExistsAsync("cache/data.bin"));

        var document = new Document(7, "test");
        await _store.SaveAsync("extensionless", document);
        var json = await _store.ReadBytesAsync("extensionless");
        Assert.Equal(document, JsonSerializer.Deserialize<Document>(json!));
        await _store.SaveBytesAsync("extensionless", Encoding.UTF8.GetBytes("{\"Number\":8,\"Name\":\"raw\"}"));
        Assert.Equal(new Document(8, "raw"), await _store.ReadAsync<Document>("extensionless"));
    }

    [Fact]
    public async Task SeparateRootsNeverShareDocuments()
    {
        using var other = new FileDataStore(Path.Combine(_temporary.FullName, "other"));
        await _store.SaveAsync("same.json", new Document(1, "first"));
        await other.SaveAsync("same.json", new Document(2, "second"));
        await _store.DeleteAsync("same.json");
        Assert.Equal(new Document(2, "second"), await other.ReadAsync<Document>("same.json"));
    }

    [Fact]
    public async Task DeleteRemovesOnlyOneFileAndDoesNotDeleteDirectories()
    {
        await _store.SaveAsync("group/one.json", new Document(1, "one"));
        await _store.SaveAsync("group/two.json", new Document(2, "two"));
        await _store.DeleteAsync("group/one.json");
        await _store.DeleteAsync("group/one.json");
        Assert.True(await _store.ExistsAsync("group/two.json"));
        var error = await Record.ExceptionAsync(() => _store.DeleteAsync("group"));
        Assert.True(error is IOException or UnauthorizedAccessException);
        Assert.True(await _store.ExistsAsync("group/two.json"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not json")]
    [InlineData("{\"Number\":\"wrong type\"}")]
    [InlineData("{\"Number\":1} trailing content")]
    public async Task InvalidJsonIsNotReportedAsMissing(string content)
    {
        await _store.SaveBytesAsync("invalid.json", Encoding.UTF8.GetBytes(content));
        await Assert.ThrowsAsync<JsonException>(() => _store.ReadAsync<Document>("invalid.json"));
        Assert.True(await _store.ExistsAsync("invalid.json"));
        Assert.Equal(content, Encoding.UTF8.GetString((await _store.ReadBytesAsync("invalid.json"))!));
    }

    [Fact]
    public async Task ConverterFailuresAreNotReportedAsMissingFiles()
    {
        await _store.SaveBytesAsync("document.json", Encoding.UTF8.GetBytes("{}"));
        await Assert.ThrowsAsync<FileNotFoundException>(() => _store.ReadAsync<FailingDocument>("document.json"));
    }

    [Fact]
    public async Task FailedSerializationPreservesTheOldFileAndCleansUpTemporaryFiles()
    {
        var original = new Document(1, "original");
        await _store.SaveAsync("document.json", original);
        var before = await _store.ReadBytesAsync("document.json");
        await Assert.ThrowsAsync<IOException>(() => _store.SaveAsync("document.json", new FailingDocument()));
        Assert.Equal(before, await _store.ReadBytesAsync("document.json"));
        Assert.Single(Directory.GetFiles(Root));
        await _store.SaveAsync("document.json", new Document(2, "replacement"));
        Assert.Equal(2, (await _store.ReadAsync<Document>("document.json"))!.Number);
    }

    [Fact]
    public async Task CancelledOperationsDoNotCreateOrChangeFiles()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var token = cancellation.Token;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SaveAsync("nested/doc.json", new Document(1, "test"), token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.SaveBytesAsync("nested/doc.bin", [1], token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.ReadAsync<Document>("doc.json", token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.ReadBytesAsync("doc.bin", token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.ExistsAsync("doc.json", token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.DeleteAsync("doc.json", token));
        Assert.False(Directory.Exists(Root));
        await _store.SaveBytesAsync("doc.bin", [1, 2, 3]);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.DeleteAsync("doc.bin", token));
        Assert.Equal(new byte[] { 1, 2, 3 }, await _store.ReadBytesAsync("doc.bin"));
    }

    [Fact]
    public async Task CancellationDuringSerializationPreservesTheOldFile()
    {
        await _store.SaveBytesAsync("doc.json", [1, 2, 3]);
        using var cancellation = new CancellationTokenSource();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            _store.SaveAsync("doc.json", new CancellingDocument(cancellation), cancellation.Token));
        Assert.Equal(new byte[] { 1, 2, 3 }, await _store.ReadBytesAsync("doc.json"));
        Assert.Single(Directory.GetFiles(Root));
    }

    [Fact]
    public async Task ConcurrentReadersAndWritersOnlySeeCompleteDocuments()
    {
        await _store.SaveAsync("doc.json", new Document(0, new string('a', 8192)));
        var writes = Enumerable.Range(1, 20).Select(i => _store.SaveAsync("doc.json", new Document(i, new string('a', 8192))));
        var reads = Enumerable.Range(0, 30).Select(async _ =>
        {
            Assert.Equal(8192, (await _store.ReadAsync<Document>("doc.json"))!.Name.Length);
            Assert.Equal(8192, JsonSerializer.Deserialize<Document>((await _store.ReadBytesAsync("doc.json"))!)!.Name.Length);
        });
        await Task.WhenAll(writes.Concat(reads));
        Assert.InRange((await _store.ReadAsync<Document>("doc.json"))!.Number, 1, 20);
        Assert.Single(Directory.GetFiles(Root));
    }

    [Fact]
    public async Task JsonFilesWrittenWithUtf8BomCanBeRead()
    {
        Directory.CreateDirectory(Root);
        await File.WriteAllTextAsync(Path.Combine(Root, "manual.json"),
            "{\"Number\":3,\"Name\":\"manual\"}", new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
        Assert.Equal(new Document(3, "manual"), await _store.ReadAsync<Document>("manual.json"));
    }

    [Fact]
    public async Task FailedReplacementPreservesTheOldFileAndRemovesTheTemporaryFile()
    {
        await _store.SaveBytesAsync("locked.bin", [1, 2, 3]);
        using (var locked = new FileStream(Path.Combine(Root, "locked.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
        {
            var error = await Record.ExceptionAsync(() => _store.SaveBytesAsync("locked.bin", [4, 5, 6]));
            Assert.True(error is IOException or UnauthorizedAccessException);
        }
        Assert.Equal(new byte[] { 1, 2, 3 }, await _store.ReadBytesAsync("locked.bin"));
        Assert.Single(Directory.GetFiles(Root));
    }

    [Fact]
    public async Task FileAccessErrorsAreNotConvertedToMissingDocuments()
    {
        await _store.SaveBytesAsync("locked.bin", [1, 2, 3]);
        using (var locked = new FileStream(Path.Combine(Root, "locked.bin"), FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            await Assert.ThrowsAnyAsync<IOException>(() => _store.ReadBytesAsync("locked.bin"));
        Assert.Equal(new byte[] { 1, 2, 3 }, await _store.ReadBytesAsync("locked.bin"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/absolute")]
    [InlineData("C:/absolute")]
    [InlineData("a\\b")]
    [InlineData(".")]
    [InlineData("..")]
    [InlineData("../escape")]
    [InlineData("a/../escape")]
    [InlineData("a/./b")]
    [InlineData("a//b")]
    [InlineData("a/")]
    [InlineData("a/ ")]
    [InlineData("a/b.")]
    [InlineData("a/b:stream")]
    [InlineData("a/\0b")]
    public async Task InvalidPathsAreRejectedByEveryOperation(string path)
    {
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.ExistsAsync(path));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.ReadAsync<Document>(path));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.ReadBytesAsync(path));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.SaveAsync(path, new Document(1, "test")));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.SaveBytesAsync(path, [1]));
        await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.DeleteAsync(path));
        Assert.False(Directory.Exists(Root));
    }

    [Fact]
    public async Task ConstructorRejectsRelativeRootsAndSaveRejectsNullDocuments()
    {
        Assert.Throws<ArgumentException>(() => new FileDataStore("relative"));
        Assert.Throws<ArgumentException>(() => new FileDataStore(""));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveAsync<Document>("doc.json", null!));
        await Assert.ThrowsAsync<ArgumentNullException>(() => _store.SaveBytesAsync("doc.bin", null!));
        Assert.False(Directory.Exists(Root));
    }

    public void Dispose()
    {
        _store.Dispose();
        var fullPath = Path.GetFullPath(_temporary.FullName);
        if (!string.Equals(Path.GetDirectoryName(fullPath), Path.TrimEndingDirectorySeparator(Path.GetTempPath()), StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(fullPath).StartsWith("gravia-storage-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unexpected test cleanup directory.");
        Directory.Delete(fullPath, recursive: true);
    }

    public sealed record Document(int Number, string Name);

    [JsonConverter(typeof(FailingConverter))]
    public sealed class FailingDocument;

    public sealed class FailingConverter : JsonConverter<FailingDocument>
    {
        public override FailingDocument Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new FileNotFoundException("Converter failure, not a missing stored document.");

        public override void Write(Utf8JsonWriter writer, FailingDocument value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.Flush();
            throw new IOException("Serialization failed after writing part of the document.");
        }
    }

    public sealed class CancellingDocument(CancellationTokenSource cancellation)
    {
        public int Value { get { cancellation.Cancel(); return 1; } }
    }
}
