using System.Text;
using System.Text.Json;
using FluentValidation;
using Gravia.Application;
using Gravia.Application.Projects;
using Gravia.Domain.Projects;
using Gravia.Infrastructure.Projects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Infrastructure.Tests.Projects;

public sealed class FileProjectStoreTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("gravia-store-tests-").FullName;
    private readonly FileProjectStore _store = new();
    private string ProjectPath => Path.Combine(_root, "Проект FCC");
    private string ManifestPath => Path.Combine(ProjectPath, FileProjectStore.ManifestFileName);

    [Fact]
    public async Task RealApplicationAndInfrastructureCreateAndReopenAcrossProviders()
    {
        ProjectDetails created;
        using (var provider = CreateProvider())
        using (var scope = provider.CreateScope())
            created = await scope.ServiceProvider.GetRequiredService<ISender>()
                .Send(new CreateProjectCommand(" FCC: сырьё / продукт ", ProjectPath));

        using var second = CreateProvider();
        using var secondScope = second.CreateScope();
        var reopened = await secondScope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new OpenProjectCommand(ProjectPath));
        Assert.Equal(created, reopened);
        Assert.Equal("FCC: сырьё / продукт", reopened.Name);
        Assert.Equal(ManifestPath, Assert.Single(Directory.GetFileSystemEntries(ProjectPath)));
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(ManifestPath));
        Assert.Equal(new[] { "formatVersion", "id", "name" }, document.RootElement.EnumerateObject().Select(p => p.Name));
        Assert.Equal(1, document.RootElement.GetProperty("formatVersion").GetInt32());
        Assert.Equal(created.Id, document.RootElement.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task ExistingEmptyFolderIsSupportedAndFolderRenamePreservesIdentity()
    {
        Directory.CreateDirectory(ProjectPath);
        var project = Project.Create("FCC");
        await _store.CreateAsync(project, ProjectPath, default);
        var moved = Path.Combine(_root, "renamed");
        Directory.Move(ProjectPath, moved);
        Assert.Equal(project.Id, (await _store.OpenAsync(moved, default)).Id);
    }

    [Fact]
    public async Task ExistingProjectIsNeverOverwritten()
    {
        await _store.CreateAsync(Project.Create("original"), ProjectPath, default);
        var original = await File.ReadAllBytesAsync(ManifestPath);
        await AssertCode("project_already_exists", () => _store.CreateAsync(Project.Create("replacement"), ProjectPath, default));
        Assert.Equal(original, await File.ReadAllBytesAsync(ManifestPath));
        Assert.Single(Directory.GetFileSystemEntries(ProjectPath));
    }

    [Fact]
    public async Task NonemptyFolderIsPreserved()
    {
        Directory.CreateDirectory(ProjectPath);
        var unrelated = Path.Combine(ProjectPath, "notes.txt");
        await File.WriteAllTextAsync(unrelated, "keep");
        await AssertCode("project_directory_not_empty", () => _store.CreateAsync(Project.Create("FCC"), ProjectPath, default));
        Assert.Equal("keep", await File.ReadAllTextAsync(unrelated));
        Assert.Equal(unrelated, Assert.Single(Directory.GetFileSystemEntries(ProjectPath)));
    }

    [Fact]
    public async Task MissingPassportDoesNotCreateAnything()
    {
        await AssertCode("project_not_found", () => _store.OpenAsync(ProjectPath, default));
        Assert.False(Directory.Exists(ProjectPath));
        Directory.CreateDirectory(ProjectPath);
        await AssertCode("project_not_found", () => _store.OpenAsync(ProjectPath, default));
        Assert.Empty(Directory.GetFileSystemEntries(ProjectPath));
    }

    [Fact]
    public async Task FileIsNotAcceptedAsProjectDirectory()
    {
        await File.WriteAllTextAsync(ProjectPath, "keep");
        await AssertCode("project_directory_invalid", () => _store.OpenAsync(ProjectPath, default));
        await AssertCode("project_directory_invalid", () => _store.CreateAsync(Project.Create("FCC"), ProjectPath, default));
        Assert.Equal("keep", await File.ReadAllTextAsync(ProjectPath));
    }

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{}")]
    [InlineData("{\"formatVersion\":\"1\"}")]
    [InlineData("{\"formatVersion\":1}")]
    [InlineData("{\"formatVersion\":1,\"id\":\"bad\",\"name\":\"FCC\"}")]
    [InlineData("{\"formatVersion\":1,\"id\":\"00000000-0000-0000-0000-000000000000\",\"name\":\"FCC\"}")]
    [InlineData("{\"formatVersion\":1,\"id\":\"62d1fcf1-5b9b-4205-8a37-c63ca802cc24\",\"name\":\" \"}")]
    [InlineData("{\"formatVersion\":1,\"id\":\"62d1fcf1-5b9b-4205-8a37-c63ca802cc24\",\"name\":null}")]
    [InlineData("{\"formatVersion\":1,\"id\":\"62d1fcf1-5b9b-4205-8a37-c63ca802cc24\",\"name\":12}")]
    [InlineData("{\"formatVersion\":1,\"id\":\"62d1fcf1-5b9b-4205-8a37-c63ca802cc24\",\"name\":\"FCC\",\"name\":\"other\"}")]
    [InlineData("{\"formatVersion\":1,\"id\":\"62d1fcf1-5b9b-4205-8a37-c63ca802cc24\",\"name\":\"FCC\",\"unknown\":true}")]
    public async Task InvalidPassportIsRejectedWithoutRepairOrRewrite(string content)
    {
        Directory.CreateDirectory(ProjectPath);
        await File.WriteAllTextAsync(ManifestPath, content);
        await AssertCode("project_manifest_invalid", () => _store.OpenAsync(ProjectPath, default));
        Assert.Equal(content, await File.ReadAllTextAsync(ManifestPath));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    [InlineData(-1)]
    public async Task UnsupportedVersionIsNotInterpretedAsCurrentFormat(int version)
    {
        Directory.CreateDirectory(ProjectPath);
        var content = $"{{\"formatVersion\":{version}}}";
        await File.WriteAllTextAsync(ManifestPath, content);
        await AssertCode("project_format_unsupported", () => _store.OpenAsync(ProjectPath, default));
        Assert.Equal(content, await File.ReadAllTextAsync(ManifestPath));
    }

    [Fact]
    public async Task Utf8BomFromExternalEditorIsAccepted()
    {
        await _store.CreateAsync(Project.Create("FCC"), ProjectPath, default);
        var text = await File.ReadAllTextAsync(ManifestPath);
        await File.WriteAllTextAsync(ManifestPath, text, new UTF8Encoding(true));
        Assert.Equal("FCC", (await _store.OpenAsync(ProjectPath, default)).Name);
    }

    [Fact]
    public async Task OversizedPassportIsRejectedOnBothWriteAndRead()
    {
        await AssertCode("project_manifest_too_large", () => _store.CreateAsync(
            Project.Create(new string('x', FileProjectStore.MaxManifestBytes)), ProjectPath, default));
        Assert.False(Directory.Exists(ProjectPath));
        Directory.CreateDirectory(ProjectPath);
        await File.WriteAllBytesAsync(ManifestPath, new byte[FileProjectStore.MaxManifestBytes + 1]);
        await AssertCode("project_manifest_too_large", () => _store.OpenAsync(ProjectPath, default));
    }

    [Fact]
    public async Task AlreadyCancelledOperationsDoNotTouchDisk()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.CreateAsync(Project.Create("FCC"), ProjectPath, cancellation.Token));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _store.OpenAsync(ProjectPath, cancellation.Token));
        Assert.False(Directory.Exists(ProjectPath));
    }

    [Fact]
    public async Task ConcurrentCreatorsProduceOneCompletePassportWithoutTemporaryFiles()
    {
        var projects = Enumerable.Range(0, 12).Select(index => Project.Create($"FCC {index}")).ToArray();
        var results = await Task.WhenAll(projects.Select(project => Task.Run(async () =>
        {
            try
            {
                await _store.CreateAsync(project, ProjectPath, default);
                return project;
            }
            catch (ValidationException exception)
            {
                Assert.Contains(Assert.Single(exception.Errors).ErrorCode, new[] { "project_already_exists", "project_directory_not_empty" });
                return null;
            }
        })));
        var winner = Assert.Single(results, result => result is not null);
        var loaded = await _store.OpenAsync(ProjectPath, default);
        Assert.Equal(winner!.Id, loaded.Id);
        Assert.Equal(winner.Name, loaded.Name);
        Assert.Equal(ManifestPath, Assert.Single(Directory.GetFileSystemEntries(ProjectPath)));
    }

    private static async Task AssertCode(string code, Func<Task> action)
    {
        var error = Assert.Single((await Assert.ThrowsAsync<ValidationException>(action)).Errors);
        Assert.Equal(code, error.ErrorCode);
        Assert.Equal("directoryPath", error.PropertyName);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddInfrastructureServices();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }

    public void Dispose()
    {
        var root = Path.GetFullPath(_root);
        var temp = Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())) + Path.DirectorySeparatorChar;
        if (!root.StartsWith(temp, StringComparison.OrdinalIgnoreCase) ||
            !Path.GetFileName(root).StartsWith("gravia-store-tests-", StringComparison.Ordinal))
            throw new InvalidOperationException("Unsafe test cleanup path.");
        Directory.Delete(root, recursive: true);
    }
}
