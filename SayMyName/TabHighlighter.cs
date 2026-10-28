using System;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.Addon.Lifecycle.AddonArgTypes;
using Dalamud.Game.Config;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using Lumina.Excel.Sheets;
using SayMyName.Core;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Colours the names of the inactive chat tabs that show a line which set off an alert, until the
/// tab is opened, in place of the game's unread colour.
/// </summary>
/// <remarks>
/// Which channels a tab shows isn't readable, so the tabs are found by their message counts (in
/// game, the log number of the tab's newest line, so they move together for a line every tab
/// shows): taken when the alerted line arrives (before the game adds it), then compared each frame
/// until one goes up. Another line landing in the same frame can colour a tab that only shows that line.
/// The game animates an unread tab's colour every frame, so the colour is put back after each of
/// the chat log's updates, just before it draws, pulsed in step with the game's (see <see cref="TabPulse"/>).
/// </remarks>
public sealed unsafe class TabHighlighter : IDisposable
{
    private const string ChatLogAddon = "ChatLog";
    // The chat log's number array has message counts for this many tabs
    private const int CountedTabs = 4;
    // Frames to wait for the line to reach the tabs, after which it was hidden or never logged
    private const int MaxWaitFrames = 10;

    private readonly Configuration configuration;

    // Only touched on the framework thread, as are the chat events that call Alert
    private readonly int[] counts = new int[CountedTabs];
    private readonly bool[] highlighted = new bool[CountedTabs];
    private int highlightedCount;
    private int framesWaited;
    private bool waiting;

    public TabHighlighter(Configuration configuration)
    {
        this.configuration = configuration;
        Plugin.AddonLifecycle.RegisterListener(AddonEvent.PreDraw, ChatLogAddon, OnChatLogPreDraw);
    }

    /// <summary>Whether tab colouring is on and has a colour to use.</summary>
    public bool Enabled => configuration.HighlightTabs && configuration.TabHighlightColor != Configuration.NoColor;

    public void Dispose()
    {
        Plugin.AddonLifecycle.UnregisterListener(AddonEvent.PreDraw, ChatLogAddon, OnChatLogPreDraw);
    }

    /// <summary>Call from the chat event for the line that set off an alert: the tabs it lands in are coloured.</summary>
    public void Alert()
    {
        // Already waiting: those counts predate this line too, so it's caught with the first
        if (!Enabled || waiting)
        {
            return;
        }

        var array = ChatLogNumberArray.Instance();
        if (array == null)
        {
            return;
        }

        for (var i = 0; i < CountedTabs; i++)
        {
            counts[i] = TabCounts(array, i).TotalMessageCount;
        }
        framesWaited = 0;
        waiting = true;
    }

    private void OnChatLogPreDraw(AddonEvent type, AddonArgs args)
    {
        if (!waiting && highlightedCount == 0)
        {
            return;
        }

        var chatLog = (AddonChatLog*)args.Addon.Address;
        var tabs = Math.Min((int)chatLog->TabCount, CountedTabs);
        if (waiting)
        {
            FindAlertedTabs(chatLog, tabs);
        }

        // Turning the setting off, or opening a tab, hands the colour back to the game
        if (!Enabled)
        {
            Array.Clear(highlighted);
            highlightedCount = 0;
            return;
        }

        uint? color = null;
        for (var i = 0; i < CountedTabs; i++)
        {
            if (!highlighted[i])
            {
                continue;
            }

            if (i >= tabs || i == chatLog->TabIndex)
            {
                highlighted[i] = false;
                highlightedCount--;
                continue;
            }

            if (chatLog->ChatTabs[i].Value is var tab && tab != null && tab->ButtonTextNode != null)
            {
                // The game has just set this frame's step of its unread pulse; ours takes the same step
                ref var text = ref tab->ButtonTextNode->TextColor;
                color ??= TabColor();
                var pulsed = TabPulse.Apply(color.Value, TabPulse.Fraction((uint)(text.R << 16 | text.G << 8 | text.B)));
                text = new ByteColor { R = (byte)(pulsed >> 16), G = (byte)(pulsed >> 8), B = (byte)pulsed, A = 255 };
            }
        }
    }

    private void FindAlertedTabs(AddonChatLog* chatLog, int tabs)
    {
        var array = ChatLogNumberArray.Instance();
        if (array == null)
        {
            waiting = false;
            return;
        }

        var changed = false;
        for (var i = 0; i < tabs; i++)
        {
            if (TabCounts(array, i).TotalMessageCount == counts[i])
            {
                continue;
            }

            changed = true;
            if (i != chatLog->TabIndex && !highlighted[i])
            {
                highlighted[i] = true;
                highlightedCount++;
            }
        }

        if (changed || ++framesWaited > MaxWaitFrames)
        {
            if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
            {
                Plugin.Log.Verbose(changed
                    ? $"Tab highlight: line reached the tabs after {framesWaited} frames; active tab {chatLog->TabIndex + 1}"
                    : $"Tab highlight: line didn't reach any tab within {MaxWaitFrames} frames");
            }
            waiting = false;
        }
    }

    private static ref ChatLogNumberArray.ChatLogChatTabNumberArray TabCounts(ChatLogNumberArray* array, int tab)
    {
        switch (tab)
        {
            case 0: return ref array->Tab1;
            case 1: return ref array->Tab2;
            case 2: return ref array->Tab3;
            default: return ref array->Tab4;
        }
    }

    // The UIColor row in the game's current theme, as the settings window's swatches show it, as 0xRRGGBB
    private uint TabColor()
    {
        var theme = Plugin.GameConfig.TryGet(SystemConfigOption.ColorThemeType, out uint t) && t < 4 ? t : 0;
        var row = Plugin.DataManager.GetExcelSheet<UIColor>().GetRowOrDefault(configuration.TabHighlightColor);
        var rgba = row is not { } r ? 0xFFFFFFFF : theme switch
        {
            1 => r.Light,
            2 => r.ClassicFF,
            3 => r.ClearBlue,
            _ => r.Dark,
        };
        return rgba >> 8;
    }
}
