using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SayMyName.Core;

namespace SayMyName;

/// <summary>What the triggers need to know about a chat line's sender.</summary>
public static class ChatLines
{
    // The game doesn't always mark your own lines as from the local player (seen in Say and Party), so the
    // sender's name is checked too: a player link if the game added one, else the sender text as the game
    // sent it, since other plugins may tag or rewrite the shown name
    public static bool IsFromYou(IChatMessage message, string you)
    {
        if (message.SourceKind == XivChatRelationKind.LocalPlayer)
        {
            return true;
        }

        var player = SenderLink(message);
        return player != null
            ? you.Length > 0 && player.PlayerName == you
            : ChatSender.Is(OriginalSenderText(message), you);
    }

    public static string OriginalSenderText(IChatMessage message) => message.OriginalSender.ExtractText();

    public static PlayerPayload? SenderLink(IChatMessage message) =>
        message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();

    // Shows apostrophe variants and the game's private-use glyphs in the log
    public static string Escape(string text) =>
        string.Concat(text.Select(c => c is >= ' ' and <= '~' ? c.ToString() : $"\\u{(int)c:X4}"));
}
