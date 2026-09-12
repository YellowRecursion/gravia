using System.Net;
using Gravia.Infrastructure.Logging;
using Gravia.Host.Errors;

namespace Gravia.Host.Desktop;

public static class DesktopHost
{
    public static WebApplication Build(string[] args)
    {
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            Args = args, ContentRootPath = AppContext.BaseDirectory
        });
        // No public listener, fixed port, certificate setup or CORS needed for this local shell.
        builder.WebHost.ConfigureKestrel(server =>
        {
            server.Configure(new ConfigurationBuilder().Build());
            server.Listen(IPAddress.Loopback, 0);
        });
        builder.Services.AddHostServices();
        builder.Services.Configure<InMemoryLogOptions>(builder.Configuration.GetSection(InMemoryLogOptions.SectionName));
        var desktop = new DesktopOptions();
        builder.Configuration.GetSection(DesktopOptions.SectionName).Bind(desktop);
        desktop.AppDirectory = Path.GetFullPath(desktop.AppDirectory);
        builder.Services.AddSingleton(desktop);
        var app = builder.Build();
        app.UseMiddleware<HttpExceptionMiddleware>();
        app.Use(async (context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'self'; frame-ancestors 'none'";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            await next(context);
        });
        // Only the two shell assets are served. No project operations or filesystem API yet.
        app.MapGet("/", () => Results.File(Path.Combine(desktop.AppDirectory, "index.html"), "text/html"));
        app.MapGet("/shell.css", () => Results.File(Path.Combine(desktop.AppDirectory, "shell.css"), "text/css"));
        return app;
    }

    public static async Task RunAsync(string[] args, CancellationToken cancellationToken)
    {
        await using var app = Build(args);
        var options = app.Services.GetRequiredService<DesktopOptions>();
        try
        {
            await app.StartAsync(cancellationToken);
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken, app.Lifetime.ApplicationStopping);
            if (!options.Enabled)
            {
                try { await Task.Delay(Timeout.InfiniteTimeSpan, stopping.Token); }
                catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
                return;
            }
            try
            {
                await using var electron = await ElectronSession.StartAsync(options, new Uri(app.Urls.Single()),
                    app.Services.GetRequiredService<ILogger<ElectronSession>>(), stopping.Token);
                await electron.Completion.WaitAsync(stopping.Token);
                if (electron.ExitCode != 0)
                    throw new InvalidOperationException($"Electron exited with code {electron.ExitCode}.");
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested) { }
        }
        finally
        {
            using var shutdown = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            await app.StopAsync(shutdown.Token);
        }
    }
}
