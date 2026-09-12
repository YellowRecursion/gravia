using System.Net;
using Gravia.Infrastructure.Logging;
using Gravia.Host.Errors;
using Microsoft.Extensions.FileProviders;

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
            // Ant/React Flow use dynamic styles and inline positioning. Scripts stay same-origin only.
            context.Response.Headers.ContentSecurityPolicy =
                "default-src 'none'; script-src 'self'; style-src 'self' 'unsafe-inline'; " +
                "connect-src 'self'; img-src 'self' data:; font-src 'self'; " +
                "base-uri 'none'; frame-ancestors 'none'; form-action 'none'";
            context.Response.Headers.XContentTypeOptions = "nosniff";
            await next(context);
        });
        // Only the compiled renderer is public, never the Electron/npm/source directory.
        var webRoot = Path.Combine(desktop.AppDirectory, "wwwroot");
        if (Directory.Exists(webRoot))
        {
            var files = new PhysicalFileProvider(webRoot);
            app.Lifetime.ApplicationStopped.Register(files.Dispose);
            app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
        }
        app.MapGet("/", () => Results.File(Path.Combine(webRoot, "index.html"), "text/html"));
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
