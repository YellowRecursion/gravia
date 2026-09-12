using FluentValidation;
using Gravia.Application.Projects;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Application.Tests.Projects;

public sealed class OpenProjectTests
{
    [Fact]
    public async Task CreatesAndReopensThroughRegisteredHandlersAndValidators()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        Assert.IsType<OpenProjectCommandValidator>(Assert.Single(scope.ServiceProvider.GetServices<IValidator<OpenProjectCommand>>()));
        Assert.IsType<OpenProjectCommandHandler>(scope.ServiceProvider.GetRequiredService<IRequestHandler<OpenProjectCommand, ProjectDetails>>());
        using var cancellation = new CancellationTokenSource();
        var directory = TestProjectStore.DirectoryPath;
        var created = await sender.Send(new CreateProjectCommand("  FCC  ", directory), cancellation.Token);
        var reopened = await sender.Send(new OpenProjectCommand(Path.Combine(directory, "child", "..")), cancellation.Token);
        Assert.Equal(created, reopened);
        Assert.Equal(directory, reopened.DirectoryPath);
        Assert.Equal("FCC", reopened.Name);
        var store = (TestProjectStore)scope.ServiceProvider.GetRequiredService<IProjectStore>();
        Assert.Equal(2, store.Calls);
        Assert.Equal(cancellation.Token, store.LastToken);
    }

    [Theory]
    [InlineData(null, "project_directory_required")]
    [InlineData("", "project_directory_required")]
    [InlineData("  ", "project_directory_required")]
    [InlineData("relative/path", "project_directory_absolute_required")]
    [InlineData("bad\0path", "project_directory_invalid")]
    public async Task BothCommandsRejectInvalidPathsBeforeCallingStore(string? path, string code)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var create = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new CreateProjectCommand("FCC", path!)));
        var open = await Assert.ThrowsAsync<ValidationException>(() => sender.Send(new OpenProjectCommand(path!)));
        foreach (var exception in new[] { create, open })
        {
            var error = Assert.Single(exception.Errors);
            Assert.Equal("directoryPath", error.PropertyName);
            Assert.Equal(code, error.ErrorCode);
        }
        Assert.Equal(0, ((TestProjectStore)scope.ServiceProvider.GetRequiredService<IProjectStore>()).Calls);
    }

    [Fact]
    public async Task CollectsNameAndDirectoryErrors()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var error = await Assert.ThrowsAsync<ValidationException>(() => scope.ServiceProvider
            .GetRequiredService<ISender>().Send(new CreateProjectCommand("", "")));
        Assert.Equal(new[] { "name", "directoryPath" }, error.Errors.Select(failure => failure.PropertyName));
    }

    [Fact]
    public async Task CancelledOpenDoesNotTouchStoreEvenWhenCallingHandlerDirectly()
    {
        var store = new TestProjectStore();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new OpenProjectCommandHandler(store)
            .Handle(new OpenProjectCommand(TestProjectStore.DirectoryPath), cancellation.Token));
        Assert.Equal(0, store.Calls);
    }

    [Fact]
    public async Task StorageFailuresAreNotSwallowedOrReclassified()
    {
        var store = new TestProjectStore { Failure = new IOException("test failure") };
        var create = await Assert.ThrowsAsync<IOException>(() => new CreateProjectCommandHandler(store)
            .Handle(new CreateProjectCommand("FCC", TestProjectStore.DirectoryPath), CancellationToken.None));
        var open = await Assert.ThrowsAsync<IOException>(() => new OpenProjectCommandHandler(store)
            .Handle(new OpenProjectCommand(TestProjectStore.DirectoryPath), CancellationToken.None));
        Assert.Same(store.Failure, create);
        Assert.Same(store.Failure, open);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddSingleton<IProjectStore, TestProjectStore>();
        return services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });
    }
}
