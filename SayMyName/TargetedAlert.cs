using System;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Plays the alert sound when another player uses a standard emote on you (Emote trigger), or says
/// a 'you' word while targeting you on a channel the You trigger listens on (Say, Party, Alliance, custom emotes);
/// the 'you' words are also coloured in chat, and the inactive chat tabs showing the line are coloured.
/// Also plays the sound, and only the sound, when your current target speaks on any channel but
/// tells (Target Speaks trigger), or optionally only while your target is targeting you.
/// Emotes come from chat only: one whose log message the emoter turned off (motion only) never alerts,
/// and one that is logged alerts only if a chat tab shows it, so you can see who set it off.
/// </summary>
public sealed class TargetedAlert : IDisposable
{
    // An emote line the tabs haven't shown within this long came too late to alert for
    private const long EmoteShownTimeoutMs = 1000;

    private readonly Configuration configuration;
    private readonly AlertSound sound;
    private readonly ChatHighlighter highlighter;
    private readonly TabHighlighter tabs;

    /// <summary>Dev plugin only: treats a sender with this name as targeting you, for injected lines.</summary>
    public Predicate<string>? FakeTargetingYou { get; set; }

    /// <summary>Dev plugin only: treats a sender with this name as your current target, for injected lines.</summary>
    public Predicate<string>? FakeYourTarget { get; set; }

    public TargetedAlert(Configuration configuration, AlertSound sound, ChatHighlighter highlighter, TabHighlighter tabs)
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

    private bool Listening(TriggerKind kind, ChatChannel channel) => (configuration.PlaySound || tabs.Enabled) && TriggerListens(kind, channel);

    private bool TriggerListens(TriggerKind kind, ChatChannel channel) => Triggers.Listens(kind, configuration.Triggers[kind], channel);

    // The sound waits until a tab is known to show the emote's line
    private void AlertForEmote()
    {
        var since = Environment.TickCount64;
        tabs.Alert(shown =>
        {
            if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
            {
                Plugin.Log.Verbose($"Emote trigger: line shown={shown}");
            }
            // Only a hidden chat log holds up the tabs' answer; a sound that late wouldn't match anything on screen
            if (shown && configuration.PlaySound && Environment.TickCount64 - since <= EmoteShownTimeoutMs)
            {
                sound.Alert();
            }
        });
    }

    private void OnChatMessage(IChatMessage message)
    {
        if (ChatChannels.FromChatType(message.LogKind) is not { } channel)
        {
            return;
        }

        // Emote and You listen on channels the other can't use, so the channel says which of them this is
        var emote = channel == ChatChannel.StandardEmote;
        var kind = emote ? TriggerKind.Emote : TriggerKind.You;
        var targeted = Listening(kind, channel);
        // Sound only: a target speaking colours no words and no tabs
        var targetSpeaks = configuration.PlaySound && TriggerListens(TriggerKind.TargetSpeaks, channel);
        if (!targeted && !targetSpeaks)
        {
            return;
        }

        var fromYou = ChatLines.IsFromYou(message, Plugin.PlayerState.CharacterName);
        var text = message.Message.TextValue;
        // A standard emote is used on the emoter's target; the game leaves the line's target kind unset
        var matched = targeted && !fromYou && (emote || YouWords.IsMatch(text)) && SenderTargetsYou(message);
        var targetSpoke = targetSpeaks && !fromYou && SenderIsYourTarget(message, configuration.TargetSpeaksOnlyTargetingYou);

        if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
        {
            Plugin.Log.Verbose($"{kind}/TargetSpeaks trigger: {channel} source={message.SourceKind} target={message.TargetKind} " +
                $"sender={ChatLines.Escape(message.Sender.TextValue)} fromYou={fromYou} matched={matched} targetSpoke={targetSpoke} text={ChatLines.Escape(text)}");
        }

        if (matched && emote)
        {
            AlertForEmote();
            return;
        }
        if (matched || targetSpoke)
        {
            sound.Alert();
        }
        if (matched)
        {
            tabs.Alert();
        }
    }

    // Colours the 'you' words of a line from someone targeting you, or of an emote at you
    private void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        if (!highlighter.Enabled
            || ChatChannels.FromChatType(message.LogKind) is not { } channel)
        {
            return;
        }

        var emote = channel == ChatChannel.StandardEmote;
        if (!TriggerListens(emote ? TriggerKind.Emote : TriggerKind.You, channel)
            || ChatLines.IsFromYou(message, Plugin.PlayerState.CharacterName))
        {
            return;
        }

        var text = highlighter.Begin(message.Message);
        YouWords.FindAll(text, highlighter.Matches);
        // Checked last: it searches the nearby players
        if (highlighter.Matches.Count > 0 && SenderTargetsYou(message))
        {
            highlighter.Apply(message);
        }
    }

    // Finds the sender among nearby players (a few hundred at most); only reached for standard emotes and lines with a 'you' word
    private bool SenderTargetsYou(IChatMessage message)
    {
        var link = ChatLines.SenderLink(message);
        var name = link?.PlayerName ?? ChatSender.Name(ChatLines.OriginalSenderText(message)).ToString();
        if (FakeTargetingYou?.Invoke(name) == true)
        {
            return true;
        }

        if (Plugin.ObjectTable.LocalPlayer is not { } me)
        {
            return false;
        }

        uint? homeWorld = link?.World.RowId;
        foreach (var player in Plugin.ObjectTable.PlayerObjects)
        {
            if (player is IPlayerCharacter character
                && (homeWorld == null || character.HomeWorld.RowId == homeWorld)
                && character.Name.TextValue == name)
            {
                return character.TargetObjectId == me.GameObjectId;
            }
        }
        return false;
    }

    // Compares the sender with your one target, so unlike SenderTargetsYou no search of nearby players is needed;
    // with targetingYou, your target must also be targeting you
    private bool SenderIsYourTarget(IChatMessage message, bool targetingYou)
    {
        var link = ChatLines.SenderLink(message);
        var name = link?.PlayerName ?? ChatSender.Name(ChatLines.OriginalSenderText(message)).ToString();
        if (FakeYourTarget?.Invoke(name) == true)
        {
            return !targetingYou || FakeTargetingYou?.Invoke(name) == true;
        }

        return Plugin.ObjectTable.LocalPlayer is { } me
            && me.TargetObject is IPlayerCharacter target
            && (link == null || target.HomeWorld.RowId == link.World.RowId)
            && target.Name.TextValue == name
            && (!targetingYou || target.TargetObjectId == me.GameObjectId);
    }
}
