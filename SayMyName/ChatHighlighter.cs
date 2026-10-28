using System.Collections.Generic;
using System.Text;
using Dalamud.Game.Chat;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SayMyName.Core;

namespace SayMyName;

/// <summary>
/// Colours the words a trigger matched in a chat line, for the triggers to call from
/// CheckMessageHandled. Call <see cref="Begin"/>, add matches to <see cref="Matches"/>, then <see cref="Apply"/>.
/// </summary>
public sealed class ChatHighlighter(Configuration configuration)
{
    // Reused for every line; chat events all arrive on the framework thread
    private readonly StringBuilder plainText = new();
    private readonly List<(int Start, int Length, bool Highlighted)> pieces = [];

    /// <summary>Matches in the text <see cref="Begin"/> returned, in any order, overlapping or not.</summary>
    public List<(int Start, int Length)> Matches { get; } = [];

    /// <summary>Whether highlighting is on and would change anything.</summary>
    public bool Enabled => configuration.Highlight
        && (configuration.HighlightTextColor != Configuration.NoColor || configuration.HighlightGlowColor != Configuration.NoColor);

    /// <summary>
    /// Clears <see cref="Matches"/> and returns the line's text to match against. Built from the
    /// payloads' own text, so match positions line up with the payloads in <see cref="Apply"/>.
    /// </summary>
    public string Begin(SeString message)
    {
        Matches.Clear();
        plainText.Clear();
        foreach (var payload in message.Payloads)
        {
            if (payload is ITextProvider provider)
            {
                plainText.Append(provider.Text);
            }
        }
        return plainText.ToString();
    }

    /// <summary>Colours the <see cref="Matches"/> in the chat line, if there are any.</summary>
    public void Apply(IHandleableChatMessage message)
    {
        if (Matches.Count == 0)
        {
            return;
        }

        message.Message = Highlight(message.Message);
    }

    /// <summary>
    /// A copy of the line with the <see cref="Matches"/> coloured: each text payload is split around
    /// them, and other payloads (links, icons, auto-translate) are kept whole.
    /// </summary>
    public SeString Highlight(SeString original)
    {
        Highlights.Merge(Matches);
        var payloads = new List<Payload>(original.Payloads.Count + Matches.Count * 6);
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
                Highlights.Split(offset, length, Matches, pieces);
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
}
