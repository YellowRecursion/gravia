using System.Diagnostics;
using System.Net;
using ActualLab.Rpc.Server;
using Gravia.Application.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Gravia.Host.Tests.Logging;

public sealed class LogJournalWireTests
{
    [Fact]
    public async Task TypeScriptClientReadsSubscribesAndClearsOverRealFusionWebSocket()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gravia.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
        builder.Services.AddHostServices();
        await using var app = builder.Build();
        // Node's native WebSocket does not send Origin. This isolated test host supplies it;
        // production keeps the original same-origin policy and middleware unchanged.
        app.Use((context, next) =>
        {
            context.Request.Headers.Origin = $"http://{context.Request.Host}";
            return next(context);
        });
        app.UseWebSockets();
        app.MapRpcWebSocketServer();
        await app.StartAsync();
        var journal = app.Services.GetRequiredService<ILogJournal>();
        for (var i = 0; i < 700; i++)
            journal.TryWrite(new(LogLevel.Error, "Model", "Ошибка 🌡", new EventId(42, "ModelError"),
                "System.NullReferenceException: inner", new Dictionary<string, string?> { ["unit"] = "АВТ" }));

        var start = new ProcessStartInfo("node")
        {
            WorkingDirectory = Path.Combine(root.FullName, "src", "Gravia.Desktop"),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        start.ArgumentList.Add("tests/log-journal-rpc.mjs");
        start.ArgumentList.Add(app.Urls.Single().Replace("http://", "ws://") + "/rpc/ws");
        using var process = Process.Start(start)!;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var error = process.StandardError.ReadToEndAsync(timeout.Token);
        var output = new List<string>();
        try
        {
            while (await process.StandardOutput.ReadLineAsync(timeout.Token) is { } line)
            {
                output.Add(line);
                if (line == "READY")
                    journal.TryWrite(new(LogLevel.Warning, "Model", "live-from-host"));
            }
            await process.WaitForExitAsync(timeout.Token);
            Assert.True(process.ExitCode == 0, string.Join('\n', output) + "\n" + await error);
            Assert.Contains("PASS", output);
            Assert.Equal(0, journal.GetState().EntryCount);
        }
        finally
        {
            if (!process.HasExited) process.Kill(entireProcessTree: true);
            await app.StopAsync();
        }
    }
}
