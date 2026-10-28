using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class TriggersTests
{
    [Fact]
    public void TargetSpeaksOnlyAllowsSayAndEmotes()
    {
        Assert.Equal(
            [ChatChannel.Say, ChatChannel.StandardEmote, ChatChannel.CustomEmote],
            Triggers.AllowedChannels(TriggerKind.TargetSpeaks).Order());
    }

    [Fact]
    public void EmoteTriggerOnlyAllowsStandardEmotes()
    {
        Assert.Equal([ChatChannel.StandardEmote], Triggers.AllowedChannels(TriggerKind.Emote));
    }

    [Fact]
    public void YouTriggerTreatsCustomEmotesAsTextLikeSay()
    {
        Assert.Equal([ChatChannel.Say, ChatChannel.CustomEmote], Triggers.AllowedChannels(TriggerKind.You).Order());
    }

    [Fact]
    public void LegacyTargetedIsNotATrigger()
    {
        Assert.DoesNotContain(TriggerKind.Targeted, Triggers.All);
    }

    [Fact]
    public void MigrateSplitsTargetedKeepingOnOffAndUsableChannels()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Targeted] = new() { Enabled = false, Channels = [ChatChannel.StandardEmote, ChatChannel.CustomEmote] },
        };

        Triggers.Migrate(saved);
        var emote = Triggers.Resolve(saved, TriggerKind.Emote);
        var you = Triggers.Resolve(saved, TriggerKind.You);

        Assert.DoesNotContain(TriggerKind.Targeted, saved.Keys);
        Assert.False(emote.Enabled);
        Assert.Equal([ChatChannel.StandardEmote], emote.Channels);
        Assert.False(you.Enabled);
        Assert.Equal([ChatChannel.CustomEmote], you.Channels);
    }

    [Fact]
    public void MigrateWithoutEmotesLeavesEmoteTriggerListeningNowhere()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Targeted] = new() { Enabled = true, Channels = [ChatChannel.Say] },
        };

        Triggers.Migrate(saved);

        Assert.Empty(Triggers.Resolve(saved, TriggerKind.Emote).Channels);
        Assert.Equal([ChatChannel.Say], Triggers.Resolve(saved, TriggerKind.You).Channels);
    }

    [Fact]
    public void MigrateKeepsSettingsAlreadySavedForTheNewTriggers()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Targeted] = new() { Enabled = true, Channels = [ChatChannel.Say] },
            [TriggerKind.You] = new() { Enabled = false, Channels = [ChatChannel.CustomEmote] },
        };

        Triggers.Migrate(saved);

        Assert.False(saved[TriggerKind.You].Enabled);
        Assert.Equal([ChatChannel.CustomEmote], saved[TriggerKind.You].Channels);
        Assert.True(saved[TriggerKind.Emote].Enabled);
    }

    [Theory]
    [InlineData(TriggerKind.Name)]
    [InlineData(TriggerKind.Custom)]
    public void WordTriggersAllowEveryChannel(TriggerKind kind)
    {
        Assert.Equal(Enum.GetValues<ChatChannel>(), Triggers.AllowedChannels(kind));
    }

    [Fact]
    public void ChannelsUsableByAllTriggersComeFirstThenTheRestInOrder()
    {
        ChatChannel[] first = [ChatChannel.Say, ChatChannel.StandardEmote, ChatChannel.CustomEmote];
        Assert.Equal(
            [.. first, .. Enum.GetValues<ChatChannel>().Except(first)],
            Triggers.ChannelsByUsage);
    }

    [Fact]
    public void OnlyTargetSpeaksIsOffByDefault()
    {
        Assert.Equal(
            [TriggerKind.TargetSpeaks],
            Triggers.All.Where(k => !Triggers.Default(k).Enabled));
    }

    [Fact]
    public void DefaultsDoNotShareChannelSets()
    {
        var a = Triggers.Default(TriggerKind.Name);
        var b = Triggers.Default(TriggerKind.Name);
        a.Channels.Remove(ChatChannel.Say);
        Assert.Contains(ChatChannel.Say, b.Channels);
    }

    [Fact]
    public void ResolveAddsMissingDefaultsAndStoresThem()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>();
        var settings = Triggers.Resolve(saved, TriggerKind.Custom);

        Assert.Same(settings, saved[TriggerKind.Custom]);
        Assert.True(settings.Enabled);
        Assert.Equal(Triggers.AllowedChannels(TriggerKind.Custom).Count, settings.Channels.Count);
    }

    [Fact]
    public void ResolveKeepsSavedChoices()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Name] = new() { Enabled = false, Channels = [ChatChannel.Party] },
        };

        var settings = Triggers.Resolve(saved, TriggerKind.Name);

        Assert.False(settings.Enabled);
        Assert.Equal([ChatChannel.Party], settings.Channels);
    }

    [Fact]
    public void ResolveDropsChannelsTheTriggerMayNotUse()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.You] = new() { Enabled = true, Channels = [ChatChannel.Say, ChatChannel.Party, ChatChannel.StandardEmote, (ChatChannel)999] },
        };

        Assert.Equal([ChatChannel.Say], Triggers.Resolve(saved, TriggerKind.You).Channels);
    }
}
