using System;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using FFXIVClientStructs.FFXIV.Client.UI;
using SayMyName.Core;
using Serilog.Events;

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
            || ChatChannels.FromChatType(message.LogKind) is not { } channel
            || !settings.Channels.Contains(channel))
        {
            return;
        }

        var you = Plugin.PlayerState.CharacterName;
        var fromYou = IsFromYou(message, you);
        var matched = !fromYou && CurrentMatcher(you).IsMatch(message.Message.TextValue);

        if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
        {
            Plugin.Log.Verbose($"Name trigger: {channel} source={message.SourceKind} sender={Escape(message.Sender.TextValue)} " +
                $"fromYou={fromYou} matched={matched} text={Escape(message.Message.TextValue)}");
        }

        if (matched && cooldown.TryFire(Environment.TickCount64, TimeSpan.FromSeconds(configuration.CooldownSeconds)))
        {
            PlaySound();
        }
    }

    // The game doesn't always mark your own lines as from the local player (seen in Say), so the
    // sender's name is checked too: a player link if the game added one, else the plain sender text
    private static bool IsFromYou(IChatMessage message, string you)
    {
        if (message.SourceKind == XivChatRelationKind.LocalPlayer)
        {
            return true;
        }

        var player = message.Sender.Payloads.OfType<PlayerPayload>().FirstOrDefault();
        return player != null
            ? you.Length > 0 && player.PlayerName == you
            : ChatSender.Is(message.Sender.TextValue, you);
    }

    // Shows apostrophe variants and the game's private-use glyphs in the log
    private static string Escape(string text) =>
        string.Concat(text.Select(c => c is >= ' ' and <= '~' ? c.ToString() : $"\\u{(int)c:X4}"));

    // Rebuilt only when the character or the case setting changes, not per message
    private NameMatcher CurrentMatcher(string name)
    {
        if (matcher.FullName != name || matcher.CaseSensitive != configuration.NameCaseSensitive)
        {
            matcher = new NameMatcher(name, configuration.NameCaseSensitive);
        }
        return matcher;
    }
}
