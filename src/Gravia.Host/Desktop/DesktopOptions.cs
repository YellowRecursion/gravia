namespace Gravia.Host.Desktop;

public sealed class DesktopOptions
{
    public const string SectionName = "Desktop";
    public bool Enabled { get; set; } = true;
    public string AppDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Desktop");
    public string? ExecutablePath { get; set; }

    public string ResolveExecutable()
    {
        if (!string.IsNullOrWhiteSpace(ExecutablePath))
        {
            var configured = Path.GetFullPath(ExecutablePath);
            if (!File.Exists(configured)) throw new FileNotFoundException("Electron executable was not found.", configured);
            return configured;
        }

        var relative = OperatingSystem.IsWindows() ? "electron.exe"
            : OperatingSystem.IsMacOS() ? "Electron.app/Contents/MacOS/Electron" : "electron";
        var local = Path.Combine(AppDirectory, "node_modules", "electron", "dist", relative);
        if (File.Exists(local)) return Path.GetFullPath(local);

        // Development fallback, independent of Rider's working directory.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (!File.Exists(Path.Combine(directory.FullName, "Gravia.slnx"))) continue;
            var installed = Path.Combine(directory.FullName, "src", "Gravia.Desktop", "node_modules", "electron", "dist", relative);
            if (File.Exists(installed)) return installed;
            break;
        }
        throw new FileNotFoundException("Electron is not installed. Run npm ci in src/Gravia.Desktop, " +
            "or configure Desktop:ExecutablePath. Packaged distribution is not configured yet.");
    }
}
