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
/// chat and logs when each comes back through the chat events the triggers listen on, can pretend an
/// injected sender is targeting you or is your target, can
/// recolour a chat tab's name to see whether the game keeps the colour, and gives Dalamud's self-test
/// window the automatable solo checklist cases (TestHarness.SelfTest.cs).
/// </summary>
public sealed unsafe partial class TestHarness : IDisposable
{
    // Chat replies carry it as Dalamud's message tag and log lines start with it the same way, so both read "[Test harness] ..."
    private const string Tag = "Test harness";

    public const string Usage = "inject <channel> <First> <Last> <message> | targeting-me [<First> <Last>] | my-target [<First> <Last>] | tabcolor <tab 1-5> <RRGGBB> [keep] | tabcolor off | tabwatch";

    private readonly Configuration configuration;
    private readonly AlertSound alertSound;
    private readonly TargetedAlert targetedAlert;
    private readonly Func<bool> settingsOpen;

    // Texts injected but not yet seen; a handful at most, only touched on the framework thread
    private readonly HashSet<string> pending = [];
    // Texts whose shown sender is replaced when they come back, as a plugin rewriting names would
    private readonly HashSet<string> rewriteSender = [];

    private string? fakeTargeter;
    private string? fakeTarget;

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

    public TestHarness(Configuration configuration, AlertSound alertSound, TargetedAlert targetedAlert, Func<bool> settingsOpen)
    {
        this.configuration = configuration;
        this.alertSound = alertSound;
        this.settingsOpen = settingsOpen;
        this.targetedAlert = targetedAlert;
        targetedAlert.FakeTargetingYou = name => name == fakeTargeter;
        targetedAlert.FakeYourTarget = name => name == fakeTarget;
        Plugin.ChatGui.ChatMessage += OnChatMessage;
        Plugin.ChatGui.ChatMessageUnhandled += OnChatMessageUnhandled;
    }

    public void Dispose()
    {
        EndSelfTest();
        ClearTabColour();
        if (tabWatchFrames > 0)
        {
            Plugin.Framework.Update -= OnTabWatchUpdate;
        }
        Plugin.ChatGui.ChatMessage -= OnChatMessage;
        Plugin.ChatGui.ChatMessageUnhandled -= OnChatMessageUnhandled;
        targetedAlert.FakeTargetingYou = null;
        targetedAlert.FakeYourTarget = null;
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
            "targeting-me" => SetFakeTargeter(rest),
            "my-target" => SetFakeTarget(rest),
            "tabcolor" => SetTabColour(rest),
            "tabwatch" => WatchTabs(rest),
            _ => (bool?)null,
        };

