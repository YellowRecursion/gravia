using Gravia.Domain.Common;

namespace Gravia.Domain.Settings.Exceptions;

/// <summary>A value does not satisfy the setting's declared constraints.</summary>
public sealed class InvalidSettingValueException : DomainException
{
    public InvalidSettingValueException(string settingId, string message)
        : base("setting_value_invalid", message)
    {
        Add("settingId", settingId);
    }
}
