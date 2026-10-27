using System;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using FFXIVClientStructs.FFXIV.Client.UI;
using SayMyName.Core;

namespace SayMyName;

/// <summary>Plays the alert sound when another player says your name on a channel the Name trigger listens on.</summary>
public sealed class NameAlert : IDisposable
{
    private readonly Configuration configuration;
    private readonly Cooldown cooldown = new();
    private NameMatcher matcher = new("", caseSensitive: true);

    public NameAlert(Configuration configuration)
    {
        this.configuration = configuration;
        // Unhandled only: a message another plugin has hidden shouldn't alert
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessage;
    }

    public void PlaySound() => UIGlobals.PlayChatSoundEffect((uint)configuration.SoundEffect);

    private void OnChatMessage(IChatMessage message)
    {
        var settings = configuration.Triggers[TriggerKind.Name];
        if (!settings.Enabled
            || !configuration.PlaySound
            || message.SourceKind == XivChatRelationKind.LocalPlayer
            || ToChannel(message.LogKind) is not { } channel
            || !settings.Channels.Contains(channel)
            || !CurrentMatcher().IsMatch(message.Message.TextValue))
        {
            return;
        }

        if (cooldown.TryFire(Environment.TickCount64, TimeSpan.FromSeconds(configuration.CooldownSeconds)))
        {
            PlaySound();
        }
    }

    // Rebuilt only when the character or the case setting changes, not per message
    private NameMatcher CurrentMatcher()
    {
        var name = Plugin.PlayerState.CharacterName;
        if (matcher.FullName != name || matcher.CaseSensitive != configuration.NameCaseSensitive)
        {
            matcher = new NameMatcher(name, configuration.NameCaseSensitive);
        }
        return matcher;
    }

    private static ChatChannel? ToChannel(XivChatType type) => type switch
    {
        XivChatType.Say => ChatChannel.Say,
        XivChatType.Yell => ChatChannel.Yell,
        XivChatType.Shout => ChatChannel.Shout,
        XivChatType.Party => ChatChannel.Party,
        XivChatType.CrossParty => ChatChannel.CrossParty,
        XivChatType.Alliance => ChatChannel.Alliance,
        XivChatType.FreeCompany => ChatChannel.FreeCompany,
        XivChatType.PvPTeam => ChatChannel.PvPTeam,
        XivChatType.NoviceNetwork => ChatChannel.NoviceNetwork,
        >= XivChatType.Ls1 and <= XivChatType.Ls8 => ChatChannel.Linkshell1 + (type - XivChatType.Ls1),
        XivChatType.CrossLinkShell1 => ChatChannel.CrossLinkshell1,
        >= XivChatType.CrossLinkShell2 and <= XivChatType.CrossLinkShell8 => ChatChannel.CrossLinkshell2 + (type - XivChatType.CrossLinkShell2),
        XivChatType.StandardEmote => ChatChannel.StandardEmote,
        XivChatType.CustomEmote => ChatChannel.CustomEmote,
        _ => null,
    };
}
