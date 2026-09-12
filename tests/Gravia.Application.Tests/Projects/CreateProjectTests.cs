using FluentValidation;
using Gravia.Application.Projects;
using Gravia.Domain.Projects.Exceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Application.Tests.Projects;

public sealed class CreateProjectTests
{
    [Fact]
    public void ApplicationRegistrationDiscoversProjectHandlerAndValidator()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        Assert.IsType<CreateProjectCommandHandler>(scope.ServiceProvider
            .GetRequiredService<IRequestHandler<CreateProjectCommand, ProjectDetails>>());
        Assert.IsType<CreateProjectCommandValidator>(Assert.Single(scope.ServiceProvider
            .GetServices<IValidator<CreateProjectCommand>>()));
    }

    [Fact]
    public async Task CreatesProjectThroughTheRealMediatorPipeline()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var first = await sender.Send(new CreateProjectCommand("  Оптимизация FCC  ", TestProjectStore.DirectoryPath));
        var second = await sender.Send(new CreateProjectCommand("Оптимизация FCC", TestProjectStore.DirectoryPath + "-2"));

        Assert.NotEqual(Guid.Empty, first.Id);
        Assert.NotEqual(first.Id, second.Id);
        Assert.Equal("Оптимизация FCC", first.Name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    [InlineData("\u2003")]
    public async Task MissingNameProducesFieldValidationInsteadOfADomainException(string? name)
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => sender.Send(new CreateProjectCommand(name!, TestProjectStore.DirectoryPath)));

        var failure = Assert.Single(exception.Errors);
        Assert.Equal("name", failure.PropertyName);
        Assert.Equal("project_name_required", failure.ErrorCode);
        Assert.Equal("Project name is required.", failure.ErrorMessage);
    }

    [Fact]
    public async Task DisplayNameIsNotValidatedAsAPath()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new CreateProjectCommand("FCC: сырьё / продукт", TestProjectStore.DirectoryPath));

        Assert.Equal("FCC: сырьё / продукт", result.Name);
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotCreateProject()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider
            .GetRequiredService<ISender>().Send(new CreateProjectCommand("FCC", TestProjectStore.DirectoryPath), cancellation.Token));
    }

    [Fact]
    public async Task DirectHandlerCallStillObservesCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new CreateProjectCommandHandler(new TestProjectStore())
            .Handle(new CreateProjectCommand("FCC", TestProjectStore.DirectoryPath), cancellation.Token));
    }

    [Fact]
    public async Task BypassingValidationDoesNotBypassDomainInvariant()
    {
        await Assert.ThrowsAsync<ProjectNameRequiredException>(() => new CreateProjectCommandHandler(new TestProjectStore())
            .Handle(new CreateProjectCommand(" ", TestProjectStore.DirectoryPath), CancellationToken.None));
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();
        services.AddSingleton<IProjectStore, TestProjectStore>();
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }
}
