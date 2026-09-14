using System.Text;
using Gravia.Application.Logging;
using Gravia.Infrastructure.Logging;
using Microsoft.Extensions.Logging;

namespace Gravia.Infrastructure.Tests.Logging;

public sealed class SessionFileLogTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "gravia-log-test-" + Guid.NewGuid());
    private string Current => Path.Combine(_directory, "Gravia.log");
    private string Previous => Path.Combine(_directory, "Gravia-prev.log");
    private static LogMessage Message(string text) => new(LogLevel.Error, "Example.Model", text,
        Exception: "System.NullReferenceException: object is null\n   at Model.Calculate()");

    [Fact]
    public async Task ShutdownDrainsQueueAndOnlyNewRunRotatesPrevious()
    {
        await using (var first = new SessionFileLog(_directory))
        {
            for (var i = 0; i < 100; i++) Assert.True(first.TryWrite(Message($"first-{i}")));
        }
        var previous = await File.ReadAllBytesAsync(Current);
        Assert.Contains("first-99", Encoding.UTF8.GetString(previous));
        Assert.False(File.Exists(Previous));
        await using (var second = new SessionFileLog(_directory))
            Assert.True(second.TryWrite(Message("second")));
        Assert.Equal(previous, await File.ReadAllBytesAsync(Previous));
        Assert.DoesNotContain("first-", await File.ReadAllTextAsync(Current));
        Assert.Contains("second", await File.ReadAllTextAsync(Current));
    }

    [Fact]
    public async Task ConcurrentShutdownDoesNotLoseAnAcceptedFinalRecord()
    {
        for (var i = 0; i < 50; i++)
        {
            var log = new SessionFileLog(_directory);
            using var start = new ManualResetEventSlim();
            var write = Task.Run(() => { start.Wait(); return log.TryWrite(Message("racing-final")); });
            var stop = Task.Run(async () => { start.Wait(); await log.DisposeAsync(); });
            start.Set();
            var accepted = await write;
            await stop;
            Assert.Null(log.Failure);
            if (accepted) Assert.Contains("racing-final", await File.ReadAllTextAsync(Current));
        }
    }

    [Fact]
    public async Task TrimmingKeepsWholeUtf8RecordsAndPreviousSessionUnchanged()
    {
        await using (var first = new SessionFileLog(_directory)) first.TryWrite(Message("previous session"));
        var previous = await File.ReadAllBytesAsync(Current);
        const int capacity = 128 * 1024;
        await using (var log = new SessionFileLog(_directory, capacityBytes: capacity, trimBytes: 16 * 1024,
            queueCapacityBytes: 4 * 1024 * 1024))
        {
            for (var i = 0; i < 500; i++)
                Assert.True(log.TryWrite(Message($"record-{i:D4}: " + new string('я', 900))));
        }
        Assert.Equal(previous, await File.ReadAllBytesAsync(Previous));
        var bytes = await File.ReadAllBytesAsync(Current);
        Assert.InRange(bytes.Length, capacity - 16 * 1024 - 4096, capacity);
        var text = new UTF8Encoding(false, true).GetString(bytes);
        Assert.DoesNotContain("record-0000", text);
        Assert.Contains("record-0499", text);
        Assert.EndsWith("\n", text);
        var lines = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.DoesNotContain(lines, line => !line.StartsWith("  ") && !char.IsAsciiDigit(line[0]));
        Assert.Equal(lines.Count(line => line.Contains("record-")),
            lines.Count(line => line.Contains("at Model.Calculate()")));
    }

    [Fact]
    public async Task RealEightMiBLimitTrimsApproximatelyOneMiB()
    {
        await using (var log = new SessionFileLog(_directory, queueCapacityBytes: 16 * 1024 * 1024))
            for (var i = 0; i < 2400; i++)
                log.TryWrite(Message($"entry-{i:D4}: " + new string('a', 3900)));
        var size = new FileInfo(Current).Length;
        Assert.InRange(size, 7 * 1024 * 1024 - 8192, 8 * 1024 * 1024);
        Assert.Contains("entry-2399", await File.ReadAllTextAsync(Current));
        Assert.False(File.Exists(Previous)); // Size does not rotate sessions.
        Assert.False(File.Exists(Path.Combine(_directory, "Gravia.log.tmp")));
    }

    [Fact]
    public async Task FailedCompactionKeepsTheAlreadyWrittenCurrentFile()
    {
        using var errors = new StringWriter();
        await using var log = new SessionFileLog(_directory, errors, capacityBytes: 64 * 1024, trimBytes: 16 * 1024);
        log.TryWrite(Message("first-committed-record"));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (!File.Exists(Current) || new FileInfo(Current).Length == 0)
            await Task.Delay(5, timeout.Token);
        // Fail the next rewrite, after startup and the first committed record.
        Directory.CreateDirectory(Path.Combine(_directory, "Gravia.log.tmp"));
        for (var i = 0; i < 100; i++) log.TryWrite(Message(new string('a', 2000)));
        await log.DisposeAsync();
        Assert.NotNull(log.Failure);
        Assert.Contains("first-committed-record", await File.ReadAllTextAsync(Current));
        Assert.InRange(new FileInfo(Current).Length, 1, 64 * 1024);
        Assert.False(File.Exists(Previous));
    }

    [Fact]
    public async Task StartupDiscardsOnlyTheInterruptedCompactionCopy()
    {
        Directory.CreateDirectory(_directory);
        var temporary = Path.Combine(_directory, "Gravia.log.tmp");
        await File.WriteAllTextAsync(Current, "last committed session");
        await File.WriteAllTextAsync(temporary, "incomplete copy");
        await using (var log = new SessionFileLog(_directory)) log.TryWrite(Message("new session"));
        Assert.False(File.Exists(temporary));
        Assert.Equal("last committed session", await File.ReadAllTextAsync(Previous));
        Assert.Contains("new session", await File.ReadAllTextAsync(Current));
    }

    [Fact]
    public async Task FileFailureIsObservableAndDoesNotEscapeDispose()
    {
        Directory.CreateDirectory(_directory);
        Directory.CreateDirectory(Current); // File path is deliberately occupied by a directory.
        using var errors = new StringWriter();
        var diagnostics = new List<LogMessage>();
        var log = new SessionFileLog(_directory, errors, reportDiagnostic: diagnostics.Add);
        log.TryWrite(Message("one"));
        await log.DisposeAsync();
        Assert.NotNull(log.Failure);
        Assert.Contains("Gravia file logging failed", errors.ToString());
        Assert.Equal(LogLevel.Error, Assert.Single(diagnostics).Level);
        Assert.False(log.TryWrite(Message("after shutdown")));
    }

    [Fact]
    public async Task ClearDoesNotDiscardPendingFileRecords()
    {
        var journal = new InMemoryLogJournal(
            Microsoft.Extensions.Options.Options.Create(new InMemoryLogOptions()), TimeProvider.System);
        await using (var log = new SessionFileLog(_directory))
        using (var provider = new JournalLoggerProvider(journal, log))
        {
            provider.CreateLogger("Test").LogError("before-clear");
            journal.Clear();
            provider.CreateLogger("Test").LogError("after-clear");
            Assert.Equal("after-clear", Assert.Single(journal.Read().Entries).Message);
        }
        var text = await File.ReadAllTextAsync(Current);
        Assert.Contains("before-clear", text);
        Assert.Contains("after-clear", text);
    }

    [Fact]
    public async Task AnotherProcessOwnerCannotRotateAnActiveSession()
    {
        Directory.CreateDirectory(_directory);
        using var ownership = new FileStream(Path.Combine(_directory, "Gravia.log.lock"),
            FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        await File.WriteAllTextAsync(Current, "existing");
        using var errors = new StringWriter();
        var log = new SessionFileLog(_directory, errors);
        await log.DisposeAsync();
        Assert.NotNull(log.Failure);
        Assert.Equal("existing", await File.ReadAllTextAsync(Current));
        Assert.False(File.Exists(Previous));
    }

    public void Dispose()
    {
        // Only this test's uniquely-created directory, never a configured application path.
        Assert.Equal(Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar),
            Path.GetDirectoryName(Path.GetFullPath(_directory)));
        Assert.StartsWith("gravia-log-test-", Path.GetFileName(_directory));
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }
}
