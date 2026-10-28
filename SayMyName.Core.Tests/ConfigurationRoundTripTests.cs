using Newtonsoft.Json;
using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class ConfigurationRoundTripTests
{
    // Dalamud's PluginConfigurations saves and loads plugin configs with these settings
    private static readonly JsonSerializerSettings DalamudSettings = new()
    {
        TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
        TypeNameHandling = TypeNameHandling.Objects,
    };

    // Stands in for the plugin's Configuration, which adds only Dalamud's interface
    private sealed class TestConfiguration : ConfigurationBase
    {
        public int Version { get; set; }
    }

    private static TestConfiguration Fresh()
    {
        var config = new TestConfiguration();
        config.Normalize();
        return config;
    }

    // As the plugin does on load: deserialize, then Normalize()
    private static ConfigurationBase SaveAndLoad(ConfigurationBase config)
    {
        var json = JsonConvert.SerializeObject(config, Formatting.Indented, DalamudSettings);
        var loaded = JsonConvert.DeserializeObject<ConfigurationBase>(json, DalamudSettings)!;
        loaded.Normalize();
        return loaded;
    }

    [Fact]
    public void ChannelsTurnedOffStayOff()
    {
        var config = Fresh();
        config.Triggers[TriggerKind.Name].Channels.Remove(ChatChannel.Say);
        config.Triggers[TriggerKind.You].Channels.Clear();

        var loaded = SaveAndLoad(config);

        Assert.Equal(
            Triggers.AllowedChannels(TriggerKind.Name).Where(c => c != ChatChannel.Say).Order(),
            loaded.Triggers[TriggerKind.Name].Channels.Order());
        Assert.Empty(loaded.Triggers[TriggerKind.You].Channels);
    }

    [Fact]
    public void EnabledFlagsSurviveBothWays()
    {
        var config = Fresh();
        config.Triggers[TriggerKind.Name].Enabled = false;
        config.Triggers[TriggerKind.TargetSpeaks].Enabled = true;

        var loaded = SaveAndLoad(config);

        Assert.False(loaded.Triggers[TriggerKind.Name].Enabled);
        Assert.True(loaded.Triggers[TriggerKind.TargetSpeaks].Enabled);
    }

    [Fact]
    public void ScalarSettingsSurvive()
    {
        var config = Fresh();
        config.NameCaseSensitive = false;
        config.PlaySound = false;
        config.SoundEffect = 7;
        config.CooldownSeconds = 0;
        config.CustomWords = "raid\ngood morning";
        config.CustomWordsCaseSensitive = true;
        config.Highlight = false;
        config.HighlightTextColor = 45;
        config.HighlightGlowColor = ConfigurationBase.NoColor;
        config.HighlightTabs = false;
        config.TabHighlightColor = 31;

        var loaded = SaveAndLoad(config);

        Assert.False(loaded.NameCaseSensitive);
        Assert.False(loaded.PlaySound);
        Assert.Equal(7, loaded.SoundEffect);
        Assert.Equal(0, loaded.CooldownSeconds);
        Assert.Equal("raid\ngood morning", loaded.CustomWords);
        Assert.True(loaded.CustomWordsCaseSensitive);
        Assert.False(loaded.Highlight);
        Assert.Equal(45, loaded.HighlightTextColor);
        Assert.Equal(ConfigurationBase.NoColor, loaded.HighlightGlowColor);
        Assert.False(loaded.HighlightTabs);
        Assert.Equal(31, loaded.TabHighlightColor);
    }

    [Fact]
    public void OnlyTriggerSectionStartsExpanded()
    {
        Assert.Equal([SettingsSection.Triggers], SaveAndLoad(Fresh()).ExpandedSections!);
    }

    [Fact]
    public void ExpandedSectionsSurvive()
    {
        var config = Fresh();
        config.ExpandedSections!.Remove(SettingsSection.Triggers);
        config.ExpandedSections.Add(SettingsSection.Channels);

        var loaded = SaveAndLoad(config);

        Assert.Equal([SettingsSection.Channels], loaded.ExpandedSections!);
    }

    [Fact]
    public void AllSectionsClosedStaysClosed()
    {
        var config = Fresh();
        config.ExpandedSections!.Clear();

        Assert.Empty(SaveAndLoad(config).ExpandedSections!);
    }

    [Fact]
    public void TriggerMissingFromSavedConfigGetsDefaults()
    {
        var config = Fresh();
        config.Triggers.Remove(TriggerKind.Custom);

        var loaded = SaveAndLoad(config);

        var expected = Triggers.Default(TriggerKind.Custom);
        Assert.Equal(expected.Enabled, loaded.Triggers[TriggerKind.Custom].Enabled);
        Assert.Equal(expected.Channels.Order(), loaded.Triggers[TriggerKind.Custom].Channels.Order());
    }

    [Fact]
    public void OlderTargetedSettingsLoadAsEmoteAndYou()
    {
        var config = Fresh();
        config.Triggers.Remove(TriggerKind.Emote);
        config.Triggers.Remove(TriggerKind.You);
        config.Triggers[TriggerKind.Targeted] = new() { Enabled = false, Channels = [ChatChannel.Say, ChatChannel.StandardEmote] };

        var loaded = SaveAndLoad(config);

        Assert.DoesNotContain(TriggerKind.Targeted, loaded.Triggers.Keys);
        Assert.False(loaded.Triggers[TriggerKind.Emote].Enabled);
        Assert.Equal([ChatChannel.StandardEmote], loaded.Triggers[TriggerKind.Emote].Channels);
        Assert.False(loaded.Triggers[TriggerKind.You].Enabled);
        Assert.Equal([ChatChannel.Say], loaded.Triggers[TriggerKind.You].Channels);
    }

    [Fact]
    public void OverlongCustomWordsAreCutToTheLimit()
    {
        var config = Fresh();
        config.CustomWords = new string('a', ConfigurationBase.MaxCustomWordsLength + 10);

        Assert.Equal(ConfigurationBase.MaxCustomWordsLength, SaveAndLoad(config).CustomWords.Length);
    }

    [Fact]
    public void LoadsAsTheSavedType()
    {
        Assert.IsType<TestConfiguration>(SaveAndLoad(Fresh()));
    }
}
