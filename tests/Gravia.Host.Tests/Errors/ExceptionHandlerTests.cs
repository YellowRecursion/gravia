using System.Diagnostics;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Logging;
using Gravia.Domain.Common;
using Gravia.Host.Errors;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Tests.Errors;

public sealed class ExceptionHandlerTests
{
    [Fact]
    public void DomainRejectionPreservesClientDataWithoutLogging()
    {
        using var services = CreateServices();
        var id = Guid.NewGuid();
        var error = services.GetRequiredService<ExceptionHandler>().Handle(new RuleException(id));

        Assert.Equal(ErrorKind.Domain, error.Kind);
        Assert.Equal("test_rule_rejected", error.Code);
        Assert.Equal("Safe rule explanation.", error.Message);
        Assert.Equal(id, error.Extra["nodeId"]);
        Assert.Empty(error.Errors);
        Assert.Empty(services.GetRequiredService<ILogJournal>().Read().Entries);
        Assert.DoesNotContain("Private cause", JsonSerializer.Serialize(error));
    }

    [Fact]
    public void ValidationPreservesAllFieldErrorsButNotAttemptedValuesOrCustomState()
    {
        using var services = CreateServices();
        var failures = new[]
        {
            new ValidationFailure("Name", "Name is required.")
            {
                ErrorCode = "name_required", AttemptedValue = "Private input", CustomState = "Private state"
            },
            new ValidationFailure("Name", "Name is invalid.") { ErrorCode = "name_invalid" },
            new ValidationFailure("Count", "Count is invalid.")
        };
        var error = services.GetRequiredService<ExceptionHandler>().Handle(new ValidationException(failures));

        Assert.Equal(ErrorKind.Validation, error.Kind);
        Assert.Equal("validation_failed", error.Code);
        Assert.Equal(3, error.Errors.Count);
        Assert.Equal(new FieldError("Name", "name_required", "Name is required."), error.Errors[0]);
        Assert.Equal("invalid_value", error.Errors[2].Code);
        Assert.Empty(error.Extra);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(error));
        Assert.Empty(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public void RequestedCancellationIsNotLoggedAsFailure()
    {
        using var services = CreateServices();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var error = services.GetRequiredService<ExceptionHandler>()
            .Handle(new OperationCanceledException(cancellation.Token), cancellation.Token);

        Assert.Equal(ErrorKind.Cancelled, error.Kind);
        Assert.Equal("operation_cancelled", error.Code);
        Assert.Empty(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public void UnrequestedCancellationIsAnAnomaly()
    {
        using var services = CreateServices();
        var error = services.GetRequiredService<ExceptionHandler>().Handle(new OperationCanceledException());

        Assert.Equal(ErrorKind.Internal, error.Kind);
        Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public void AnomalyProducesSafeErrorAndOneCorrelatedJournalEntry()
    {
        using var services = CreateServices();
        using var activity = new Activity("test operation").SetIdFormat(ActivityIdFormat.W3C).Start();
        var error = services.GetRequiredService<ExceptionHandler>()
            .Handle(new NullReferenceException("Private technical detail"));

        Assert.Equal(ErrorKind.Internal, error.Kind);
        Assert.Equal("internal_error", error.Code);
        Assert.Equal(activity.TraceId.ToString(), error.TraceId);
        Assert.Empty(error.Extra);
        Assert.Empty(error.Errors);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(error));
        var entry = Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
        Assert.Equal(LogLevel.Error, entry.Level);
        Assert.Equal(error.TraceId, entry.Properties["TraceId"]);
        Assert.Contains("Private technical detail", entry.Exception);
    }

    [Fact]
    public void WrapperIsNotSilentlyConvertedIntoAnExpectedDomainRejection()
    {
        using var services = CreateServices();
        var error = services.GetRequiredService<ExceptionHandler>()
            .Handle(new InvalidOperationException("Wrapper", new RuleException(Guid.NewGuid())));

        Assert.Equal(ErrorKind.Internal, error.Kind);
        Assert.Equal("internal_error", error.Code);
    }

    [Fact]
    public void BrokenLoggerDoesNotReplaceFailureOrLeakDetails()
    {
        var handler = new ExceptionHandler(new BrokenLogger());
        var error = handler.Handle(new Exception("Private technical detail"));
        Assert.Equal(ErrorKind.Internal, error.Kind);
        Assert.DoesNotContain("Private", JsonSerializer.Serialize(error));
        Assert.False(string.IsNullOrWhiteSpace(error.TraceId));
    }

    [Fact]
    public void RegistrationIsIdempotent()
    {
        var services = new ServiceCollection();
        services.AddHostServices();
        services.AddHostServices();
        using var provider = services.BuildServiceProvider();
        Assert.Single(provider.GetServices<ExceptionHandler>());
    }

    private static ServiceProvider CreateServices() => new ServiceCollection()
        .AddHostServices().BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true, ValidateScopes = true
        });

    private sealed class RuleException : DomainException
    {
        public RuleException(Guid id) : base("test_rule_rejected", "Safe rule explanation.",
            new Exception("Private cause")) => Add("nodeId", id);
    }

    private sealed class BrokenLogger : ILogger<ExceptionHandler>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter) => throw new InvalidOperationException("Logging failed");
    }
}
