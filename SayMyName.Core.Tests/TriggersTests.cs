using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class TriggersTests
{
    [Theory]
    [InlineData(TriggerKind.Targeted)]
    [InlineData(TriggerKind.TargetSpeaks)]
    public void TargetTriggersOnlyAllowSayAndEmotes(TriggerKind kind)
    {
        Assert.Equal(
            [ChatChannel.Say, ChatChannel.StandardEmote, ChatChannel.CustomEmote],
            Triggers.AllowedChannels(kind).Order());
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
            [TriggerKind.Targeted] = new() { Enabled = true, Channels = [ChatChannel.Say, ChatChannel.Party, (ChatChannel)999] },
        };

        Assert.Equal([ChatChannel.Say], Triggers.Resolve(saved, TriggerKind.Targeted).Channels);
    }
}
