using SayMyName.Core;

namespace SayMyName.Core.Tests;

public class TriggersTests
{
    [Fact]
    public void TargetSpeaksListensOnEveryChannelButStandardEmotes()
    {
        Assert.Equal(Enum.GetValues<ChatChannel>().Except([ChatChannel.StandardEmote, ChatChannel.CrossParty, ChatChannel.PvPTeam]), Triggers.AllowedChannels(TriggerKind.TargetSpeaks));
        Assert.Equal(Triggers.AllowedChannels(TriggerKind.TargetSpeaks).Order(), Triggers.Default(TriggerKind.TargetSpeaks).Channels.Order());
    }

    [Fact]
    public void OnlyTheEmoteTriggerListensOnStandardEmotes()
    {
        Assert.Equal([TriggerKind.Emote], Triggers.All.Where(k => Triggers.AllowedChannels(k).Contains(ChatChannel.StandardEmote)));
    }

    [Fact]
    public void ChannelTableHasNoStandardEmoteRowOrEmoteColumn()
    {
        Assert.DoesNotContain(ChatChannel.StandardEmote, Triggers.ChannelsByUsage);
        Assert.Equal([TriggerKind.Name, TriggerKind.Custom, TriggerKind.You], Triggers.WithChannelChoice);
    }

    [Fact]
    public void EmoteTriggerWithStandardEmotesUntickedLoadsOff()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Emote] = new() { Enabled = true, Channels = [] },
        };

        var emote = Triggers.Resolve(saved, TriggerKind.Emote);

        Assert.False(emote.Enabled);
        Assert.Equal([ChatChannel.StandardEmote], emote.Channels);
        // Settled: a second load keeps it as the user leaves it
        emote.Enabled = true;
        Assert.True(Triggers.Resolve(saved, TriggerKind.Emote).Enabled);
    }

    // revert-check: not revert-sensitive (the rule predates the change; the edit only loosens the Target Speaks assert to still hold once it listens everywhere)
    [Fact]
    public void StandardEmotesAreDroppedFromTextTriggers()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Name] = new() { Enabled = true, Channels = [ChatChannel.Say, ChatChannel.StandardEmote] },
            [TriggerKind.TargetSpeaks] = new() { Enabled = true, Channels = [ChatChannel.StandardEmote, ChatChannel.CustomEmote] },
        };

        Assert.Equal([ChatChannel.Say], Triggers.Resolve(saved, TriggerKind.Name).Channels);
        Assert.DoesNotContain(ChatChannel.StandardEmote, Triggers.Resolve(saved, TriggerKind.TargetSpeaks).Channels);
    }

    [Fact]
    public void EmoteTriggerOnlyAllowsStandardEmotes()
    {
        Assert.Equal([ChatChannel.StandardEmote], Triggers.AllowedChannels(TriggerKind.Emote));
    }

    [Fact]
    public void YouTriggerAllowsSayPartyAllianceAndCustomEmotes()
    {
        Assert.Equal([ChatChannel.Say, ChatChannel.Party, ChatChannel.Alliance, ChatChannel.CustomEmote], Triggers.AllowedChannels(TriggerKind.You).Order());
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
    public void MigrateWithoutEmotesTurnsEmoteTriggerOff()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Targeted] = new() { Enabled = true, Channels = [ChatChannel.Say] },
        };

        Triggers.Migrate(saved);

        Assert.False(Triggers.Resolve(saved, TriggerKind.Emote).Enabled);
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
    public void WordTriggersAllowEveryChannelButStandardEmotes(TriggerKind kind)
    {
        Assert.Equal(Enum.GetValues<ChatChannel>().Except([ChatChannel.StandardEmote, ChatChannel.CrossParty, ChatChannel.PvPTeam]), Triggers.AllowedChannels(kind));
    }

    [Fact]
    public void ChannelsUsableByAllTriggersComeFirstThenTheRestInOrder()
    {
        ChatChannel[] first = [ChatChannel.Say, ChatChannel.Party, ChatChannel.Alliance, ChatChannel.CustomEmote];
        Assert.Equal(
            [.. first, .. Enum.GetValues<ChatChannel>().Except([.. first, ChatChannel.StandardEmote, ChatChannel.CrossParty, ChatChannel.PvPTeam])],
            Triggers.ChannelsByUsage);
    }

    [Fact]
    public void OnlyTargetSpeaksIsOffByDefault()
    {
        Assert.Equal(
            [TriggerKind.TargetSpeaks],
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
        Assert.Equal([ChatChannel.Say, ChatChannel.Party, ChatChannel.CustomEmote], Triggers.Default(TriggerKind.You).Channels.Order());
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
        Assert.True(settings.Enabled);
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

    [Fact]
    public void ResolveWidensAnOlderTargetSpeaksToEveryChannel()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.TargetSpeaks] = new() { Enabled = true, Channels = [ChatChannel.Say, ChatChannel.StandardEmote] },
        };

        var settings = Triggers.Resolve(saved, TriggerKind.TargetSpeaks);

        Assert.True(settings.Enabled);
        Assert.Equal(Triggers.AllowedChannels(TriggerKind.TargetSpeaks).Order(), settings.Channels.Order());
    }

    [Fact]
    public void CrossWorldPartyIsNotAChannelOfItsOwn()
    {
        Assert.DoesNotContain(ChatChannel.CrossParty, Triggers.ChannelsByUsage);
        Assert.All(Triggers.All, k => Assert.DoesNotContain(ChatChannel.CrossParty, Triggers.Default(k).Channels));
    }

    [Fact]
    public void ResolveDropsCrossWorldPartySavedByOlderVersions()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Name] = new() { Enabled = true, Channels = [ChatChannel.Party, ChatChannel.CrossParty, ChatChannel.Alliance] },
        };

        Assert.Equal([ChatChannel.Party, ChatChannel.Alliance], Triggers.Resolve(saved, TriggerKind.Name).Channels.Order());
    }

    [Fact]
    public void PvPTeamIsNotAChannel()
    {
        Assert.DoesNotContain(ChatChannel.PvPTeam, Triggers.ChannelsByUsage);
        Assert.All(Triggers.All, k => Assert.DoesNotContain(ChatChannel.PvPTeam, Triggers.AllowedChannels(k)));
        Assert.All(Triggers.All, k => Assert.DoesNotContain(ChatChannel.PvPTeam, Triggers.Default(k).Channels));
    }

    [Fact]
    public void ResolveDropsPvPTeamSavedByOlderVersions()
    {
        var saved = new Dictionary<TriggerKind, TriggerSettings>
        {
            [TriggerKind.Name] = new() { Enabled = true, Channels = [ChatChannel.Say, ChatChannel.PvPTeam, ChatChannel.NoviceNetwork] },
            [TriggerKind.TargetSpeaks] = new() { Enabled = true, Channels = [ChatChannel.PvPTeam] },
        };

        Assert.Equal([ChatChannel.Say, ChatChannel.NoviceNetwork], Triggers.Resolve(saved, TriggerKind.Name).Channels.Order());
        Assert.DoesNotContain(ChatChannel.PvPTeam, Triggers.Resolve(saved, TriggerKind.TargetSpeaks).Channels);
    }

    [Fact]
    public void LaterChannelsKeepTheirSavedNumbers()
    {
        Assert.Equal(8, (int)ChatChannel.NoviceNetwork);
        Assert.Equal(26, (int)ChatChannel.CustomEmote);
    }
}
