using System;
using System.Collections.Generic;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using FFXIVClientStructs.FFXIV.Component.GUI;
using SayMyName.Core;

namespace SayMyName;

/// <summary>
/// Dev-plugin helper for the smoke checklist (docs/smoke-test.md): puts fake player messages into
/// chat and logs when each comes back through the chat events the triggers listen on, logs the
/// emotes the Targeted trigger sees, can pretend an injected sender is targeting you, and can
/// recolour a chat tab's name to see whether the game keeps the colour.
/// </summary>
public sealed unsafe class TestHarness : IDisposable
{
    public const string Usage = "inject <channel> <First> <Last> <message> | emotes | targeting-me [<First> <Last>] | tabcolor <tab 1-5> <RRGGBB> [keep] | tabcolor off | tabwatch";

    private readonly TargetedAlert targetedAlert;

    // Texts injected but not yet seen; a handful at most, only touched on the framework thread
    private readonly HashSet<string> pending = [];

    private string? fakeTargeter;
    private bool watchingEmotes;

    // The recoloured tab (0-based), its colour and the colour it had before; only touched on the framework thread
    private int? colouredTab;
    private ByteColor tabColour;
    private ByteColor originalTabColour;
    private bool keepTabColour;
    private int framesSinceTabColoured;
    // Logged once only, since with "keep" the game may change it back every frame
    private bool tabColourReset;

    // Frames left of logging the chat tabs' message counts and colours, and each tab's last logged state
    private int tabWatchFrames;
    private readonly string?[] tabStates = new string?[5];

    public TestHarness(TargetedAlert targetedAlert)
    {
        this.targetedAlert = targetedAlert;
        targetedAlert.FakeTargetingYou = name => name == fakeTargeter;
        Plugin.ChatGui.ChatMessage += OnChatMessage;
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessageUnhandled;
    }

    public void Dispose()
    {
        ClearTabColour();
        if (tabWatchFrames > 0)
        {
            Plugin.Framework.Update -= OnTabWatchUpdate;
        }
        Plugin.ChatGui.ChatMessage -= OnChatMessage;
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessageUnhandled;
        targetedAlert.FakeTargetingYou = null;
        targetedAlert.EmotePlayed = null;
    }

    /// <summary>Runs a harness subcommand; false if <paramref name="args"/> isn't one, so it can mean something else.</summary>
    public bool Handle(string args)
    {
        var (command, rest) = args.Split(' ', 2, StringSplitOptions.TrimEntries) switch
        {
            [var c] => (c, ""),
            [var c, var r] => (c, r),
            _ => ("", ""),
        };

        var ok = command.ToLowerInvariant() switch
        {
            "inject" => Inject(rest),
            "emotes" => ToggleEmoteWatch(rest),
            "targeting-me" => SetFakeTargeter(rest),
            "tabcolor" => SetTabColour(rest),
            "tabwatch" => WatchTabs(rest),
            _ => (bool?)null,
        };

        if (ok == false)
        {
            Plugin.ChatGui.PrintError($"Usage: /saymyname {Usage}");
        }
        return ok != null;
    }

    /// <summary>Queues "&lt;channel&gt; &lt;First&gt; &lt;Last&gt; &lt;message&gt;" as a chat line; false if malformed.</summary>
    private bool Inject(string args)
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

    // Logs every emote the Targeted trigger's hook sees, so other players' emotes in a busy place
    // show whether the hook fires for them, aimed at whom, and whether they were logged
    private bool ToggleEmoteWatch(string args)
    {
        if (args.Length > 0)
        {
            return false;
        }

        watchingEmotes = !watchingEmotes;
        targetedAlert.EmotePlayed = watchingEmotes ? OnEmotePlayed : null;
        Plugin.ChatGui.Print($"Test harness: emote watch {(watchingEmotes ? "on, see /xllog" : "off")}");
        return true;
    }

    // Lines from this name pass the Targeted trigger's "sender is targeting you" check
    private bool SetFakeTargeter(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (0 or 2))
        {
            return false;
        }

