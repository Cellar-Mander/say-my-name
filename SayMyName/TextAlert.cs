using System;
using Dalamud.Game.Chat;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Plays the alert sound when another player says your name (Name trigger) or one of your custom
/// words or phrases (Custom trigger), on a channel that trigger listens on.
/// </summary>
public sealed class TextAlert : IDisposable
{
    private readonly Configuration configuration;
    private readonly AlertSound sound;
    private NameMatcher nameMatcher = new("", caseSensitive: true);
    private NameMatcher customMatcher = NameMatcher.ForPhrases("", caseSensitive: false);

    public TextAlert(Configuration configuration, AlertSound sound)
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
        if (!configuration.PlaySound || ChatChannels.FromChatType(message.LogKind) is not { } channel)
        {
            return;
        }

        var name = Listening(TriggerKind.Name, channel);
        var custom = Listening(TriggerKind.Custom, channel);
        if (!name && !custom)
        {
            return;
        }

        var you = Plugin.PlayerState.CharacterName;
        var fromYou = ChatLines.IsFromYou(message, you);
        var text = message.Message.TextValue;
        var nameMatched = name && !fromYou && CurrentNameMatcher(you).IsMatch(text);
        var customMatched = custom && !fromYou && !nameMatched && CurrentCustomMatcher().IsMatch(text);

        if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
        {
            Plugin.Log.Verbose($"Name/Custom trigger: {channel} source={message.SourceKind} sender={ChatLines.Escape(message.Sender.TextValue)} " +
                $"fromYou={fromYou} name={nameMatched} custom={customMatched} text={ChatLines.Escape(text)}");
        }

        if (nameMatched || customMatched)
        {
            sound.Alert();
        }
    }

    private bool Listening(TriggerKind kind, ChatChannel channel)
    {
        var settings = configuration.Triggers[kind];
        return settings.Enabled && settings.Channels.Contains(channel);
    }

    // The matchers are rebuilt only when their input or case setting changes, not per message
    private NameMatcher CurrentNameMatcher(string name)
    {
        if (nameMatcher.Source != name || nameMatcher.CaseSensitive != configuration.NameCaseSensitive)
        {
            nameMatcher = new NameMatcher(name, configuration.NameCaseSensitive);
        }
        return nameMatcher;
    }

    // By reference: every edit in the settings window stores a new string, and comparing contents would scan it per message
    private NameMatcher CurrentCustomMatcher()
    {
        if (!ReferenceEquals(customMatcher.Source, configuration.CustomWords)
            || customMatcher.CaseSensitive != configuration.CustomWordsCaseSensitive)
        {
            customMatcher = NameMatcher.ForPhrases(configuration.CustomWords, configuration.CustomWordsCaseSensitive);
        }
        return customMatcher;
    }
}
