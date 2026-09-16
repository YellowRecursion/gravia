namespace Gravia.Host.Desktop;

public sealed class DesktopOptions
{
    public const string SectionName = "Desktop";
    public bool Enabled { get; set; } = true;
    public string AppDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "Desktop");
    public string? ExecutablePath { get; set; }
    public bool HotReload { get; set; }
    public string? SourceDirectory { get; set; }

    public string ResolveSourceDirectory()
    {
        if (!string.IsNullOrWhiteSpace(SourceDirectory))
            return ValidateSourceDirectory(Path.GetFullPath(SourceDirectory));
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Gravia.slnx")))
                return ValidateSourceDirectory(Path.Combine(directory.FullName, "src", "Gravia.Desktop"));
        }
        throw new DirectoryNotFoundException("Gravia Dev requires the source checkout or Desktop:SourceDirectory.");
    }

    private static string ValidateSourceDirectory(string directory)
    {
        if (!File.Exists(Path.Combine(directory, "vite.config.ts")) ||
            !File.Exists(Path.Combine(directory, "renderer", "index.html")))
            throw new DirectoryNotFoundException($"Desktop sources were not found in {directory}.");
        return directory;
    }

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
