using FluentValidation;
using Gravia.Application.Common.Behaviors;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Gravia.Application.Tests;

public sealed class DependencyInjectionTests
{
    [Fact]
    public void RegistersMediatorAndValidationPipeline()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISender>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPublisher>());
        var behavior = Assert.Single(scope.ServiceProvider
            .GetServices<IPipelineBehavior<TestCommand, string>>());
        Assert.IsType<ValidationBehavior<TestCommand, string>>(behavior);
    }

    [Fact]
    public async Task ValidRequestRunsAsyncValidationBeforeHandler()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var state = scope.ServiceProvider.GetRequiredService<TestState>();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new TestCommand("test", 1), cancellation.Token);

        Assert.Equal("test", result);
        Assert.Equal(new[] { "validate-name", "validate-count", "handle" }, state.Steps);
        Assert.Equal(cancellation.Token, state.ValidationToken);
        Assert.Equal(cancellation.Token, state.HandlerToken);
    }

    [Fact]
    public async Task InvalidRequestCollectsFailuresWithoutCallingHandler()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();
        var state = scope.ServiceProvider.GetRequiredService<TestState>();

        var exception = await Assert.ThrowsAsync<ValidationException>(
            () => sender.Send(new TestCommand("", 0)));

        Assert.Equal(new[] { "Name", "Count" }, exception.Errors.Select(error => error.PropertyName));
        Assert.Equal(new[] { "validate-name", "validate-count" }, state.Steps);
    }

    [Fact]
    public async Task RequestWithoutValidatorsReachesHandler()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();

        var result = await scope.ServiceProvider.GetRequiredService<ISender>()
            .Send(new UnvalidatedCommand());

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task AlreadyCancelledRequestDoesNotRunValidationOrHandler()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var sender = scope.ServiceProvider.GetRequiredService<ISender>();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => sender.Send(new TestCommand("test", 1), cancellation.Token));

        Assert.Empty(scope.ServiceProvider.GetRequiredService<TestState>().Steps);
    }

    [Fact]
    public async Task CancellationDuringValidationDoesNotReachHandler()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        state.OnValidation = cancellation.Cancel;

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => scope.ServiceProvider
            .GetRequiredService<ISender>().Send(new TestCommand("test", 1), cancellation.Token));

        Assert.DoesNotContain("handle", state.Steps);
        Assert.Equal(cancellation.Token, state.ValidationToken);
    }

    [Fact]
    public async Task HandlerExceptionIsNotSwallowed()
    {
        using var provider = CreateProvider();
        using var scope = provider.CreateScope();
        var state = scope.ServiceProvider.GetRequiredService<TestState>();
        state.HandlerException = new InvalidOperationException("test failure");

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider
            .GetRequiredService<ISender>().Send(new TestCommand("test", 1)));

        Assert.Same(state.HandlerException, exception);
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddApplicationServices();

        // Test-only requests keep scaffolding out of the production Application assembly.
        services.AddScoped<TestState>();
        services.AddTransient<IRequestHandler<TestCommand, string>, TestCommandHandler>();
        services.AddTransient<IRequestHandler<UnvalidatedCommand, int>, UnvalidatedCommandHandler>();
        services.AddScoped<IValidator<TestCommand>, NameValidator>();
        services.AddScoped<IValidator<TestCommand>, CountValidator>();

        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
    }

    public sealed record TestCommand(string Name, int Count) : IRequest<string>;
    public sealed record UnvalidatedCommand : IRequest<int>;

    public sealed class TestState
    {
        public List<string> Steps { get; } = [];
        public CancellationToken ValidationToken { get; set; }
        public CancellationToken HandlerToken { get; set; }
        public Action? OnValidation { get; set; }
        public Exception? HandlerException { get; set; }
    }

    public sealed class NameValidator : AbstractValidator<TestCommand>
    {
        public NameValidator(TestState state)
        {
            RuleFor(command => command.Name).MustAsync(async (name, cancellationToken) =>
            {
                await Task.Yield();
                state.Steps.Add("validate-name");
                state.ValidationToken = cancellationToken;
                state.OnValidation?.Invoke();
                cancellationToken.ThrowIfCancellationRequested();
                return !string.IsNullOrWhiteSpace(name);
            });
        }
    }

    public sealed class CountValidator : AbstractValidator<TestCommand>
    {
        public CountValidator(TestState state)
        {
            RuleFor(command => command.Count).Must(count =>
            {
                state.Steps.Add("validate-count");
                return count > 0;
            });
        }
    }

    public sealed class TestCommandHandler(TestState state) : IRequestHandler<TestCommand, string>
    {
        public Task<string> Handle(TestCommand request, CancellationToken cancellationToken)
        {
            state.Steps.Add("handle");
            state.HandlerToken = cancellationToken;
            if (state.HandlerException is not null)
            {
                throw state.HandlerException;
            }

            return Task.FromResult(request.Name);
        }
    }

    public sealed class UnvalidatedCommandHandler : IRequestHandler<UnvalidatedCommand, int>
    {
        public Task<int> Handle(UnvalidatedCommand request, CancellationToken cancellationToken)
            => Task.FromResult(42);
    }
}
