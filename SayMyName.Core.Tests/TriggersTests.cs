using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class TriggersTests
{
    private static TriggerSettings On(params ChatChannel[] channels) => new() { Enabled = true, Channels = [.. channels] };

    [Fact]
    public void TargetSpeaksListensOnEveryChannelButStandardEmotesWithoutSavedChannels()
    {
        Assert.Equal(
            Enum.GetValues<ChatChannel>().Except([ChatChannel.StandardEmote]),
            Enum.GetValues<ChatChannel>().Where(c => Triggers.Listens(TriggerKind.TargetSpeaks, On(), c)));
    }

    [Fact]
    public void EmoteTriggerListensOnlyOnStandardEmotesWhateverIsSaved()
    {
        Assert.Equal(
            [ChatChannel.StandardEmote],
            Enum.GetValues<ChatChannel>().Where(c => Triggers.Listens(TriggerKind.Emote, On(ChatChannel.Say), c)));
    }

    [Fact]
    public void OnlyTheEmoteTriggerListensOnStandardEmotes()
    {
        // Every channel saved, as a hand-edited config might; loading drops the ones a trigger may not use
        var saved = Triggers.All.ToDictionary(k => k, _ => On(Enum.GetValues<ChatChannel>()));
        Assert.Equal([TriggerKind.Emote], Triggers.All.Where(k => Triggers.Listens(k, Triggers.Resolve(saved, k), ChatChannel.StandardEmote)));
    }

    [Theory]
    [InlineData(TriggerKind.Name)]
    [InlineData(TriggerKind.Custom)]
    [InlineData(TriggerKind.Emote)]
    [InlineData(TriggerKind.You)]
    [InlineData(TriggerKind.TargetSpeaks)]
    public void TriggerThatIsOffListensNowhere(TriggerKind kind)
    {
        var off = new TriggerSettings { Enabled = false, Channels = [.. Enum.GetValues<ChatChannel>()] };
        Assert.DoesNotContain(Enum.GetValues<ChatChannel>(), c => Triggers.Listens(kind, off, c));
    }

    [Fact]
    public void ChosenChannelTriggersListenOnlyOnTheirSavedChannels()
    {
        Assert.True(Triggers.Listens(TriggerKind.Name, On(ChatChannel.Party), ChatChannel.Party));
        Assert.False(Triggers.Listens(TriggerKind.Name, On(ChatChannel.Party), ChatChannel.Say));
        Assert.False(Triggers.Listens(TriggerKind.You, On(), ChatChannel.Say));
    }

    [Fact]
    public void ChannelTableHasNoStandardEmoteRowOrEmoteColumn()
    {
        Assert.DoesNotContain(ChatChannel.StandardEmote, Triggers.ChannelsByUsage);
        Assert.Equal([TriggerKind.Name, TriggerKind.Custom, TriggerKind.You], Triggers.WithChannelChoice);
    }

    // revert-check: not revert-sensitive (the rule predates the change; the edit only loosens the Target Speaks assert to still hold once it listens everywhere)
    [Fact]
    public void StandardEmotesAreDroppedFromTextTriggers()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Name] = new() { Enabled = true, Channels = [ChatChannel.Say, ChatChannel.StandardEmote] },
        };

        Assert.Equal([ChatChannel.Say], Triggers.Resolve(saved, TriggerKind.Name).Channels);
    }

    [Theory]
    [InlineData(TriggerKind.Emote)]
    [InlineData(TriggerKind.TargetSpeaks)]
    public void TriggersWithoutChannelChoiceKeepNoChannels(TriggerKind kind)
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [kind] = On(ChatChannel.Say, ChatChannel.StandardEmote),
        };

        Assert.Empty(Triggers.AllowedChannels(kind));
        Assert.Empty(Triggers.Default(kind).Channels);
        var settings = Triggers.Resolve(saved, kind);
        Assert.True(settings.Enabled);
        Assert.Empty(settings.Channels);
    }

    [Fact]
    public void YouTriggerAllowsSayPartyAllianceAndCustomEmotes()
    {
        Assert.Equal([ChatChannel.Say, ChatChannel.CustomEmote, ChatChannel.Party, ChatChannel.Alliance], Triggers.AllowedChannels(TriggerKind.You).Order());
    }

    [Theory]
    [InlineData(TriggerKind.Name)]
    [InlineData(TriggerKind.Custom)]
    public void WordTriggersAllowEveryChannelButStandardEmotes(TriggerKind kind)
    {
        Assert.Equal(Enum.GetValues<ChatChannel>().Except([ChatChannel.StandardEmote]), Triggers.AllowedChannels(kind));
    }

    [Fact]
    public void ChannelsUsableByAllTriggersComeFirstThenTheRestInOrder()
    {
        ChatChannel[] first = [ChatChannel.Say, ChatChannel.CustomEmote, ChatChannel.Party, ChatChannel.Alliance];
        Assert.Equal(
            [.. first, .. Enum.GetValues<ChatChannel>().Except([.. first, ChatChannel.StandardEmote])],
            Triggers.ChannelsByUsage);
    }

    [Fact]
    public void OnlyEmoteIsOffByDefault()
    {
        Assert.Equal(
            [TriggerKind.Emote],
            Triggers.All.Where(k => !Triggers.Default(k).Enabled));
    }

    // revert-check: not revert-sensitive (the rule predates the change; the edit only leaves out Target Speaks, which has no channel choice and is covered by its own test)
    [Fact]
    public void LargeGroupChannelsStartOffAndTheRestOn()
    {
        ChatChannel[] linkshells = [.. Enum.GetValues<ChatChannel>().Where(c => c.ToString().Contains("Linkshell"))];
        ChatChannel[] groups = [ChatChannel.Alliance, ChatChannel.FreeCompany, ChatChannel.NoviceNetwork, .. linkshells];
        Assert.Equal(16, linkshells.Length);

        foreach (var kind in Triggers.WithChannelChoice)
        {
            Assert.Equal(
                Triggers.AllowedChannels(kind).Except(groups).Order(),
                Triggers.Default(kind).Channels.Order());
        }
        Assert.Equal([ChatChannel.Say, ChatChannel.CustomEmote, ChatChannel.Party], Triggers.Default(TriggerKind.You).Channels.Order());
        Assert.Contains(ChatChannel.Shout, Triggers.Default(TriggerKind.Name).Channels);
    }

    [Fact]
    public void DefaultsDoNotShareChannelSets()
    {
        var a = Triggers.Default(TriggerKind.Name);
        var b = Triggers.Default(TriggerKind.Name);
        a.Channels.Remove(ChatChannel.Say);
        Assert.Contains(ChatChannel.Say, b.Channels);
    }

    // revert-check: not revert-sensitive (checks Resolve stores the default; the edit only compares against Default() instead of every allowed channel)
    [Fact]
    public void ResolveAddsMissingDefaultsAndStoresThem()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>();
        var settings = Triggers.Resolve(saved, TriggerKind.Custom);

        Assert.Same(settings, saved[TriggerKind.Custom]);
        Assert.Equal(Triggers.Default(TriggerKind.Custom).Enabled, settings.Enabled);
        Assert.Equal(Triggers.Default(TriggerKind.Custom).Channels.Order(), settings.Channels.Order());
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

    // revert-check: not revert-sensitive (the dropping rule predates the change; edits only swap in channels that stay disallowed)
    [Fact]
    public void ResolveDropsChannelsTheTriggerMayNotUse()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.You] = new() { Enabled = true, Channels = [ChatChannel.Say, ChatChannel.Linkshell1, ChatChannel.StandardEmote, (ChatChannel)999] },
        };

        Assert.Equal([ChatChannel.Say], Triggers.Resolve(saved, TriggerKind.You).Channels);
    }
}
