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

    // revert-check: not revert-sensitive (checks the save/load round trip; the edit only compares against the default instead of every allowed channel)
    [Fact]
    public void ChannelsTurnedOffStayOff()
    {
        var config = Fresh();
        config.Triggers[TriggerKind.Name].Channels.Remove(ChatChannel.Say);
        config.Triggers[TriggerKind.You].Channels.Clear();

        var loaded = SaveAndLoad(config);

        Assert.Equal(
            Triggers.Default(TriggerKind.Name).Channels.Where(c => c != ChatChannel.Say).Order(),
            loaded.Triggers[TriggerKind.Name].Channels.Order());
        Assert.Empty(loaded.Triggers[TriggerKind.You].Channels);
    }

    [Fact]
    public void EnabledFlagsSurviveBothWays()
    {
        // Every trigger both ways, so the check holds whichever way each default is set
        var config = Fresh();
        foreach (var enabled in new[] { true, false })
        {
            foreach (var kind in Triggers.All)
                config.Triggers[kind].Enabled = enabled;

            var loaded = SaveAndLoad(config);

            Assert.All(Triggers.All, kind => Assert.Equal(enabled, loaded.Triggers[kind].Enabled));
        }
    }

    [Fact]
    public void ScalarSettingsSurvive()
    {
        // revert-check: not revert-sensitive (checks saving; a branch that only changes defaults leaves saving intact)
        var config = Fresh();
        // Flipped from the default, so the check holds whichever way the default is set
        var nameCaseSensitive = !config.NameCaseSensitive;
        var playSound = !config.PlaySound;
        var customWordsCaseSensitive = !config.CustomWordsCaseSensitive;
        var onlyTargetingYou = !config.TargetSpeaksOnlyTargetingYou;
        var highlight = !config.Highlight;
        var highlightTabs = !config.HighlightTabs;
        config.NameCaseSensitive = nameCaseSensitive;
        config.PlaySound = playSound;
        config.SoundEffect = 7;
        config.CooldownSeconds = 0;
        config.Nicknames = "Ro\nBig Ro";
        config.CustomWords = "raid\ngood morning";
        config.CustomWordsCaseSensitive = customWordsCaseSensitive;
        config.TargetSpeaksOnlyTargetingYou = onlyTargetingYou;
        config.Highlight = highlight;
        config.HighlightTextColor = 45;
        config.HighlightGlowColor = ConfigurationBase.NoColor;
        config.HighlightTabs = highlightTabs;
        config.TabHighlightColor = 31;

        var loaded = SaveAndLoad(config);

        Assert.Equal(nameCaseSensitive, loaded.NameCaseSensitive);
        Assert.Equal(playSound, loaded.PlaySound);
        Assert.Equal(7, loaded.SoundEffect);
        Assert.Equal(0, loaded.CooldownSeconds);
        Assert.Equal("Ro\nBig Ro", loaded.Nicknames);
        Assert.Equal("raid\ngood morning", loaded.CustomWords);
        Assert.Equal(customWordsCaseSensitive, loaded.CustomWordsCaseSensitive);
        Assert.Equal(onlyTargetingYou, loaded.TargetSpeaksOnlyTargetingYou);
        Assert.Equal(highlight, loaded.Highlight);
        Assert.Equal(45, loaded.HighlightTextColor);
        Assert.Equal(ConfigurationBase.NoColor, loaded.HighlightGlowColor);
        Assert.Equal(highlightTabs, loaded.HighlightTabs);
        Assert.Equal(31, loaded.TabHighlightColor);
    }

    [Fact]
    public void SavedTabColourNoneLoadsAsTabHighlightingOff()
    {
        var config = Fresh();
        config.HighlightTabs = true;
        config.TabHighlightColor = ConfigurationBase.NoColor;

        var loaded = SaveAndLoad(config);

        Assert.False(loaded.HighlightTabs);
        Assert.Equal(Fresh().TabHighlightColor, loaded.TabHighlightColor);
    }

    [Fact]
    public void TargetSpeaksAlertsOnlyWhileTargetingYouByDefault()
    {
        Assert.True(SaveAndLoad(Fresh()).TargetSpeaksOnlyTargetingYou);
    }

    [Fact]
    public void FreshConfigHasDefaultSoundAndColours()
    {
        var loaded = SaveAndLoad(Fresh());

        Assert.Equal(16, loaded.SoundEffect);
        Assert.Equal(535, loaded.HighlightTextColor);
        Assert.Equal(18, loaded.HighlightGlowColor);
        Assert.Equal(576, loaded.TabHighlightColor);
    }

    [Fact]
    public void OnlyTriggerSectionStartsExpanded()
    {
        Assert.Equal([SettingsSection.Triggers], SaveAndLoad(Fresh()).ExpandedSections!);
    }

    [Fact]
    public void ExpandedSectionsSurvive()
    {
        // The opposite of the default set, so the check holds whichever sections start open
        var config = Fresh();
        var opposite = Enum.GetValues<SettingsSection>().Except(config.ExpandedSections!).ToHashSet();
        Assert.NotEmpty(opposite);
        config.ExpandedSections = [.. opposite];

        var loaded = SaveAndLoad(config);

        Assert.Equal(opposite.Order(), loaded.ExpandedSections!.Order());
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
    public void OverlongCustomWordsAreCutToTheLimit()
    {
        var config = Fresh();
        config.CustomWords = new string('a', ConfigurationBase.MaxCustomWordsLength + 10);

        Assert.Equal(ConfigurationBase.MaxCustomWordsLength, SaveAndLoad(config).CustomWords.Length);
    }

    [Fact]
    public void OverlongNicknamesAreCutToTheLimit()
    {
        var config = Fresh();
        config.Nicknames = new string('a', ConfigurationBase.MaxNicknamesLength + 10);

        Assert.Equal(ConfigurationBase.MaxNicknamesLength, SaveAndLoad(config).Nicknames.Length);
    }

    [Fact]
    public void ChannelsAreSavedByName()
    {
        var config = Fresh();
        config.Triggers[TriggerKind.You].Channels = [ChatChannel.CustomEmote];

        var json = JsonConvert.SerializeObject(config, DalamudSettings);

        Assert.Contains("\"Channels\":[\"CustomEmote\"]", json);
    }

    [Fact]
    public void UnknownSavedChannelsAreDropped()
    {
        var json = JsonConvert.SerializeObject(Fresh(), DalamudSettings);
        var nameChannels = JsonConvert.SerializeObject(Triggers.Default(TriggerKind.Name).Channels.Select(c => c.ToString()));
        var edited = json.Replace($"\"Channels\":{nameChannels}", "\"Channels\":[\"Say\",\"PvPTeam\",\"say\",\"3\",1,8,-1,27,null,\"Party\"]", StringComparison.Ordinal);
        Assert.NotEqual(json, edited);

        var loaded = JsonConvert.DeserializeObject<ConfigurationBase>(edited, DalamudSettings)!;
        loaded.Normalize();

        Assert.Equal([ChatChannel.Say, ChatChannel.Party], loaded.Triggers[TriggerKind.Name].Channels.Order());
    }

    [Fact]
    public void LoadsAsTheSavedType()
    {
        Assert.IsType<TestConfiguration>(SaveAndLoad(Fresh()));
    }
}
