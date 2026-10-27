using System;
using System.Collections.Generic;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using SayMyName.Core;

namespace SayMyName;

/// <summary>
/// Dev-plugin helper for the smoke checklist (docs/smoke-test.md): puts fake player messages into
/// chat and logs when each comes back through the chat events the triggers listen on.
/// </summary>
public sealed class TestHarness : IDisposable
{
    public const string Usage = "inject <channel> <First> <Last> <message>";

    // Texts injected but not yet seen; a handful at most, only touched on the framework thread
    private readonly HashSet<string> pending = [];

    public TestHarness()
    {
        Plugin.ChatGui.ChatMessage += OnChatMessage;
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessageUnhandled;
    }

    public void Dispose()
    {
        Plugin.ChatGui.ChatMessage -= OnChatMessage;
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessageUnhandled;
    }

    /// <summary>Queues "&lt;channel&gt; &lt;First&gt; &lt;Last&gt; &lt;message&gt;" as a chat line; false if malformed.</summary>
    public bool Inject(string args)
    {
        var parts = args.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4
            || !Enum.TryParse<ChatChannel>(parts[0], ignoreCase: true, out var channel)
            || !Enum.IsDefined(channel))
        {
            return false;
        }

        var name = $"{parts[1]} {parts[2]}";
        var text = parts[3];
        pending.Add(text);

        // Other players' lines carry a player link, so give the fake sender one on your world.
        // PlayerPayload encodes the name text and link terminator itself; adding them again doubles the name.
        Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = ChatChannels.ToChatType(channel),
            Name = new SeString(new PlayerPayload(name, Plugin.PlayerState.CurrentWorld.RowId)),
            Message = text,
        });
        Plugin.Log.Information($"Test harness: injected {channel} from {name}: {text}");
        return true;
    }

    private void OnChatMessage(IHandleableChatMessage message)
    {
        if (pending.Contains(message.Message.TextValue))
        {
            Plugin.Log.Information($"Test harness: ChatMessage got {message.LogKind} from {message.Sender.TextValue}: {message.Message.TextValue}");
        }
    }

    private void OnChatMessageUnhandled(IChatMessage message)
    {
        if (pending.Remove(message.Message.TextValue))
        {
            Plugin.Log.Information($"Test harness: ChatMessageUnhandled got {message.LogKind} (source={message.SourceKind}) from {message.Sender.TextValue}");
        }
    }
}
