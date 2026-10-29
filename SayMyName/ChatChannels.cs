using System;
using Dalamud.Game.Text;
using SayMyName.Core;

namespace SayMyName;

/// <summary>Maps between the game's chat types and the channels triggers listen on.</summary>
public static class ChatChannels
{
    public static ChatChannel? FromChatType(XivChatType type) => type switch
    {
        XivChatType.Say => ChatChannel.Say,
        XivChatType.Yell => ChatChannel.Yell,
        XivChatType.Shout => ChatChannel.Shout,
        XivChatType.Party or XivChatType.CrossParty => ChatChannel.Party,
        XivChatType.Alliance => ChatChannel.Alliance,
        XivChatType.FreeCompany => ChatChannel.FreeCompany,
        XivChatType.NoviceNetwork => ChatChannel.NoviceNetwork,
        >= XivChatType.Ls1 and <= XivChatType.Ls8 => ChatChannel.Linkshell1 + (type - XivChatType.Ls1),
        XivChatType.CrossLinkShell1 => ChatChannel.CrossLinkshell1,
        >= XivChatType.CrossLinkShell2 and <= XivChatType.CrossLinkShell8 => ChatChannel.CrossLinkshell2 + (type - XivChatType.CrossLinkShell2),
        XivChatType.StandardEmote => ChatChannel.StandardEmote,
        XivChatType.CustomEmote => ChatChannel.CustomEmote,
        _ => null,
    };

    public static XivChatType ToChatType(ChatChannel channel) => channel switch
    {
        ChatChannel.Say => XivChatType.Say,
        ChatChannel.Yell => XivChatType.Yell,
        ChatChannel.Shout => XivChatType.Shout,
        ChatChannel.Party => XivChatType.Party,
        ChatChannel.CrossParty => XivChatType.CrossParty,
        ChatChannel.Alliance => XivChatType.Alliance,
        ChatChannel.FreeCompany => XivChatType.FreeCompany,
        ChatChannel.PvPTeam => XivChatType.PvPTeam,
        ChatChannel.NoviceNetwork => XivChatType.NoviceNetwork,
        >= ChatChannel.Linkshell1 and <= ChatChannel.Linkshell8 => (XivChatType)((int)XivChatType.Ls1 + (channel - ChatChannel.Linkshell1)),
        ChatChannel.CrossLinkshell1 => XivChatType.CrossLinkShell1,
        >= ChatChannel.CrossLinkshell2 and <= ChatChannel.CrossLinkshell8 => (XivChatType)((int)XivChatType.CrossLinkShell2 + (channel - ChatChannel.CrossLinkshell2)),
        ChatChannel.StandardEmote => XivChatType.StandardEmote,
        ChatChannel.CustomEmote => XivChatType.CustomEmote,
        _ => throw new ArgumentOutOfRangeException(nameof(channel), channel, null),
    };
}
