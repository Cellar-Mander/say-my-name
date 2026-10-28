using System;
using Dalamud.Game.Chat;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Alerts when another player says your name (Name trigger) or one of your custom words or
/// phrases (Custom trigger), on a channel that trigger listens on: plays the alert sound, colours
/// the matched words in the chat line, and colours the inactive chat tabs that show it.
/// </summary>
public sealed class TextAlert : IDisposable
{
    private readonly Configuration configuration;
    private readonly AlertSound sound;
    private readonly ChatHighlighter highlighter;
    private readonly TabHighlighter tabs;
    private NameMatcher nameMatcher = new("", caseSensitive: true);
    private NameMatcher customMatcher = NameMatcher.ForPhrases("", caseSensitive: false);

    public TextAlert(Configuration configuration, AlertSound sound, ChatHighlighter highlighter, TabHighlighter tabs)
    {
        this.configuration = configuration;
        this.sound = sound;
        this.highlighter = highlighter;
        this.tabs = tabs;
        // Dalamud's place for formatting a line; skipped for a line another plugin has already hidden
        Plugin.ChatGui.CheckMessageHandled += OnCheckMessageHandled;
        // Unhandled only: a message another plugin has hidden shouldn't alert
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.CheckMessageHandled -= OnCheckMessageHandled;
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessage;
    }

    private void OnChatMessage(IChatMessage message)
    {
        if (!(configuration.PlaySound || tabs.Enabled) || !Listening(message, out var channel, out var name, out var custom, out var you, out var fromYou))
        {
            return;
        }

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
            tabs.Alert();
        }
    }

    private void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        if (!highlighter.Enabled
            || !Listening(message, out _, out var name, out var custom, out var you, out var fromYou)
            || fromYou)
        {
            return;
        }

        var text = highlighter.Begin(message.Message);
        if (name)
        {
            CurrentNameMatcher(you).FindAll(text, highlighter.Matches);
        }
        if (custom)
        {
            CurrentCustomMatcher().FindAll(text, highlighter.Matches);
        }
        highlighter.Apply(message);
    }

    // Whether either text trigger listens on this line's channel, and which
    private bool Listening(IChatMessage message, out ChatChannel channel, out bool name, out bool custom, out string you, out bool fromYou)
    {
        name = custom = fromYou = false;
        you = "";
        if (ChatChannels.FromChatType(message.LogKind) is not { } c)
        {
            channel = default;
            return false;
        }

        channel = c;
        name = Listening(TriggerKind.Name, channel);
        custom = Listening(TriggerKind.Custom, channel);
        if (!name && !custom)
        {
            return false;
        }

        you = Plugin.PlayerState.CharacterName;
        fromYou = ChatLines.IsFromYou(message, you);
        return true;
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
