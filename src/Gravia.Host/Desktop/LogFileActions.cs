using System.Diagnostics;
using Gravia.Infrastructure;

namespace Gravia.Host.Desktop;

/// <summary>Only the two known log locations can be opened; the renderer never supplies a path.</summary>
public class LogFileActions
{
    public virtual void Open(bool currentFile)
    {
        var directory = ApplicationPaths.StorageDirectory;
        var target = currentFile ? Path.Combine(directory, "Gravia.log") : directory;
        if (currentFile ? !File.Exists(target) : !Directory.Exists(target))
            throw new FileNotFoundException("The log location is not available.");
        using var process = Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
    }
}
