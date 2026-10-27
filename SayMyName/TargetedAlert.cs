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
/// emote) while targeting you, on a channel the Targeted trigger listens on.
/// </summary>
public sealed unsafe class TargetedAlert : IDisposable
{
    private delegate bool PlayEmoteDelegate(EmoteController* controller, uint emoteId, EmoteController.PlayEmoteOption* options);

    private readonly Configuration configuration;
    private readonly AlertSound sound;

    // Sees every other player's emote, including those whose log message is turned off and never
    // reach chat. Null if the game function wasn't found; emotes then come from chat instead.
    private readonly Hook<PlayEmoteDelegate>? playEmoteHook;

    public TargetedAlert(Configuration configuration, AlertSound sound)
    {
        this.configuration = configuration;
        this.sound = sound;

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

        // Unhandled only: a message another plugin has hidden shouldn't alert
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessage;
    }

    public void Dispose()
    {
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessage;
        playEmoteHook?.Dispose();
    }

    private bool Listening(ChatChannel channel)
    {
        var settings = configuration.Triggers[TriggerKind.Targeted];
        return settings.Enabled && configuration.PlaySound && settings.Channels.Contains(channel);
    }

    private bool OnPlayEmote(EmoteController* controller, uint emoteId, EmoteController.PlayEmoteOption* options)
    {
        var result = playEmoteHook!.Original(controller, emoteId, options);

        // An exception must not escape into the game
        try
        {
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

    // Finds the sender among nearby players (a few hundred at most); only reached for lines with a 'you' word
    private static bool SenderTargetsYou(IChatMessage message)
    {
        if (Plugin.ObjectTable.LocalPlayer is not { } me)
        {
            return false;
        }

        var link = ChatLines.SenderLink(message);
        var name = link?.PlayerName ?? ChatSender.Name(message.Sender.TextValue).ToString();
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
