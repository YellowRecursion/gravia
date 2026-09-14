using System.Diagnostics;
using System.Text.Json;
using ActualLab.Rpc;
using ActualLab.Rpc.Infrastructure;
using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Logging;
using Gravia.Domain.Common;
using Gravia.Host.Fusion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Tests.Fusion;

public sealed class RpcExceptionMiddlewareTests
{
    [Fact]
    public async Task DomainExceptionBecomesDeclaredResultWithStructuredData()
    {
        using var services = CreateServices();
        using var activity = new Activity("RPC test").SetIdFormat(ActivityIdFormat.W3C).Start();
        var middleware = GetMiddleware(services);
        var invoke = middleware.Create<RpcResult<int>>(null!, _ =>
            Task.FromException<RpcResult<int>>(new LimitExceededException(42)));

        var result = await invoke(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal("limit_exceeded", result.ErrorCode);
        Assert.Equal("The limit was exceeded.", result.ErrorMessage);
        Assert.Equal(42, result.ErrorData!["limit"]);
        Assert.Equal(activity.TraceId.ToString(), result.TraceId);
        Assert.Equal(42, JsonDocument.Parse(JsonSerializer.Serialize(result.ErrorData))
            .RootElement.GetProperty("limit").GetInt32());
        Assert.Empty(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public async Task ValidationExceptionBecomesFieldErrors()
    {
        using var services = CreateServices();
        var middleware = GetMiddleware(services);
        var exception = new ValidationException([
            new ValidationFailure("name", "Name is required.")
            {
                ErrorCode = "name_required",
                AttemptedValue = "Private input",
                CustomState = "Private state"
            }
        ]);
        var invoke = middleware.Create<RpcResult>(null!, _ => Task.FromException<RpcResult>(exception));

        var result = await invoke(null!);

        Assert.Equal("validation_failed", result.ErrorCode);
        var error = Assert.Single(Assert.IsType<RpcFieldError[]>(result.ErrorData!["errors"]));
        Assert.Equal(new RpcFieldError("name", "name_required", "Name is required."), error);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(result));
        Assert.Empty(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public async Task StandardExceptionBecomesInternalErrorAndIsLoggedOnce()
    {
        using var services = CreateServices();
        var middleware = GetMiddleware(services);
        var invoke = middleware.Create<RpcResult>(null!, _ =>
            Task.FromException<RpcResult>(new InvalidOperationException("Private detail")));

        var result = await invoke(null!);

        Assert.Equal("internal_error", result.ErrorCode);
        Assert.Equal("An internal error occurred.", result.ErrorMessage);
        Assert.Null(result.ErrorData);
        Assert.DoesNotContain("Private detail", result.ErrorMessage);
        var entry = Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
        Assert.Equal(result.TraceId, entry.Properties["TraceId"]);
    }

    [Fact]
    public void NonResultMethodIsNotWrapped()
    {
        using var services = CreateServices();
        var middleware = GetMiddleware(services);
        Func<RpcInboundCall, Task<int>> next = _ => Task.FromResult(7);

        Assert.Same(next, middleware.Create<int>(null!, next));
    }

    [Fact]
    public async Task BrokenLoggerDoesNotReplaceInternalError()
    {
        var middleware = new RpcExceptionMiddleware(new BrokenLogger());
        var invoke = middleware.Create<RpcResult>(null!, _ =>
            Task.FromException<RpcResult>(new InvalidOperationException("Private detail")));

        var result = await invoke(null!);

        Assert.Equal("internal_error", result.ErrorCode);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(result));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddHostServices();
        return services.BuildServiceProvider();
    }

    private static RpcExceptionMiddleware GetMiddleware(IServiceProvider services)
        => services.GetServices<IRpcMiddleware>().OfType<RpcExceptionMiddleware>().Single();

    private sealed class LimitExceededException : DomainException
    {
        public LimitExceededException(int limit) : base("limit_exceeded", "The limit was exceeded.")
            => Add("limit", limit);
    }

    private sealed class BrokenLogger : ILogger<RpcExceptionMiddleware>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("Logging failed");
    }
}
