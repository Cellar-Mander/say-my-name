using System;
using System.Collections.Generic;
using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Alerts when another player says your name (Name trigger) or one of your custom words or
/// phrases (Custom trigger), on a channel that trigger listens on: plays the alert sound, and
/// colours the matched words in the chat line.
/// </summary>
public sealed class TextAlert : IDisposable
{
    private readonly Configuration configuration;
    private readonly AlertSound sound;
    private NameMatcher nameMatcher = new("", caseSensitive: true);
    private NameMatcher customMatcher = NameMatcher.ForPhrases("", caseSensitive: false);

    // Reused for every highlighted line; chat events all arrive on the framework thread
    private readonly StringBuilder plainText = new();
    private readonly List<(int Start, int Length)> matches = [];
    private readonly List<(int Start, int Length, bool Highlighted)> pieces = [];

    public TextAlert(Configuration configuration, AlertSound sound)
    {
        this.configuration = configuration;
        this.sound = sound;
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
        if (!configuration.PlaySound || !Listening(message, out var channel, out var name, out var custom, out var you, out var fromYou))
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
        }
    }

    private void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        if (!configuration.Highlight
            || (configuration.HighlightTextColor == Configuration.NoColor && configuration.HighlightGlowColor == Configuration.NoColor)
            || !Listening(message, out _, out var name, out var custom, out var you, out var fromYou)
            || fromYou)
        {
            return;
        }

        // Matched against the payloads' own text, so match positions line up with the payloads below
        var original = message.Message;
        plainText.Clear();
        foreach (var payload in original.Payloads)
        {
            if (payload is ITextProvider provider)
            {
                plainText.Append(provider.Text);
            }
        }
        var text = plainText.ToString();

        matches.Clear();
        if (name)
        {
            CurrentNameMatcher(you).FindAll(text, matches);
        }
        if (custom)
        {
            CurrentCustomMatcher().FindAll(text, matches);
        }
        if (matches.Count == 0)
        {
            return;
        }

        Highlights.Merge(matches);
        message.Message = Highlighted(original);
    }

    // Splits each text payload around the matches; other payloads (links, icons, auto-translate) are kept whole
    private SeString Highlighted(SeString original)
    {
        var payloads = new List<Payload>(original.Payloads.Count + matches.Count * 6);
        var offset = 0;
        foreach (var payload in original.Payloads)
        {
            if (payload is not ITextProvider provider)
            {
                payloads.Add(payload);
                continue;
            }

            var length = provider.Text?.Length ?? 0;
            var segment = (payload as TextPayload)?.Text;
            pieces.Clear();
            if (segment != null)
            {
                Highlights.Split(offset, length, matches, pieces);
            }

            if (pieces.Count == 0 || (pieces.Count == 1 && !pieces[0].Highlighted))
            {
                payloads.Add(payload);
            }
            else
            {
                foreach (var piece in pieces)
                {
                    var part = new TextPayload(segment!.Substring(piece.Start, piece.Length));
                    if (piece.Highlighted)
                    {
                        AddHighlighted(payloads, part);
                    }
                    else
                    {
                        payloads.Add(part);
                    }
                }
            }
            offset += length;
        }
        return new SeString(payloads);
    }

    private void AddHighlighted(List<Payload> payloads, TextPayload part)
    {
        var textColor = configuration.HighlightTextColor;
        var glowColor = configuration.HighlightGlowColor;
        if (textColor != Configuration.NoColor)
        {
            payloads.Add(new UIForegroundPayload(textColor));
        }
        if (glowColor != Configuration.NoColor)
        {
            payloads.Add(new UIGlowPayload(glowColor));
        }
        payloads.Add(part);
        // Off restores whatever colour the surrounding text had, e.g. inside a party finder link
        if (glowColor != Configuration.NoColor)
        {
            payloads.Add(UIGlowPayload.UIGlowOff);
        }
        if (textColor != Configuration.NoColor)
        {
            payloads.Add(UIForegroundPayload.UIForegroundOff);
        }
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
