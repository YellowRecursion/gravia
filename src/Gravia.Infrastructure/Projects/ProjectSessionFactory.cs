using FluentValidation;
using FluentValidation.Results;
using Gravia.Application.Plugins.Services;
using Gravia.Application.Plugins.Interfaces;
using Gravia.Application.Projects.Interfaces;
using Gravia.Application.Projects.Services;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Domain.CommonExceptions;
using Gravia.Infrastructure.Storage;
using Microsoft.Extensions.Logging;

namespace Gravia.Infrastructure.Projects;

public sealed class ProjectSessionFactory(ILoggerFactory loggerFactory,
    IEnumerable<IPluginFactory>? pluginFactories = null) : IProjectSessionFactory
{
    public async Task<ProjectSession> CreateAsync(string parentDirectory, string name, CancellationToken cancellationToken = default)
    {
        var parent = NormalizePath(parentDirectory, "parentDirectory");
        ValidateName(name);
        EnsureDirectory(parent);
        var path = Path.Combine(parent, name);
        if (Path.Exists(path))
            throw Invalid("name", "project_directory_exists", "A file or directory with this name already exists.");

        cancellationToken.ThrowIfCancellationRequested();
        Directory.CreateDirectory(path);
        try { return await OpenAsync(path, cancellationToken); }
        catch
        {
            // Remove only the newly created directory, and only if it is still empty.
            try { Directory.Delete(path, recursive: false); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            throw;
        }
    }

    public async Task<ProjectSession> OpenAsync(string projectPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var path = NormalizePath(projectPath, "path");
        EnsureDirectory(path);
        // Verify read access without creating files or assuming a project manifest exists.
        using (var entries = Directory.EnumerateFileSystemEntries(path).GetEnumerator())
            _ = entries.MoveNext();

        var storage = new FileDataStore(path);
        ProjectSettings? settings = null;
        KeyValueStore? keyValues = null;
        PluginManager? pluginManager = null;
        try
        {
            // Apply the store's path guards now, including its junction/symlink policy.
            _ = await storage.ExistsAsync(SettingsStorage.FileName, cancellationToken);
            settings = new ProjectSettings(storage, loggerFactory.CreateLogger<ProjectSettings>());
            keyValues = new KeyValueStore(storage);
            pluginManager = new PluginManager(path, storage, loggerFactory.CreateLogger<PluginManager>(), pluginFactories);
            await pluginManager.InitializeAsync(cancellationToken);
            return new ProjectSession(path, storage, settings, keyValues, pluginManager);
        }
        catch
        {
            if (pluginManager is not null)
            {
                try { await pluginManager.DisposeAsync(); }
                catch (Exception cleanup) { loggerFactory.CreateLogger<ProjectSessionFactory>().LogError(cleanup, "Failed to release plugins after opening a project failed."); }
            }
            settings?.Dispose();
            keyValues?.Dispose();
            storage.Dispose();
            throw;
        }
    }

    private static string NormalizePath(string path, string field)
    {
        if (string.IsNullOrWhiteSpace(path) || !Path.IsPathFullyQualified(path))
            throw Invalid(field, "invalid_project_path", "Choose an absolute directory path.");
        try { return Path.TrimEndingDirectorySeparator(Path.GetFullPath(path)); }
        catch (Exception error) when (error is ArgumentException or NotSupportedException or PathTooLongException)
        {
            throw Invalid(field, "invalid_project_path", "The directory path is invalid.");
        }
    }

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." ||
            name.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || name.Contains('/') || name.Contains('\\') ||
            name.EndsWith('.') || name.EndsWith(' '))
            throw Invalid("name", "invalid_project_name", "The project name must be a single valid directory name.");

        if (OperatingSystem.IsWindows())
        {
            var stem = name.Split('.')[0];
            if (stem.Equals("CON", StringComparison.OrdinalIgnoreCase) || stem.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
                stem.Equals("AUX", StringComparison.OrdinalIgnoreCase) || stem.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
                (stem.Length == 4 && (stem.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
                    stem.StartsWith("LPT", StringComparison.OrdinalIgnoreCase)) && "123456789¹²³".Contains(stem[3])))
                throw Invalid("name", "invalid_project_name", "The project name is reserved by the operating system.");
        }
    }

    private static void EnsureDirectory(string path)
    {
        if (!Directory.Exists(path))
            throw new NotFoundException("The project directory does not exist or is unavailable.", "project_directory_not_found");
    }

    private static ValidationException Invalid(string field, string code, string message)
        => new([new ValidationFailure(field, message) { ErrorCode = code }]);
}