        fakeTargeter = parts.Length == 2 ? $"{parts[0]} {parts[1]}" : null;
        Plugin.ChatGui.Print(fakeTargeter == null
            ? "Test harness: nobody is pretending to target you"
            : $"Test harness: {fakeTargeter} is pretending to target you");
        return true;
    }

    // Sets a chat tab's name colour once, then logs the frame the game changes it back.
    // With "keep" it re-applies the colour every frame instead, to see whether that holds without flicker.
    private bool SetTabColour(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["off"])
        {
            ClearTabColour();
            Plugin.ChatGui.Print("Test harness: tab colour cleared");
            return true;
        }

        if (parts.Length is not (2 or 3)
            || !int.TryParse(parts[0], out var tab) || tab is < 1 or > 5
            || parts[1].Length != 6 || !uint.TryParse(parts[1], System.Globalization.NumberStyles.HexNumber, null, out var rgb)
            || (parts.Length == 3 && !parts[2].Equals("keep", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        ClearTabColour();
        var text = TabTextNode(tab - 1);
        if (text == null)
        {
            Plugin.ChatGui.PrintError($"Test harness: chat tab {tab} not found");
            return true;
        }

        colouredTab = tab - 1;
        originalTabColour = text->TextColor;
        tabColour = new ByteColor { R = (byte)(rgb >> 16), G = (byte)(rgb >> 8), B = (byte)rgb, A = 255 };
        keepTabColour = parts.Length == 3;
        framesSinceTabColoured = 0;
        tabColourReset = false;
        text->TextColor = tabColour;
        Plugin.Framework.Update += OnFrameworkUpdate;
        Plugin.Log.Information($"Test harness: tab {tab} text colour {Hex(originalTabColour)} -> {Hex(tabColour)}, keep={keepTabColour}");
        return true;
    }

    private void ClearTabColour()
    {
        if (colouredTab is not { } tab)
        {
            return;
        }

        Plugin.Framework.Update -= OnFrameworkUpdate;
        colouredTab = null;
        var text = TabTextNode(tab);
        if (text != null)
        {
            text->TextColor = originalTabColour;
        }
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        if (colouredTab is not { } tab)
        {
            return;
        }

        framesSinceTabColoured++;
        var text = TabTextNode(tab);
        if (text == null || text->TextColor.RGBA == tabColour.RGBA)
        {
            return;
        }

        if (!tabColourReset)
        {
            Plugin.Log.Information($"Test harness: tab {tab + 1} colour changed to {Hex(text->TextColor)} after {framesSinceTabColoured} frames");
            tabColourReset = true;
        }
        if (keepTabColour)
        {
            text->TextColor = tabColour;
        }
        else
        {
            // Stop watching, so the next change isn't reported and the game's colour is left alone
            Plugin.Framework.Update -= OnFrameworkUpdate;
            colouredTab = null;
        }
    }

    private static AtkTextNode* TabTextNode(int tab)
    {
        var chatLog = (AddonChatLog*)RaptureAtkUnitManager.Instance()->GetAddonByName("ChatLog");
        if (chatLog == null || tab >= chatLog->TabCount)
        {
            return null;
        }

        var button = chatLog->ChatTabs[tab].Value;
        return button == null ? null : button->ButtonTextNode;
    }

    // For ten seconds, logs each chat tab's message count and colours whenever they change, to see
    // whether inactive tabs' counts go up and what the game's unread flash changes
    private bool WatchTabs(string args)
    {
        if (args.Length > 0)
        {
            return false;
        }

        if (tabWatchFrames == 0)
        {
            Plugin.Framework.Update += OnTabWatchUpdate;
        }
        tabWatchFrames = TabWatchLength;
        Array.Clear(tabStates);
        Plugin.ChatGui.Print("Test harness: logging chat tabs for 10 s, see /xllog");
        return true;
    }

    private const int TabWatchLength = 600;

    private void OnTabWatchUpdate(IFramework framework)
    {
        if (--tabWatchFrames <= 0)
        {
            Plugin.Framework.Update -= OnTabWatchUpdate;
            Plugin.Log.Information("Test harness: tab watch ended");
            return;
        }

        var chatLog = (AddonChatLog*)RaptureAtkUnitManager.Instance()->GetAddonByName("ChatLog");
        var counts = ChatLogNumberArray.Instance();
        if (chatLog == null || counts == null)
        {
            return;
        }

        for (var i = 0; i < Math.Min((int)chatLog->TabCount, tabStates.Length); i++)
        {
            var tab = chatLog->ChatTabs[i].Value;
            if (tab == null)
            {
                continue;
            }

            var count = i switch
            {
                0 => counts->Tab1.TotalMessageCount,
                1 => counts->Tab2.TotalMessageCount,
                2 => counts->Tab3.TotalMessageCount,
                3 => counts->Tab4.TotalMessageCount,
                _ => -1,
            };
            var text = tab->ButtonTextNode;
            var state = $"count={count} active={i == chatLog->TabIndex} " +
                (text == null ? "no text node" : $"text={Hex(text->TextColor)} edge={Hex(text->EdgeColor)} textNode={Tint(&text->AtkResNode)}") +
                $" bg={Tint(tab->ButtonBGNode)} owner={Tint(&tab->OwnerNode->AtkResNode)}";
            if (state != tabStates[i])
            {
                tabStates[i] = state;
                Plugin.Log.Information($"Test harness: frame {TabWatchLength - tabWatchFrames} tab {i + 1} {state}");
            }
        }
    }

    // A node's colour, multiply and add tint, and visibility
    private static string Tint(AtkResNode* node) => node == null
        ? "null"
        : $"{Hex(node->Color)}*{node->MultiplyRed},{node->MultiplyGreen},{node->MultiplyBlue}+{node->AddRed},{node->AddGreen},{node->AddBlue}{(node->IsVisible() ? "" : " hidden")}";

    private static string Hex(ByteColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";

    private static void OnEmotePlayed(nint owner, uint emoteId, ulong targetId, bool logged)
    {
        var from = Plugin.ObjectTable.CreateObjectReference(owner)?.Name.TextValue ?? $"0x{owner:X}";
        var target = targetId == Plugin.ObjectTable.LocalPlayer?.GameObjectId
            ? "you"
            : Plugin.ObjectTable.SearchById(targetId)?.Name.TextValue ?? $"0x{targetId:X}";
        Plugin.Log.Information($"Test harness: emote {emoteId} from {from} at {target}, logged={logged}");
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
