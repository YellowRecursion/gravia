using Gravia.Application.Storage;
using Gravia.Domain.Localization.Consts;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Consts;
using Gravia.Domain.Settings.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Settings;

/// <summary>Application-wide settings, initialized when the host starts.</summary>
public sealed class ApplicationSettings(
    [FromKeyedServices(DataStoreKey.Application)] IDataStore dataStore,
    ILogger<ApplicationSettings> logger) : SettingsStorage(dataStore, logger), IHostedService
{
    public StringSetting Language { get; } = new(
        "gravia.language",
        new LocalizedText(GraviaLanguages.English, "Language").With(GraviaLanguages.Russian, "Язык"),
        GraviaLanguages.Russian,
        SettingsUiPaths.Appearance);

    public StringSetting Theme { get; } = new(
        "gravia.theme",
        new LocalizedText(GraviaLanguages.English, "Theme").With(GraviaLanguages.Russian, "Тема"),
        "system",
        SettingsUiPaths.Appearance);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await Register(Language, cancellationToken);
        await Register(Theme, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
