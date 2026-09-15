using FluentValidation;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Domain.CommonExceptions;
using Gravia.Infrastructure.Projects;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Infrastructure.Tests.Projects;

public sealed class ProjectSessionFactoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gravia-project-test-" + Guid.NewGuid().ToString("N"));
    private readonly ProjectSessionFactory _factory = new(NullLoggerFactory.Instance);

    public ProjectSessionFactoryTests() => Directory.CreateDirectory(_directory);
    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task CreateMakesOnlyTheNamedDirectoryAndOpenRequiresNoManifest()
    {
        var path = Path.Combine(_directory, "Новый проект");
        await using (var session = await _factory.CreateAsync(_directory, "Новый проект"))
        {
            Assert.Equal(path, session.ProjectPath);
            Assert.Equal("Новый проект", session.Name);
            Assert.True(Directory.Exists(path));
            Assert.Empty(Directory.EnumerateFileSystemEntries(path));
        }
        Assert.Empty(Directory.EnumerateFileSystemEntries(path));
        await using var reopened = await _factory.OpenAsync(path);
        Assert.Equal(path, reopened.ProjectPath);
    }

    [Fact]
    public async Task StoresUseProjectRootAndValuesSurviveReopening()
    {
        var firstPath = Path.Combine(_directory, "first");
        await using (var session = await _factory.CreateAsync(_directory, "first"))
        {
            await session.KeyValueStore.SetAsync("value", 27);
            await session.DataStore.SaveBytesAsync("data.bin", [1, 2, 3]);
            Assert.True(File.Exists(Path.Combine(firstPath, KeyValueStore.FileName)));
            Assert.False(Directory.Exists(Path.Combine(firstPath, ".gravia")));
            Assert.False(File.Exists(Path.Combine(firstPath, "project.json")));
        }
        await using var second = await _factory.CreateAsync(_directory, "second");
        Assert.False(await second.KeyValueStore.ExistsAsync("value"));
        await using var reopened = await _factory.OpenAsync(firstPath);
        Assert.Equal(27, await reopened.KeyValueStore.GetAsync<int>("value"));
        Assert.Equal(new byte[] { 1, 2, 3 }, await reopened.DataStore.ReadBytesAsync("data.bin"));
        Assert.False(await reopened.DataStore.ExistsAsync(SettingsStorage.FileName));
    }

    [Fact]
    public async Task ExistingDirectoryIsNotReusedByCreateOrModified()
    {
        var path = Path.Combine(_directory, "existing");
        Directory.CreateDirectory(path);
        await File.WriteAllTextAsync(Path.Combine(path, "keep.txt"), "keep");

        var error = await Assert.ThrowsAsync<ValidationException>(() => _factory.CreateAsync(_directory, "existing"));

        Assert.Equal("project_directory_exists", Assert.Single(error.Errors).ErrorCode);
        Assert.Equal("keep", await File.ReadAllTextAsync(Path.Combine(path, "keep.txt")));
        await using var session = await _factory.OpenAsync(path);
        Assert.Equal("existing", session.Name);
        Assert.Single(Directory.EnumerateFileSystemEntries(path));
    }

    [Theory]
    [InlineData("../outside")]
    [InlineData("nested/project")]
    [InlineData("nested\\project")]
    [InlineData("..")]
    [InlineData(".")]
    [InlineData("")]
    [InlineData("name.")]
    [InlineData("name ")]
    public async Task InvalidNameCreatesNothing(string name)
    {
        await Assert.ThrowsAsync<ValidationException>(() => _factory.CreateAsync(_directory, name));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
    }

    [Fact]
    public async Task MissingDirectoryProducesAnErrorWithoutCreatingIt()
    {
        var missing = Path.Combine(_directory, "missing");
        var error = await Assert.ThrowsAsync<NotFoundException>(() => _factory.OpenAsync(missing));
        Assert.Equal("project_directory_not_found", error.Code);
        await Assert.ThrowsAsync<NotFoundException>(() => _factory.CreateAsync(missing, "child"));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
    }

    [Fact]
    public async Task CancelledCreateDoesNotCreateADirectory()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _factory.CreateAsync(_directory, "cancelled", cancellation.Token));
        Assert.Empty(Directory.EnumerateFileSystemEntries(_directory));
    }
}
