using Gravia.Domain.Localization.Entities;
using Gravia.Domain.Settings.Entities;
using Gravia.Domain.Settings.Exceptions;

namespace Gravia.Domain.Tests.Settings.Entities;

public sealed class ChoiceSettingTests
{
    private static readonly LocalizedText Title = new("en", "Theme");

    [Fact]
    public void SupportsSelectionAndResetUsingIdsNotTitles()
    {
        var description = new LocalizedText("en", "Application theme");
        var setting = new ChoiceSetting("theme", Title, "system", Options(), description);

        Assert.Same(Title, setting.Title);
        Assert.Same(description, setting.Description);
        Assert.Equal("system", setting.Value);
        setting.SetValue("dark");
        Assert.Equal("dark", setting.Value);
        Assert.Equal("system", setting.DefaultValue);
        Assert.Equal("Тёмная", setting.Options[1].Title.Resolve("ru"));
        setting.Reset();
        Assert.Equal("system", setting.Value);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("Dark")]
    [InlineData("Тёмная")]
    [InlineData("")]
    [InlineData(null)]
    public void InvalidDefaultAndChangesAreDomainErrorsAndPreserveValue(string? invalid)
    {
        Assert.Throws<InvalidSettingValueException>(() => new ChoiceSetting("theme", Title, invalid!, Options()));
        var setting = new ChoiceSetting("theme", Title, "system", Options());
        setting.SetValue("dark");

        var error = Assert.Throws<InvalidSettingValueException>(() => setting.SetValue(invalid!));

        Assert.Equal("setting_value_invalid", error.Code);
        Assert.Equal("theme", error.Extra["settingId"]);
        Assert.Single(error.Extra);
        Assert.Equal("dark", setting.Value);
        Assert.Equal("system", setting.DefaultValue);
    }

    [Fact]
    public void OptionsAreAnImmutableSnapshotInDeclarationOrder()
    {
        var options = Options();
        var setting = new ChoiceSetting("theme", Title, "system", options);
        options[0] = new SettingOption("other", Title);
        var exposed = Assert.IsAssignableFrom<IList<SettingOption>>(setting.Options);

        Assert.Throws<NotSupportedException>(() => exposed[0] = options[0]);
        Assert.Throws<NotSupportedException>(() => exposed.Clear());
        Assert.Equal(new[] { "system", "dark" }, setting.Options.Select(option => option.Id));
        setting.SetValue("system");
        Assert.Throws<InvalidSettingValueException>(() => setting.SetValue("other"));
    }

    [Fact]
    public void CaseDistinctIdsAndIdenticalTitlesAreAllowed()
    {
        var setting = new ChoiceSetting("choice", Title, "id",
            [new SettingOption("id", Title), new SettingOption("ID", Title)]);

        setting.SetValue("ID");
        Assert.Equal("ID", setting.Value);
        setting.Reset();
        Assert.Equal("id", setting.Value);
    }

    [Fact]
    public void SupportsASingleOption()
    {
        var setting = new ChoiceSetting("choice", Title, "only", [new SettingOption("only", Title)]);

        Assert.Single(setting.Options);
        setting.SetValue("only");
        setting.Reset();
        Assert.Equal("only", setting.Value);
    }

    [Fact]
    public void RejectsNullEmptyAndNullContainingOptions()
    {
        Assert.Throws<ArgumentNullException>(() => new ChoiceSetting("theme", Title, "system", null!));
        Assert.Throws<ArgumentException>(() => new ChoiceSetting("theme", Title, "system", []));
        Assert.Throws<ArgumentException>(() => new ChoiceSetting("theme", Title, "system", [null!]));
    }

    [Fact]
    public void RejectsDuplicateIdsEvenWithDifferentTitles()
    {
        Assert.Throws<ArgumentException>(() => new ChoiceSetting("theme", Title, "same",
        [
            new SettingOption("same", new LocalizedText("en", "First")),
            new SettingOption("same", new LocalizedText("en", "Second"))
        ]));
    }

    private static SettingOption[] Options() =>
    [
        new("system", new LocalizedText("en", "System").With("ru", "Системная")),
        new("dark", new LocalizedText("en", "Dark").With("ru", "Тёмная"))
    ];
}
