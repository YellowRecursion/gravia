using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;

namespace Gravia.Host.Desktop;

/// <summary>Owns one Electron process and its private lifetime pipe.</summary>
public sealed class ElectronSession : IAsyncDisposable
{
    private readonly Process _process;
    private readonly Task _stdout;
    private readonly Task _stderr;
    private readonly NamedPipeServerStream _control;
    public Task Completion { get; }
    public int ExitCode => _process.ExitCode;

    private ElectronSession(Process process, NamedPipeServerStream control, ILogger logger)
    {
        _process = process;
        _control = control;
        Completion = process.WaitForExitAsync();
        _stdout = ReadDiagnosticsAsync(process.StandardOutput, logger);
        _stderr = ReadDiagnosticsAsync(process.StandardError, logger);
    }

    public static async Task<ElectronSession> StartAsync(DesktopOptions options, Uri backendUrl,
        ILogger logger, CancellationToken cancellationToken)
    {
        var appDirectory = Path.GetFullPath(options.AppDirectory);
        if (!File.Exists(Path.Combine(appDirectory, "package.json")))
            throw new FileNotFoundException("Desktop package.json was not found.", appDirectory);

        var start = new ProcessStartInfo(options.ResolveExecutable())
        {
            UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = appDirectory,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        start.ArgumentList.Add(appDirectory);
        start.Environment.Remove("ELECTRON_RUN_AS_NODE");
        // Otherwise Electron can replace redirected handles while attaching a Windows console.
        start.Environment["ELECTRON_NO_ATTACH_CONSOLE"] = "1";
        var name = "gravia-" + Guid.NewGuid().ToString("N");
        var pipeName = OperatingSystem.IsWindows() ? name : Path.Combine(Path.GetTempPath(), name);
        var control = new NamedPipeServerStream(pipeName, PipeDirection.InOut, 1,
            PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        start.Environment["GRAVIA_CONTROL_PIPE"] = OperatingSystem.IsWindows() ? @"\\.\pipe\" + name : pipeName;
        Process process;
        try { process = Process.Start(start) ?? throw new InvalidOperationException("Electron did not start."); }
        catch { control.Dispose(); throw; }
        var session = new ElectronSession(process, control, logger);
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(TimeSpan.FromSeconds(options.HotReload ? 90 : 20));
        var handshake = session.HandshakeAsync(backendUrl, options, startup.Token);
        try
        {
            if (await Task.WhenAny(handshake, session.Completion) != handshake)
                throw new InvalidOperationException("Electron exited before its window was ready.");
            await handshake;
            logger.LogInformation("Electron is ready. ProcessId: {ProcessId}", process.Id);
            return session;
        }
        catch
        {
            await startup.CancelAsync();
            try { await handshake; } catch (Exception) { } // Observe the cancelled handshake; preserve the original error.
            await session.DisposeAsync();
            throw;
        }
    }

    private async Task HandshakeAsync(Uri backendUrl, DesktopOptions options, CancellationToken cancellationToken)
    {
        await _control.WaitForConnectionAsync(cancellationToken);
        using var writer = new StreamWriter(_control, new UTF8Encoding(false), leaveOpen: true);
        using var reader = new StreamReader(_control, Encoding.UTF8, leaveOpen: true);
        var bootstrap = new Dictionary<string, string> { ["backendUrl"] = backendUrl.AbsoluteUri };
        if (options.HotReload) bootstrap["sourceDirectory"] = options.ResolveSourceDirectory();
        await writer.WriteLineAsync(JsonSerializer.Serialize(bootstrap).AsMemory(), cancellationToken);
        await writer.FlushAsync(cancellationToken);
        if (await reader.ReadLineAsync(cancellationToken) != "{\"event\":\"ready\"}")
            throw new InvalidOperationException("Electron sent an invalid readiness message.");
    }

    private static async Task ReadDiagnosticsAsync(StreamReader reader, ILogger logger)
    {
        while (await reader.ReadLineAsync() is { } line)
            logger.LogDebug("Electron: {Diagnostic}", line);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            _control.Dispose();
            try { await Completion.WaitAsync(TimeSpan.FromSeconds(5)); }
            catch (TimeoutException)
            {
                if (!_process.HasExited) _process.Kill(entireProcessTree: true);
                await Completion.WaitAsync(TimeSpan.FromSeconds(5));
            }
            await Task.WhenAll(_stdout, _stderr);
        }
        finally { _process.Dispose(); }
    }
}
