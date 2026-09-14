using Gravia.Application.Storage;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Settings;

/// <summary>Settings owned by a project session and backed by its data store.</summary>
public sealed class ProjectSettings(IDataStore dataStore, ILogger<ProjectSettings> logger)
    : SettingsStorage(dataStore, logger);
