using Gravia.Domain.Localization;
using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Interfaces;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class SettingTests
{
    [Fact]
    public void ConstructorPreservesMetadataAndInitializesValueFromDefault()
    {
        var title = new LocalizedText("en", "Iterations");
        var description = new LocalizedText("en", "Maximum iterations");
        var setting = new PositiveSetting("plugin.iterations", title, 10, description);

        ISetting common = setting;
        Assert.Equal("plugin.iterations", common.Id);
        Assert.Same(title, common.Title);
        Assert.Same(description, common.Description);
        Assert.Equal(10, setting.DefaultValue);
        Assert.Equal(10, setting.Value);
    }

    [Fact]
    public void SetValueAndResetPreserveTheDefault()
    {
        var setting = Create();

        setting.SetValue(20);
        Assert.Equal(20, setting.Value);
        Assert.Equal(10, setting.DefaultValue);

        ((ISetting)setting).Reset();
        Assert.Equal(10, setting.Value);
        Assert.Equal(10, setting.DefaultValue);
        Assert.Null(setting.Description);
    }

    [Fact]
    public void FailedValidationPreservesTheCurrentValue()
    {
        var setting = Create();
        setting.SetValue(20);

        Assert.Throws<ArgumentOutOfRangeException>(() => setting.SetValue(0));
        Assert.Equal(20, setting.Value);
        Assert.Equal(10, setting.DefaultValue);
    }

    [Fact]
    public void ResetUsesValidation()
    {
        var setting = Create();
        setting.SetValue(20);
        setting.Reset();

        Assert.Equal(new[] { 10, 20, 10 }, setting.ValidatedValues);
    }

    [Fact]
    public void DerivedConstructorValidatesDefaultAfterInitializingConstraints()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Create(0));
        Assert.Equal(10, Create().Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t")]
    public void InvalidIdIsRejected(string? id)
    {
        Assert.ThrowsAny<ArgumentException>(() => new PositiveSetting(id!, new LocalizedText("en", "Title"), 10));
    }

    [Fact]
    public void NullTitleIsRejected()
    {
        Assert.Throws<ArgumentNullException>(() => new PositiveSetting("example", null!, 10));
    }

    private static PositiveSetting Create(int defaultValue = 10)
        => new("iterations", new LocalizedText("en", "Iterations"), defaultValue);

    private sealed class PositiveSetting : Setting<int>
    {
        private readonly int _minimum;
        public List<int> ValidatedValues { get; } = [];

        public PositiveSetting(string id, LocalizedText title, int defaultValue, LocalizedText? description = null)
            : base(id, title, defaultValue, description)
        {
            _minimum = 1;
            ValidateValue(defaultValue);
        }

        protected override void ValidateValue(int value)
        {
            Assert.Equal(1, _minimum);
            ValidatedValues.Add(value);
            if (value < _minimum) throw new ArgumentOutOfRangeException(nameof(value));
        }
    }
}
