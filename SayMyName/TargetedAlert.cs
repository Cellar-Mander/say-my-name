using System;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Plays the alert sound when another player uses a standard emote on you (Emote trigger), or says
/// a 'you' word while targeting you on a channel the You trigger listens on (Say, Party, Alliance, custom emotes);
/// the 'you' words are also coloured in chat, and the inactive chat tabs showing the line are coloured.
/// Also plays the sound, and only the sound, when your current target speaks on any channel but
/// tells (Target Speaks trigger), or optionally only while your target is targeting you.
/// An emote at you whose log message the emoter turned off (motion only) is written to chat anyway,
/// marked as the plugin's, so you can see who set off the alert; it alerts only if a chat tab shows it.
/// </summary>
public sealed unsafe class TargetedAlert : IDisposable
{
    /// <summary>Ends a line the plugin wrote to chat in place of an emote's missing log message.</summary>
    public const string InsertedTag = " [Say My Name]";

    // A line the game hasn't shown within this long after the emote, even in a tab, came too late to alert for
    private const long InsertTimeoutMs = 1000;

    private delegate bool PlayEmoteDelegate(EmoteController* controller, uint emoteId, EmoteController.PlayEmoteOption* options);

    private readonly Configuration configuration;
    private readonly AlertSound sound;
    private readonly ChatHighlighter highlighter;
    private readonly TabHighlighter tabs;

    // Sees every other player's emote, including those whose log message is turned off and never
    // reach chat. Null if the game function wasn't found; emotes then come from chat instead.
    private readonly Hook<PlayEmoteDelegate>? playEmoteHook;

    // The unlogged emote at you whose log message the hook turned back on, until its line arrives
    private string? insertSource;
    private long insertAt;

    /// <summary>Dev plugin only: told of every emote the hook sees (emoting character's address, emote, target id, logged).</summary>
    public Action<nint, uint, ulong, bool>? EmotePlayed { get; set; }

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

        var playEmote = (nint)EmoteController.MemberFunctionPointers.PlayEmote;
        if (playEmote == 0)
        {
            Plugin.Log.Warning("Emote trigger: emote function not found; only emotes in the chat log will alert");
        }
        else
        {
            playEmoteHook = Plugin.GameInterop.HookFromAddress<PlayEmoteDelegate>(playEmote, OnPlayEmote);
            playEmoteHook.Enable();
        }

