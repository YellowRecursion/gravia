using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Settings.Entities;

/// <summary>A setting whose value is the stable ID of one of its declared options.</summary>
/// <remarks>Option IDs are case-sensitive. Options are copied in declaration order and cannot be changed.</remarks>
public sealed class ChoiceSetting : Setting<string>
{
    private readonly HashSet<string> _optionIds = new(StringComparer.Ordinal);

    public IReadOnlyList<SettingOption> Options { get; }

    public ChoiceSetting(string id, LocalizedText title, string defaultValue,
        IEnumerable<SettingOption> options, LocalizedText? description = null)
        : base(id, title, defaultValue, description)
    {
        ArgumentNullException.ThrowIfNull(options);
        var snapshot = options.ToArray();
        if (snapshot.Length == 0)
            throw new ArgumentException("At least one option is required.", nameof(options));

        foreach (var option in snapshot)
        {
            if (option is null)
                throw new ArgumentException("Options must not contain null.", nameof(options));
            if (!_optionIds.Add(option.Id))
                throw new ArgumentException("Option IDs must be unique.", nameof(options));
        }

        Options = Array.AsReadOnly(snapshot);
        ValidateValue(defaultValue);
    }

    protected override void ValidateValue(string value)
    {
        if (value is null || !_optionIds.Contains(value))
            throw new InvalidSettingValueException(Id, "The value must identify one of the available options.");
    }
}
