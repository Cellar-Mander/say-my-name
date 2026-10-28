using System;
using Dalamud.Game.Config;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Graphics;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Arrays;
using Lumina.Excel.Sheets;
using Serilog.Events;

namespace SayMyName;

/// <summary>
/// Colours the names of the inactive chat tabs that show a line which set off an alert. The game
/// puts the tab's own colour back when it is selected, as with its unread highlight.
/// </summary>
/// <remarks>
/// Which channels a tab shows isn't readable, so the tabs are found by their message counts: taken
/// when the alerted line arrives (before the game adds it), then compared each frame until one
/// goes up. Another line landing in the same frame can colour a tab that only shows that line.
/// </remarks>
public sealed unsafe class TabHighlighter(Configuration configuration) : IDisposable
{
    // The chat log's number array has message counts for this many tabs
    private const int CountedTabs = 4;
    // Frames to wait for the line to reach the tabs, after which it was hidden or never logged
    private const int MaxWaitFrames = 10;

    // Only touched on the framework thread, as are the chat events that call Alert
    private readonly int[] counts = new int[CountedTabs];
    private int framesWaited;
    private bool waiting;

    /// <summary>Whether tab colouring is on and has a colour to use.</summary>
    public bool Enabled => configuration.HighlightTabs && configuration.TabHighlightColor != Configuration.NoColor;

    public void Dispose()
    {
        StopWaiting();
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
        Plugin.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(IFramework framework)
    {
        var array = ChatLogNumberArray.Instance();
        var chatLog = (AddonChatLog*)RaptureAtkUnitManager.Instance()->GetAddonByName("ChatLog");
        if (array == null || chatLog == null)
        {
            StopWaiting();
            return;
        }
        if (++framesWaited > MaxWaitFrames)
        {
            if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
            {
                Plugin.Log.Verbose($"Tab highlight: line didn't reach any tab within {MaxWaitFrames} frames");
            }
            StopWaiting();
            return;
        }

        var changed = false;
        var tabs = Math.Min((int)chatLog->TabCount, CountedTabs);
        for (var i = 0; i < tabs; i++)
        {
            if (TabCounts(array, i).TotalMessageCount == counts[i])
            {
                continue;
            }

            changed = true;
            if (i != chatLog->TabIndex && chatLog->ChatTabs[i].Value is var tab && tab != null && tab->ButtonTextNode != null)
            {
                tab->ButtonTextNode->TextColor = TabColor();
            }
        }

        if (changed)
        {
            if (Plugin.Log.MinimumLogLevel <= LogEventLevel.Verbose)
            {
                Plugin.Log.Verbose($"Tab highlight: line reached the tabs after {framesWaited} frames; active tab {chatLog->TabIndex + 1}");
            }
            StopWaiting();
        }
    }

    private void StopWaiting()
    {
        if (waiting)
        {
            Plugin.Framework.Update -= OnFrameworkUpdate;
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

    // The UIColor row in the game's current theme, as the settings window's swatches show it
    private ByteColor TabColor()
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
        return new ByteColor { R = (byte)(rgba >> 24), G = (byte)(rgba >> 16), B = (byte)(rgba >> 8), A = 255 };
    }
}