        // Dalamud's place for formatting a line; skipped for a line another plugin has already hidden
        Plugin.ChatGui.CheckMessageHandled += OnCheckMessageHandled;
        // Unhandled only: a message another plugin has hidden shouldn't alert
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.CheckMessageHandled -= OnCheckMessageHandled;
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessage;
        playEmoteHook?.Dispose();
    }

    private bool Listening(TriggerKind kind, ChatChannel channel) => (configuration.PlaySound || tabs.Enabled) && TriggerListens(kind, channel);

    private bool TriggerListens(TriggerKind kind, ChatChannel channel) => Triggers.Listens(kind, configuration.Triggers[kind], channel);

    private bool OnPlayEmote(EmoteController* controller, uint emoteId, EmoteController.PlayEmoteOption* options)
    {
        var logged = options == null || !options->DisableLogMessage;
        var atYou = false;

        // An exception must not escape into the game
        try
        {
            atYou = options != null
                && Listening(TriggerKind.Emote, ChatChannel.StandardEmote)
                && Plugin.ObjectTable.LocalPlayer is { } me
                && options->TargetId == me.GameObjectId
                && (nint)controller->OwnerObject != me.Address;

            // Motion only: without a line in chat you couldn't tell who set off the alert, so the
            // game is asked to write its own line, which OnCheckMessageHandled marks as ours
            if (atYou && !logged)
            {
                insertSource = controller->OwnerObject->NameString;
                insertAt = Environment.TickCount64;
                options->DisableLogMessage = false;
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Emote trigger: emote check failed");
        }

        var result = playEmoteHook!.Original(controller, emoteId, options);

        try
        {
            if (atYou && !logged)
            {
                // Put back for anything else reading the options after us
                options->DisableLogMessage = true;
            }

            if (options != null)
            {
                EmotePlayed?.Invoke((nint)controller->OwnerObject, emoteId, options->TargetId, logged);
            }

            if (atYou)
            {
                if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
                {
                    Plugin.Log.Verbose($"Emote trigger: emote {emoteId} at you, logged={logged}");
                }
                // An unlogged emote alerts once its line is known to show in a tab
                if (logged)
                {
                    sound.Alert();
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Emote trigger: emote check failed");
        }

        return result;
    }

    // The game's line for the emote whose log message the hook turned back on
    private bool IsInsertedEmote(IChatMessage message) =>
        insertSource != null
        && message.LogKind == XivChatType.StandardEmote
        && Environment.TickCount64 - insertAt <= InsertTimeoutMs
        && message.Message.TextValue.Contains(insertSource, StringComparison.Ordinal);

    // Alerts if a tab shows the inserted line; if none logs standard emotes, it's tried once as a custom emote
    private void AlertForInserted(SeString line)
    {
        var since = Environment.TickCount64;
        tabs.Alert(landed =>
        {
            if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
            {
                Plugin.Log.Verbose($"Emote trigger: inserted standard emote line shown={landed}");
            }

            if (landed)
            {
                AlertIfRecent(since);
                return;
            }

            // Sender left empty: no trigger takes it for a player's line, see IsInsertedCustomEmote
            tabs.Alert(shown =>
            {
                if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
                {
                    Plugin.Log.Verbose($"Emote trigger: inserted custom emote line shown={shown}");
                }
                if (shown)
                {
                    AlertIfRecent(since);
                }
            });
            Plugin.ChatGui.Print(new XivChatEntry { Type = XivChatType.CustomEmote, Message = line });
        });
    }

    // Only a hidden chat log holds up the tabs' answer; a sound that late wouldn't match anything on screen
    private void AlertIfRecent(long since)
    {
        if (configuration.PlaySound && Environment.TickCount64 - since <= InsertTimeoutMs)
        {
            sound.Alert();
        }
    }

    /// <summary>
    /// Whether this is the custom emote line the plugin wrote for an unlogged emote: a player's
    /// custom emote always has a sender. Triggers skip it, as the emote has already alerted.
    /// </summary>
    public static bool IsInsertedCustomEmote(IChatMessage message) =>
        message.LogKind == XivChatType.CustomEmote && message.Sender.TextValue.Length == 0;

    private void OnChatMessage(IChatMessage message)
    {
        if (IsInsertedEmote(message))
        {
            insertSource = null;
            AlertForInserted(message.Message);
            return;
        }

        if (ChatChannels.FromChatType(message.LogKind) is not { } channel || IsInsertedCustomEmote(message))
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

        // The hook already played the sound for this emote, logged or not; only the tabs are left
        var soundPlayed = emote && playEmoteHook != null;
        targeted &= !soundPlayed || tabs.Enabled;

        var fromYou = ChatLines.IsFromYou(message, Plugin.PlayerState.CharacterName);
        var text = message.Message.TextValue;
        var matched = targeted && !fromYou && (emote
            ? message.TargetKind == XivChatRelationKind.LocalPlayer
            : YouWords.IsMatch(text) && SenderTargetsYou(message));
        var targetSpoke = targetSpeaks && !fromYou && SenderIsYourTarget(message, configuration.TargetSpeaksOnlyTargetingYou);

        if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
        {
            Plugin.Log.Verbose($"{kind}/TargetSpeaks trigger: {channel} source={message.SourceKind} target={message.TargetKind} " +
                $"sender={ChatLines.Escape(message.Sender.TextValue)} fromYou={fromYou} matched={matched} targetSpoke={targetSpoke} text={ChatLines.Escape(text)}");
        }

        if ((matched && !soundPlayed) || targetSpoke)
        {
            sound.Alert();
        }
        if (matched)
        {
            tabs.Alert();
        }
    }

    // Colours the 'you' words, and marks and colours the line written for an unlogged emote at you
    private void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        if (IsInsertedEmote(message))
        {
            MarkInserted(message);
            return;
        }

        if (!highlighter.Enabled
            || IsInsertedCustomEmote(message)
            || ChatChannels.FromChatType(message.LogKind) is not { } channel
            || !TriggerListens(TriggerKind.You, channel)
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

    // The 'you' words, or your name where the language has none (e.g. Japanese), are coloured
    private void MarkInserted(IHandleableChatMessage message)
    {
        var line = message.Message;
        if (highlighter.Enabled)
        {
            var text = highlighter.Begin(line);
            YouWords.FindAll(text, highlighter.Matches);
            var you = Plugin.PlayerState.CharacterName;
            for (var at = you.Length == 0 ? -1 : text.IndexOf(you, StringComparison.Ordinal); at >= 0; at = text.IndexOf(you, at + you.Length, StringComparison.Ordinal))
            {
                highlighter.Matches.Add((at, you.Length));
            }
            line = highlighter.Highlight(line);
        }
        message.Message = line.Append(new TextPayload(InsertedTag));
    }

    // Finds the sender among nearby players (a few hundred at most); only reached for lines with a 'you' word
    private bool SenderTargetsYou(IChatMessage message)
    {
        var link = ChatLines.SenderLink(message);
        var name = link?.PlayerName ?? ChatSender.Name(message.Sender.TextValue).ToString();
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
        var name = link?.PlayerName ?? ChatSender.Name(message.Sender.TextValue).ToString();
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
