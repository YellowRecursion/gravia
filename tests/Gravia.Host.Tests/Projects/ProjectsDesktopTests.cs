using System.Net;
using System.Text.Json;
using ActualLab.Rpc.Server;
using Gravia.Application.Projects.Services;
using Gravia.Application.Storage;
using Gravia.Host.Desktop;
using Gravia.Infrastructure.Storage;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Host.Tests.Projects;

[Collection("Project lifecycle")]
public sealed class ProjectsDesktopTests
{
    // Explicitly enabled by npm run smoke: this test opens a real Electron window.
    [DesktopSmokeFact]
    public async Task ProjectsScreenCreatesOpensSwitchesAndRestoresAgainstIsolatedHost()
    {
        var root = new DirectoryInfo(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Combine(root.FullName, "Gravia.slnx"))) root = root.Parent;
        Assert.NotNull(root);
        var desktop = Path.Combine(root.FullName, "src", "Gravia.Desktop");
        var directory = Directory.CreateTempSubdirectory("gravia-projects-desktop-").FullName;
        try
        {
            var shell = Path.Combine(directory, "desktop");
            Directory.CreateDirectory(shell);
            Directory.CreateDirectory(Path.Combine(directory, "projects"));
            foreach (var file in new[] { "shell.cjs", "bootstrap.cjs", "preload.cjs", "directory-dialog.cjs", "window-appearance.cjs", "app-window.cjs", "settings-windows.cjs", "package.json" })
                File.Copy(Path.Combine(desktop, file), Path.Combine(shell, file));
            File.Copy(Path.Combine(desktop, "tests", "projects-smoke-main.cjs"), Path.Combine(shell, "main.cjs"));

            var builder = WebApplication.CreateBuilder();
            builder.Logging.ClearProviders();
            builder.WebHost.ConfigureKestrel(server => server.Listen(IPAddress.Loopback, 0));
            builder.Services.AddHostServices();
            builder.Services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application,
                (_, _) => new FileDataStore(Path.Combine(directory, "application")));
            await using (var app = builder.Build())
            {
                using var files = new PhysicalFileProvider(Path.Combine(desktop, "dist"));
                app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
                app.MapGet("/", () => Results.File(Path.Combine(desktop, "dist", "index.html"), "text/html"));
                app.UseWebSockets();
                app.MapRpcWebSocketServer();
                await app.StartAsync();
                try
                {
                    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
                    await using var electron = await ElectronSession.StartAsync(
                        new DesktopOptions { AppDirectory = shell }, new Uri(app.Urls.Single()),
                        NullLogger.Instance, timeout.Token);
                    await electron.Completion.WaitAsync(timeout.Token);
                    var resultFile = Path.Combine(shell, "result.json");
                    Assert.True(File.Exists(resultFile), "Electron did not report its project workflow result.");
                    var resultText = await File.ReadAllTextAsync(resultFile);
                    using var result = JsonDocument.Parse(resultText);
                    Assert.True(result.RootElement.GetProperty("success").GetBoolean(), resultText);
                    Assert.Equal(0, electron.ExitCode);
                    Assert.Equal("Бета", ProjectSession.Current?.Name);
                    Assert.True(Directory.Exists(Path.Combine(directory, "projects", "Альфа")));
                    Assert.True(Directory.Exists(Path.Combine(directory, "projects", "Бета")));
                }
                finally { await app.StopAsync(); }
            }
        }
        finally
        {
            // Only this test's freshly allocated temporary directory is removed.
            Directory.Delete(directory, recursive: true);
        }
    }
}

public sealed class DesktopSmokeFactAttribute : FactAttribute
{
    public DesktopSmokeFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("GRAVIA_PROJECTS_DESKTOP_SMOKE") != "1")
            Skip = "Run npm run smoke in src/Gravia.Desktop to open the integration test window.";
    }
}
