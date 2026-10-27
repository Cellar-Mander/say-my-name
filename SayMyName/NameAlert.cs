using System;
using Dalamud.Game.Chat;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>Plays the alert sound when another player says your name on a channel the Name trigger listens on.</summary>
public sealed class NameAlert : IDisposable
{
    private readonly Configuration configuration;
    private readonly AlertSound sound;
    private NameMatcher matcher = new("", caseSensitive: true);

    public NameAlert(Configuration configuration, AlertSound sound)
    {
        this.configuration = configuration;
        this.sound = sound;
        // Unhandled only: a message another plugin has hidden shouldn't alert
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessage;
    }

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
        var fromYou = ChatLines.IsFromYou(message, you);
        var matched = !fromYou && CurrentMatcher(you).IsMatch(message.Message.TextValue);

        if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
        {
            Plugin.Log.Verbose($"Name trigger: {channel} source={message.SourceKind} sender={ChatLines.Escape(message.Sender.TextValue)} " +
                $"fromYou={fromYou} matched={matched} text={ChatLines.Escape(message.Message.TextValue)}");
        }

        if (matched)
        {
            sound.Alert();
        }
    }

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
