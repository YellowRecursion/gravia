using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Gravia.Application.Settings.Events;
using Gravia.Application.Storage;
using Gravia.Domain.Common;
using Gravia.Domain.Common.Events;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Interfaces;
using Microsoft.Extensions.Logging;

namespace Gravia.Application.Settings.Services;

/// <summary>Registers settings and persists their values using an existing data store.</summary>
/// <remarks>Use one instance per store. Dispose after pending events finish. Does not own the data store.</remarks>
public abstract class SettingsStorage(IDataStore dataStore, ILogger logger) : IDisposable, IAsyncDisposable
{
    public const string FileName = "settings.json";

    // Events carry setting objects, so ownership does not depend on potentially shared IDs.
    private static readonly ConditionalWeakTable<ISetting, SettingsStorage> Owners = new();
    private readonly ConcurrentDictionary<string, ISetting> _settings = new(StringComparer.Ordinal);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private bool _disposed;

    public IReadOnlyCollection<ISetting> Settings => _settings.Values.ToArray();

    /// <summary>Registers or replaces a definition, restoring its saved or currently registered value.</summary>
    public async Task Register<T>(Setting<T> setting, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var owner = Owners.GetValue(setting, _ => this);
            if (!ReferenceEquals(owner, this))
                throw new InvalidOperationException("The setting is already registered in another storage.");

            _settings.TryGetValue(setting.Id, out var previous);
            try
            {
                var values = await ReadValuesAsync(cancellationToken);
                var hasValue = values.TryGetValue(setting.Id, out var value);
                if (previous is not null)
                {
                    value = previous.SerializeValue();
                    hasValue = true;
                }

                var reset = false;
                if (hasValue)
                {
                    try { setting.DeserializeValue(value); }
                    catch (Exception exception) when (exception is JsonException or DomainException)
                    {
                        setting.SetValueWithoutNotify(setting.DefaultValue);
                        reset = true;
                        logger.LogWarning("Setting {SettingId} was reset because its previous value is incompatible.", setting.Id);
                    }
                }

                // Preserve pending changes when replacing a definition; repair incompatible saved values.
                if (previous is not null || reset)
                {
                    values[setting.Id] = setting.SerializeValue();
                    await dataStore.SaveAsync(FileName, values, cancellationToken);
                }

                _settings[setting.Id] = setting;
                if (previous is not null && !ReferenceEquals(previous, setting))
                    Owners.Remove(previous);
                new SettingsRegistrationChanged(this, setting.Id).Raise();
            }
            catch
            {
                if (!ReferenceEquals(previous, setting))
                    Owners.Remove(setting);
                throw;
            }
        }
        finally { _gate.Release(); }
    }

    /// <summary>Removes a definition while preserving its current value in storage.</summary>
    public async Task Unregister(string id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (!_settings.TryGetValue(id, out var setting)) return;

            var values = await ReadValuesAsync(cancellationToken);
            values[id] = setting.SerializeValue();
            await dataStore.SaveAsync(FileName, values, cancellationToken);
            _settings.TryRemove(id, out _);
            Owners.Remove(setting);
            new SettingsRegistrationChanged(this, id).Raise();
        }
        finally { _gate.Release(); }
    }

    public ISetting? Find(string id) => _settings.GetValueOrDefault(id);
    
    public bool Contains(ISetting setting) => ReferenceEquals(Find(setting.Id), setting);
    
    internal static SettingsStorage? FindOwner(ISetting setting)
        => Owners.TryGetValue(setting, out var owner) ? owner : null;

    internal async Task SaveAsync(ISetting setting, CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_disposed) return;
            if (!_settings.TryGetValue(setting.Id, out var registered) || !ReferenceEquals(registered, setting))
                return;

            var values = await ReadValuesAsync(cancellationToken);
            values[setting.Id] = setting.SerializeValue();
            await dataStore.SaveAsync(FileName, values, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<Dictionary<string, JsonElement>> ReadValuesAsync(CancellationToken cancellationToken)
        => await dataStore.ReadAsync<Dictionary<string, JsonElement>>(FileName, cancellationToken)
           ?? new(StringComparer.Ordinal);

    /// <summary>Persists current values even when their change events are still queued.</summary>
    internal async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_settings.IsEmpty) return;
            var values = await ReadValuesAsync(cancellationToken);
            foreach (var setting in _settings.Values)
                values[setting.Id] = setting.SerializeValue();
            await dataStore.SaveAsync(FileName, values, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public void Dispose()
    {
        _gate.Wait();
        try { Clear(); }
        finally { _gate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        await _gate.WaitAsync();
        try { Clear(); }
        finally { _gate.Release(); }
    }

    private void Clear()
    {
        _disposed = true;
        foreach (var setting in _settings.Values)
            Owners.Remove(setting);
        _settings.Clear();
        // Queued event handlers may already be waiting on this gate. Let them exit normally.
        // No semaphore wait handle is allocated; the gate is collected with the storage.
    }
}
