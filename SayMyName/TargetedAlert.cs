using System;
using Dalamud.Game.Chat;
using Dalamud.Game.ClientState.Objects.SubKinds;
using Dalamud.Game.Text;
using Dalamud.Hooking;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Plays the alert sound when another player emotes at you, or says a 'you' word (Say or custom
/// emote) while targeting you, on a channel the Targeted trigger listens on; the 'you' words are
/// also coloured in chat.
/// </summary>
public sealed unsafe class TargetedAlert : IDisposable
{
    private delegate bool PlayEmoteDelegate(EmoteController* controller, uint emoteId, EmoteController.PlayEmoteOption* options);

    private readonly Configuration configuration;
    private readonly AlertSound sound;
    private readonly ChatHighlighter highlighter;

    // Sees every other player's emote, including those whose log message is turned off and never
    // reach chat. Null if the game function wasn't found; emotes then come from chat instead.
    private readonly Hook<PlayEmoteDelegate>? playEmoteHook;

    /// <summary>Dev plugin only: told of every emote the hook sees (emoting character's address, emote, target id, logged).</summary>
    public Action<nint, uint, ulong, bool>? EmotePlayed { get; set; }

    /// <summary>Dev plugin only: treats a sender with this name as targeting you, for injected lines.</summary>
    public Predicate<string>? FakeTargetingYou { get; set; }

    public TargetedAlert(Configuration configuration, AlertSound sound, ChatHighlighter highlighter)
    {
        this.configuration = configuration;
        this.sound = sound;
        this.highlighter = highlighter;

        var playEmote = (nint)EmoteController.MemberFunctionPointers.PlayEmote;
        if (playEmote == 0)
        {
            Plugin.Log.Warning("Targeted trigger: emote function not found; only emotes in the chat log will alert");
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

    private bool Listening(ChatChannel channel) => configuration.PlaySound && TriggerListens(channel);

    private bool TriggerListens(ChatChannel channel)
    {
        var settings = configuration.Triggers[TriggerKind.Targeted];
        return settings.Enabled && settings.Channels.Contains(channel);
    }

    private bool OnPlayEmote(EmoteController* controller, uint emoteId, EmoteController.PlayEmoteOption* options)
    {
        var result = playEmoteHook!.Original(controller, emoteId, options);

        // An exception must not escape into the game
        try
        {
            if (options != null)
            {
                EmotePlayed?.Invoke((nint)controller->OwnerObject, emoteId, options->TargetId, !options->DisableLogMessage);
            }

            if (options != null
                && Listening(ChatChannel.StandardEmote)
                && Plugin.ObjectTable.LocalPlayer is { } me
                && options->TargetId == me.GameObjectId
                && (nint)controller->OwnerObject != me.Address)
            {
                if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
                {
                    Plugin.Log.Verbose($"Targeted trigger: emote {emoteId} at you, logged={!options->DisableLogMessage}");
                }
                sound.Alert();
            }
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Targeted trigger: emote check failed");
        }

        return result;
    }

    private void OnChatMessage(IChatMessage message)
    {
        if (ChatChannels.FromChatType(message.LogKind) is not { } channel
            || !Listening(channel)
            // The hook already alerted for this emote, logged or not
            || (channel == ChatChannel.StandardEmote && playEmoteHook != null))
        {
            return;
        }

        var fromYou = ChatLines.IsFromYou(message, Plugin.PlayerState.CharacterName);
        var text = message.Message.TextValue;
        var matched = !fromYou && (channel == ChatChannel.StandardEmote
            ? message.TargetKind == XivChatRelationKind.LocalPlayer
            : YouWords.IsMatch(text) && SenderTargetsYou(message));

        if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
        {
            Plugin.Log.Verbose($"Targeted trigger: {channel} source={message.SourceKind} target={message.TargetKind} " +
                $"sender={ChatLines.Escape(message.Sender.TextValue)} fromYou={fromYou} matched={matched} text={ChatLines.Escape(text)}");
        }

        if (matched)
        {
            sound.Alert();
        }
    }

    // Colours the 'you' words; emotes at you have no word to colour
    private void OnCheckMessageHandled(IHandleableChatMessage message)
    {
        if (!highlighter.Enabled
            || ChatChannels.FromChatType(message.LogKind) is not { } channel
            || channel == ChatChannel.StandardEmote
            || !TriggerListens(channel)
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
}