        if (ok == false)
        {
            Plugin.ChatGui.PrintError($"Usage: /saymyname {Usage}", Tag);
        }
        return ok != null;
    }

    /// <summary>Queues "&lt;channel&gt; &lt;First&gt; &lt;Last&gt; &lt;message&gt;" as a chat line; false if malformed.</summary>
    private bool Inject(string args)
    {
        var parts = args.Split(' ', 4, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 4)
        {
            return false;
        }
        XivChatType type;
        if (Enum.TryParse<ChatChannel>(parts[0], ignoreCase: true, out var channel) && Enum.IsDefined(channel))
        {
            type = ChatChannels.ToChatType(channel);
        }
        // Cross-world party lines count as Party, so they have no channel of their own to name
        else if (parts[0].Equals(nameof(XivChatType.CrossParty), StringComparison.OrdinalIgnoreCase))
        {
            type = XivChatType.CrossParty;
        }
        else
        {
            return false;
        }

        // Other players' lines carry a player link, so give the fake sender one on your world.
        // PlayerPayload encodes the name text and link terminator itself; adding them again doubles the name.
        var name = $"{parts[1]} {parts[2]}";
        InjectLine(type, new SeString(new PlayerPayload(name, Plugin.PlayerState.CurrentWorld.RowId)), parts[3]);
        return true;
    }

    private void InjectLine(XivChatType type, SeString sender, string text)
    {
        pending.Add(text);
        Plugin.ChatGui.Print(new XivChatEntry
        {
            Type = type,
            Name = sender,
            Message = text,
        });
        Plugin.Log.Information($"[{Tag}] injected {type} from {sender.TextValue}: {text}");
    }

    // Lines from this name pass the You trigger's "sender is targeting you" check
    private bool SetFakeTargeter(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (0 or 2))
        {
            return false;
        }

        fakeTargeter = parts.Length == 2 ? $"{parts[0]} {parts[1]}" : null;
        Plugin.ChatGui.Print(fakeTargeter == null
            ? "nobody is pretending to target you"
            : $"{fakeTargeter} is pretending to target you", Tag);
        return true;
    }

    // Lines from this name pass the Target Speaks trigger's "sender is your target" check
    private bool SetFakeTarget(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length is not (0 or 2))
        {
            return false;
        }

        fakeTarget = parts.Length == 2 ? $"{parts[0]} {parts[1]}" : null;
        Plugin.ChatGui.Print(fakeTarget == null
            ? "nobody is pretending to be your target"
            : $"{fakeTarget} is pretending to be your target", Tag);
        return true;
    }

    // Sets a chat tab's name colour and logs the frame the game first changes it. Without "keep" it
    // then leaves the colour to the game; with "keep" it re-applies the colour every frame.
    private bool SetTabColour(string args)
    {
        var parts = args.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts is ["off"])
        {
            ClearTabColour();
            Plugin.ChatGui.Print("tab colour cleared", Tag);
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
            Plugin.ChatGui.PrintError($"chat tab {tab} not found", Tag);
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
        Plugin.Log.Information($"[{Tag}] tab {tab} text colour {Hex(originalTabColour)} -> {Hex(tabColour)}, keep={keepTabColour}");
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
            Plugin.Log.Information($"[{Tag}] tab {tab + 1} colour changed to {Hex(text->TextColor)} after {framesSinceTabColoured} frames");
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
        var chatLog = Plugin.GameGui.GetAddonByName<AddonChatLog>("ChatLog");
        if (chatLog == null || tab >= chatLog->TabCount)
        {
            return null;
        }

        var button = chatLog->ChatTabs[tab].Value;
        return button == null ? null : button->ButtonTextNode;
    }

    // For ten seconds, logs each chat tab's message count, whether it's the active tab, and its
    // text, edge and node colours whenever any of them changes
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
        Plugin.ChatGui.Print("logging chat tabs for 10 s, see /xllog", Tag);
        return true;
    }

    private const int TabWatchLength = 600;

    private void OnTabWatchUpdate(IFramework framework)
    {
        if (--tabWatchFrames <= 0)
        {
            Plugin.Framework.Update -= OnTabWatchUpdate;
            Plugin.Log.Information($"[{Tag}] tab watch ended");
            return;
        }

        var chatLog = Plugin.GameGui.GetAddonByName<AddonChatLog>("ChatLog");
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
                Plugin.Log.Information($"[{Tag}] frame {TabWatchLength - tabWatchFrames} tab {i + 1} {state}");
            }
        }
    }

    // A node's colour, multiply and add tint, and visibility
    private static string Tint(AtkResNode* node) => node == null
        ? "null"
        : $"{Hex(node->Color)}*{node->MultiplyRed},{node->MultiplyGreen},{node->MultiplyBlue}+{node->AddRed},{node->AddGreen},{node->AddBlue}{(node->IsVisible() ? "" : " hidden")}";

    private static string Hex(ByteColor c) => $"#{c.R:X2}{c.G:X2}{c.B:X2}{c.A:X2}";

    private void OnChatMessage(IHandleableChatMessage message)
    {
        SelfTestSaw(message, unhandled: false);
        if (rewriteSender.Remove(message.Message.TextValue))
        {
            message.Sender = new SeString(new TextPayload("Random Name"));
        }
        if (pending.Contains(message.Message.TextValue))
        {
            Plugin.Log.Information($"[{Tag}] ChatMessage got {message.LogKind} from {message.Sender.TextValue}: {message.Message.TextValue}");
        }
    }

    private void OnChatMessageUnhandled(IChatMessage message)
    {
        SelfTestSaw(message, unhandled: true);
        if (pending.Remove(message.Message.TextValue))
        {
            Plugin.Log.Information($"[{Tag}] ChatMessageUnhandled got {message.LogKind} (source={message.SourceKind}) from {message.Sender.TextValue}");
        }
    }
}
