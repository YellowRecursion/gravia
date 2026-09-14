using System.Text.Json;
using FluentValidation;
using Gravia.Application.Settings.Commands;
using Gravia.Application.Settings.Queries;
using Gravia.Application.Settings.Services;
using Gravia.Application.Storage;
using Gravia.Domain.CommonExceptions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gravia.Application.Tests.Settings;

[Collection("Domain events")]
public sealed class ApplicationSettingsRequestsTests : IAsyncLifetime
{
    private readonly MemoryDataStore _data = new();
    private ServiceProvider _provider = null!;
    private AsyncServiceScope _scope;
    private ApplicationSettings _settings = null!;
    private ISender _sender = null!;

    public async Task InitializeAsync()
    {
        var services = new ServiceCollection();
        services.AddSingleton(typeof(Microsoft.Extensions.Logging.ILogger<>), typeof(NullLogger<>));
        services.AddKeyedSingleton<IDataStore>(DataStoreKey.Application, _data);
        services.AddApplicationServices();
        _provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        _settings = _provider.GetRequiredService<ApplicationSettings>();
        _scope = _provider.CreateAsyncScope();
        _sender = _scope.ServiceProvider.GetRequiredService<ISender>();
        foreach (var service in _provider.GetServices<IHostedService>())
            await service.StartAsync(default);
    }

    public async Task DisposeAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        foreach (var service in _provider.GetServices<IHostedService>().Reverse())
            await service.StopAsync(timeout.Token);
        await _scope.DisposeAsync();
        await _provider.DisposeAsync();
    }

    [Fact]
    public async Task QueriesReturnOrderedCatalogAndSettingSnapshots()
    {
        var catalog = await _sender.Send(new GetApplicationSettingsQuery());
        var theme = await _sender.Send(new GetApplicationSettingQuery("gravia.theme"));

        Assert.Equal(["gravia.language", "gravia.theme"], catalog.Select(setting => setting.Id));
        Assert.Equal("system", theme!.Value.GetString());
        Assert.Equal("system", theme.DefaultValue.GetString());
        Assert.Null(await _sender.Send(new GetApplicationSettingQuery("missing")));
    }

    [Fact]
    public async Task CommandsSetAndResetRegisteredValue()
    {
        await _sender.Send(new SetApplicationSettingValueCommand(
            "gravia.theme", JsonSerializer.SerializeToElement("dark")));

        Assert.Equal("dark", _settings.Theme.Value);

        await _sender.Send(new ResetApplicationSettingValueCommand("gravia.theme"));

        Assert.Equal("system", _settings.Theme.Value);
    }

    [Fact]
    public async Task ExpectedRejectionsKeepTheirSemanticTypeWithoutChangingValue()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() => _sender.Send(
            new SetApplicationSettingValueCommand("gravia.theme", JsonSerializer.SerializeToElement(123))));
        Assert.Equal("invalid_setting_value", Assert.Single(error.Errors).ErrorCode);

        var notFound = await Assert.ThrowsAsync<NotFoundException>(() =>
            _sender.Send(new ResetApplicationSettingValueCommand("missing")));
        Assert.Equal("not_found", notFound.Code);
        Assert.Empty(notFound.Extra);

        notFound = await Assert.ThrowsAsync<NotFoundException>(() => _sender.Send(
            new SetApplicationSettingValueCommand("missing", JsonSerializer.SerializeToElement("dark"))));
        Assert.Equal("not_found", notFound.Code);

        Assert.Equal("system", _settings.Theme.Value);
    }

    [Fact]
    public async Task ValidatorsRejectEmptyIdsWithoutApplicationSpecificCodes()
    {
        var error = await Assert.ThrowsAsync<ValidationException>(() =>
            _sender.Send(new GetApplicationSettingQuery("")));
        Assert.DoesNotContain(error.Errors, failure => failure.ErrorCode == "setting_id_required");

        error = await Assert.ThrowsAsync<ValidationException>(() =>
            _sender.Send(new SetApplicationSettingValueCommand("", JsonSerializer.SerializeToElement("dark"))));
        Assert.DoesNotContain(error.Errors, failure => failure.ErrorCode == "setting_id_required");

        error = await Assert.ThrowsAsync<ValidationException>(() =>
            _sender.Send(new ResetApplicationSettingValueCommand("")));
        Assert.DoesNotContain(error.Errors, failure => failure.ErrorCode == "setting_id_required");
    }

    private sealed class MemoryDataStore : IDataStore
    {
        private string _json = "{}";

        public Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken = default) where T : class
            => Task.FromResult(JsonSerializer.Deserialize<T>(_json));

        public Task SaveAsync<T>(string path, T value, CancellationToken cancellationToken = default) where T : class
        {
            _json = JsonSerializer.Serialize(value);
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(string path, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(string path, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<byte[]?> ReadBytesAsync(string path, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task SaveBytesAsync(string path, byte[] data, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
