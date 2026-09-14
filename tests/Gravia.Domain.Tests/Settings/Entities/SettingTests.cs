using System.Text.Json;
using Gravia.Domain.Common.Events;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Events;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class SettingTests
{
    private static readonly LocalizedText Title = new("en", "Iterations");
    private static readonly SettingsUiPath UiPath = new(new LocalizedText("en", "General"));

    [Fact]
    public void ConstructorPreservesMetadataAndInitializesValueFromDefault()
    {
        var description = new LocalizedText("en", "Iteration count");
        var setting = new IntSetting("plugin.iterations", Title, 10, UiPath, description);

        ISetting common = setting;
        Assert.Equal("plugin.iterations", common.Id);
        Assert.Same(Title, common.Title);
        Assert.Same(UiPath, common.UiPath);
        Assert.Same(description, common.Description);
        Assert.Equal(10, setting.DefaultValue);
        Assert.Equal(10, setting.Value);
    }

    [Fact]
    public void SetValueAndResetNotifyWhileSilentAssignmentAndDeserializationDoNot()
    {
        var setting = new IntSetting("iterations", Title, 10, UiPath);
        var notifiedValues = new List<int>();
        void OnRaised(DomainEvent domainEvent)
        {
            if (domainEvent is SettingChanged changed && ReferenceEquals(changed.Setting, setting))
                notifiedValues.Add(setting.Value);
        }

        DomainEventPublisher.DomainEventRaised += OnRaised;
        try
        {
            setting.SetValueWithoutNotify(20);
            Assert.Equal(20, setting.Value);
            Assert.Empty(notifiedValues);

            setting.SetValue(30);
            Assert.Equal(new[] { 30 }, notifiedValues);

            setting.DeserializeValue(JsonSerializer.SerializeToElement(40));
            Assert.Equal(40, setting.Value);
            Assert.Equal(new[] { 30 }, notifiedValues);

            ((ISetting)setting).Reset();
            Assert.Equal(10, setting.Value);
            Assert.Equal(10, setting.DefaultValue);
            Assert.Equal(new[] { 30, 10 }, notifiedValues);
        }
        finally { DomainEventPublisher.DomainEventRaised -= OnRaised; }
    }

    [Fact]
    public void FailedDeserializationPreservesValueAndDoesNotNotify()
    {
        var setting = new IntSetting("iterations", Title, 10, UiPath);
        setting.SetValueWithoutNotify(20);
        var notifications = 0;
        void OnRaised(DomainEvent domainEvent)
        {
            if (domainEvent is SettingChanged changed && ReferenceEquals(changed.Setting, setting))
                notifications++;
        }

        DomainEventPublisher.DomainEventRaised += OnRaised;
        try
        {
            Assert.Throws<JsonException>(() => setting.DeserializeValue(JsonSerializer.SerializeToElement("wrong")));
            Assert.Equal(20, setting.Value);
            Assert.Equal(10, setting.DefaultValue);
            Assert.Equal(0, notifications);
        }
        finally { DomainEventPublisher.DomainEventRaised -= OnRaised; }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidIdIsRejected(string? id)
        => Assert.ThrowsAny<ArgumentException>(() => new IntSetting(id!, Title, 10, UiPath));

    [Fact]
    public void NullTitleIsRejected()
        => Assert.Throws<ArgumentNullException>(() => new IntSetting("iterations", null!, 10, UiPath));

    [Fact]
    public void NullUiPathIsRejected()
        => Assert.Throws<ArgumentNullException>(() => new IntSetting("iterations", Title, 10, null!));
}
