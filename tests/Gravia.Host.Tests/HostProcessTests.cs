using Gravia.Application.Logging;
using Gravia.Domain.Common;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Tests;

public sealed class HostProcessTests
{
    [Fact]
    public async Task SuccessfulLifetimeReturnsZero()
    {
        using var services = new ServiceCollection().AddHostServices().BuildServiceProvider();
        using var output = new StringWriter();
        var called = false;
        var exitCode = await HostProcess.RunAsync(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, CreateLogger(services), output);

        Assert.True(called);
        Assert.Equal(0, exitCode);
        Assert.Equal("", output.ToString());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SynchronousAndAwaitedFailuresReturnNonzeroAndLogOnce(bool asynchronous)
    {
        using var services = new ServiceCollection().AddHostServices().BuildServiceProvider();
        using var output = new StringWriter();
        var error = new InvalidOperationException("Private technical detail");
        var exitCode = await HostProcess.RunAsync(_ => asynchronous ? Task.FromException(error) : throw error,
            CreateLogger(services), output);

        Assert.Equal(1, exitCode);
        Assert.Contains("internal_error", output.ToString());
        Assert.DoesNotContain("Private", output.ToString());
        var entry = Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
        Assert.Contains(entry.Properties["TraceId"]!, output.ToString());
    }

    [Fact]
    public async Task ExpectedRejectionStillMeansHostDidNotStartSuccessfully()
    {
        using var services = new ServiceCollection().AddHostServices().BuildServiceProvider();
        using var output = new StringWriter();
        var exitCode = await HostProcess.RunAsync(_ => throw new RuleException(),
            CreateLogger(services), output);

        Assert.Equal(1, exitCode);
        Assert.Contains("test_rule_rejected", output.ToString());
        Assert.Empty(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public async Task PreCancelledLifetimeDoesNotRunOrLog()
    {
        using var services = new ServiceCollection().AddHostServices().BuildServiceProvider();
        using var output = new StringWriter();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var called = false;
        var exitCode = await HostProcess.RunAsync(_ =>
        {
            called = true;
            return Task.CompletedTask;
        }, CreateLogger(services), output, cancellation.Token);

        Assert.Equal(0, exitCode);
        Assert.False(called);
        Assert.Equal("", output.ToString());
        Assert.Empty(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public async Task UnrequestedCancellationDoesNotBecomeSuccessfulExit()
    {
        using var services = new ServiceCollection().AddHostServices().BuildServiceProvider();
        using var output = new StringWriter();
        var exitCode = await HostProcess.RunAsync(_ => throw new OperationCanceledException(),
            CreateLogger(services), output);
        Assert.Equal(1, exitCode);
        Assert.Contains("internal_error", output.ToString());
    }

    [Fact]
    public async Task DisposalFailureIsInsideTheBoundary()
    {
        using var services = new ServiceCollection().AddHostServices().BuildServiceProvider();
        using var output = new StringWriter();
        var exitCode = await HostProcess.RunAsync(_ =>
        {
            using var resource = new BrokenResource();
            return Task.CompletedTask;
        }, CreateLogger(services), output);
        Assert.Equal(1, exitCode);
        Assert.Single(services.GetRequiredService<ILogJournal>().Read().Entries);
    }

    [Fact]
    public async Task BrokenOutputStillReturnsFailure()
    {
        using var services = new ServiceCollection().AddHostServices().BuildServiceProvider();
        using var output = new BrokenWriter();
        var exitCode = await HostProcess.RunAsync(_ => throw new Exception("Failure"),
            CreateLogger(services), output);
        Assert.Equal(1, exitCode);
    }

    private sealed class RuleException() : DomainException("test_rule_rejected", "Safe explanation.");

    private static ILogger CreateLogger(IServiceProvider services)
        => services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(HostProcess));

    private sealed class BrokenResource : IDisposable
    {
        public void Dispose() => throw new InvalidOperationException("Disposal failed");
    }

    private sealed class BrokenWriter : StringWriter
    {
        public override Task WriteLineAsync(string? value) => throw new IOException("Output failed");
    }
}
