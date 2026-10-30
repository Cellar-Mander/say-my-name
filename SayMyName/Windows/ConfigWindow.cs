using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.Config;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Interface.ImGuiSeStringRenderer;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Lumina.Excel.Sheets;
using SayMyName.Core;

namespace SayMyName.Windows;

public class ConfigWindow : Window, IDisposable
{
    // Rows, channels shared by the most triggers first so the full rows sit together
    private static readonly IReadOnlyList<ChatChannel> Channels = Triggers.ChannelsByUsage;
    private static readonly IReadOnlyList<TriggerKind> Columns = Triggers.WithChannelChoice;

    // ImGui's default (0.6) is easy to miss on a disabled trigger's column
    private const float DisabledTriggerAlpha = 0.3f;

    private readonly Configuration configuration;
    private readonly AlertSound alertSound;
    private readonly ChatHighlighter highlighter;

    // ImGui IDs and labels built once rather than every frame
    private readonly string[] triggerLabels = new string[Triggers.All.Count];
    private readonly string[] columnHeaders = new string[Columns.Count];
    private readonly string[] channelLabels = new string[Channels.Count];
    private static readonly string[] SoundLabels =
        [.. Enumerable.Range(Configuration.MinSoundEffect, Configuration.MaxSoundEffect - Configuration.MinSoundEffect + 1).Select(n => $"<se.{n}>")];
    private readonly string?[,] channelCheckboxIds = new string?[Columns.Count, Channels.Count];

    // The UIColor sheet (about 200 rows), with a colour per game theme: Dark, Light, Classic FF, Clear Blue.
    // Chat draws both text and glow in the theme set in the game's options, so the swatches use it too.
    private readonly List<(ushort Key, uint[] ByTheme)> sheetColors = [];
    private readonly Dictionary<ushort, Vector4> colors = [];
    private List<List<ushort>> palette = [];
    private uint? paletteTheme;

    // The preview: a sample line run through the chat highlighter, encoded again only when an input changes
    private const string PreviewFallbackName = "Rose";
    private const string PreviewBefore = "Hey ";
    private const string PreviewMiddle = ", thank ";
    private const string PreviewYou = "you";
    private const string PreviewAfter = "!";
    private (string You, ushort Text, ushort Glow)? previewInputs;
    private byte[] previewBytes = [];
    private static readonly PickerIds TextPicker = new("Text", "highlightText");
    private static readonly PickerIds GlowPicker = new("Glow", "highlightGlow");
    private static readonly PickerIds TabPicker = new("Tab name", "highlightTab");

    private sealed record PickerIds(string Label, string Id)
    {
        public string Swatch { get; } = $"##{Id}-swatch";
        public string None { get; } = $"None##{Id}-swatch";
        public string Popup { get; } = $"##{Id}-popup";
    }

    public ConfigWindow(Plugin plugin) : base("Say My Name Settings###SayMyNameSettings")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(560, 620);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
        alertSound = plugin.AlertSound;
        highlighter = plugin.Highlighter;

        for (var t = 0; t < Triggers.All.Count; t++)
        {
            triggerLabels[t] = TriggerLabel(Triggers.All[t]);
        }

        for (var t = 0; t < Columns.Count; t++)
        {
            var kind = Columns[t];
            columnHeaders[t] = ColumnHeader(kind);
            var allowed = Triggers.AllowedChannels(kind);
            for (var c = 0; c < Channels.Count; c++)
            {
                if (allowed.Contains(Channels[c]))
                {
                    channelCheckboxIds[t, c] = $"##{kind}-{Channels[c]}";
                }
            }
        }

        for (var c = 0; c < Channels.Count; c++)
        {
            channelLabels[c] = ChannelLabel(Channels[c]);
        }

        foreach (var row in Plugin.DataManager.GetExcelSheet<UIColor>())
        {
            if (row.RowId <= ushort.MaxValue)
            {
                sheetColors.Add(((ushort)row.RowId, [row.Dark, row.Light, row.ClassicFF, row.ClearBlue]));
            }
        }
    }

    // Rebuilds the swatches when the game's theme changes (and on first draw)
    private void UpdatePalette()
    {
        var theme = Plugin.GameConfig.TryGet(SystemConfigOption.ColorThemeType, out uint t) && t < 4 ? t : 0;
        if (theme == paletteTheme)
        {
            return;
        }

        paletteTheme = theme;
        colors.Clear();
        foreach (var (key, byTheme) in sheetColors)
        {
            colors[key] = ToVector(byTheme[theme]);
        }
        palette = ColorPalette.Arrange(sheetColors.Select(c => (c.Key, c.ByTheme[theme])));
    }

    // UIColor stores 0xRRGGBBAA
    private static Vector4 ToVector(uint rgba) =>
        new((rgba >> 24) / 255f, ((rgba >> 16) & 0xFF) / 255f, ((rgba >> 8) & 0xFF) / 255f, (rgba & 0xFF) / 255f);

    public void Dispose() { }

    // Nicknames and custom words save when their box loses focus, which closing the window mid-edit skips
    public override void OnClose() => configuration.Save();

    public override void Draw()
    {
        if (DrawSectionHeader(SettingsSection.Triggers, "Alert Triggers###Triggers"))
        {
            DrawTriggerOptions();
        }

        if (DrawSectionHeader(SettingsSection.Sound, "Sound###Sound"))
        {
            DrawSoundOptions();
        }

        if (DrawSectionHeader(SettingsSection.Highlight, "Highlight###Highlight"))
        {
            DrawHighlightOptions();
        }

        // Last, as its table scrolls in whatever height the window has left
        if (DrawSectionHeader(SettingsSection.Channels, "Chat Channels###Channels"))
        {
            DrawChannelTable();
        }
    }

    /// <summary>Draws a section's collapsing header, open or closed as saved, and saves it when toggled.</summary>
    private bool DrawSectionHeader(SettingsSection section, string label)
    {
        var expanded = configuration.ExpandedSections!; // set by Normalize() on load
        var wasOpen = expanded.Contains(section);
        ImGui.SetNextItemOpen(wasOpen, ImGuiCond.Always);
        var open = ImGui.CollapsingHeader(label);
        if (open != wasOpen)
        {
            if (open)
            {
                expanded.Add(section);
            }
            else
            {
                expanded.Remove(section);
            }
            configuration.Save();
        }
        return open;
    }

    private void DrawTriggerOptions()
    {
        for (var t = 0; t < Triggers.All.Count; t++)
        {
            var settings = configuration.Triggers[Triggers.All[t]];
            var enabled = settings.Enabled;
            if (ImGui.Checkbox(triggerLabels[t], ref enabled))
            {
                settings.Enabled = enabled;
                configuration.Save();
            }

            if (Triggers.All[t] == TriggerKind.Name)
            {
                DrawNameOptions(settings.Enabled);
            }
            else if (Triggers.All[t] == TriggerKind.Custom)
            {
                DrawCustomOptions(settings.Enabled);
            }
            else if (Triggers.All[t] == TriggerKind.Emote)
            {
                using var indent = ImRaii.PushIndent();
                // The plugin writes these lines itself, so say so before someone wonders why emotes show with logging off
                ImGui.TextDisabled($"Motion-only emotes at you are written to chat, ending \"{TargetedAlert.InsertedTag.Trim()}\";");
                ImGui.TextDisabled("they alert only if a chat tab shows standard or custom emotes");
            }
            else if (Triggers.All[t] == TriggerKind.TargetSpeaks)
            {
                DrawTargetSpeaksOptions(settings.Enabled);
            }
        }
    }

    private void DrawChannelTable()
    {
        ImGui.TextUnformatted("Listen on these channels:");

        using var table = ImRaii.Table("##channels", 1 + Columns.Count,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.PadOuterX);
        if (!table.Success)
        {
            return;
        }

        using var dimmed = ImRaii.PushStyle(ImGuiStyleVar.DisabledAlpha, DisabledTriggerAlpha);

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Channel", ImGuiTableColumnFlags.WidthStretch);
        foreach (var header in columnHeaders)
        {
            ImGui.TableSetupColumn(header);
        }

        // Header row drawn by hand so a disabled trigger's header is dimmed with its column.
        // TableHeader (not plain text) so the columns fit their headers; its hover and click
        // colours are cleared because the headers do nothing when clicked
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        using (ImRaii.PushColor(ImGuiCol.HeaderHovered, 0u).Push(ImGuiCol.HeaderActive, 0u))
        {
            ImGui.TableNextColumn();
            ImGui.TableHeader("Channel");
            for (var t = 0; t < Columns.Count; t++)
            {
                ImGui.TableNextColumn();
                using (ImRaii.Disabled(!configuration.Triggers[Columns[t]].Enabled))
                {
                    ImGui.TableHeader(columnHeaders[t]);
                }
            }
        }

        for (var c = 0; c < Channels.Count; c++)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(channelLabels[c]);

            for (var t = 0; t < Columns.Count; t++)
            {
                ImGui.TableNextColumn();
                var id = channelCheckboxIds[t, c];
                if (id == null)
                {
                    continue;
                }

                var settings = configuration.Triggers[Columns[t]];
                var on = settings.Channels.Contains(Channels[c]);
                using (ImRaii.Disabled(!settings.Enabled))
                {
                    if (ImGui.Checkbox(id, ref on))
                    {
                        if (on)
                        {
                            settings.Channels.Add(Channels[c]);
                        }
                        else
                        {
                            settings.Channels.Remove(Channels[c]);
                        }
                        configuration.Save();
                    }
                }
            }
        }
    }

    private void DrawNameOptions(bool enabled)
    {
        using var indent = ImRaii.PushIndent();
        using var disabled = ImRaii.Disabled(!enabled);
        var caseSensitive = configuration.NameCaseSensitive;
        if (ImGui.Checkbox("Match case (Rose, but not \"a rose\")", ref caseSensitive))
        {
            configuration.NameCaseSensitive = caseSensitive;
            configuration.Save();
        }

        ImGui.TextUnformatted("Nicknames, one per line, matched like your name:");
        var nicknames = configuration.Nicknames;
        if (ImGui.InputTextMultiline("##nicknames", ref nicknames, Configuration.MaxNicknamesLength, ListBoxSize()))
        {
            configuration.Nicknames = nicknames;
        }
        // Saved once editing ends rather than on every keystroke
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            configuration.Save();
        }
    }

    // The nicknames and custom words boxes, the same size
    private static Vector2 ListBoxSize() => new(-1, ImGui.GetTextLineHeight() * 4 + ImGui.GetStyle().FramePadding.Y * 2);

    private static readonly string[] TargetSpeaksLabels = ["Every message they send", "Only messages sent while targeting you"];

    private void DrawTargetSpeaksOptions(bool enabled)
    {
        using var indent = ImRaii.PushIndent();
        using var disabled = ImRaii.Disabled(!enabled);
        var onlyTargetingYou = configuration.TargetSpeaksOnlyTargetingYou;
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 280f);
        using var combo = ImRaii.Combo("##targetSpeaks", TargetSpeaksLabels[onlyTargetingYou ? 1 : 0]);
        if (!combo.Success)
        {
            return;
        }
        for (var i = 0; i < TargetSpeaksLabels.Length; i++)
        {
            if (ImGui.Selectable(TargetSpeaksLabels[i], (i == 1) == onlyTargetingYou))
            {
                configuration.TargetSpeaksOnlyTargetingYou = i == 1;
                configuration.Save();
            }
        }
    }

    private void DrawCustomOptions(bool enabled)
    {
        using var indent = ImRaii.PushIndent();
        using var disabled = ImRaii.Disabled(!enabled);
        var caseSensitive = configuration.CustomWordsCaseSensitive;
        if (ImGui.Checkbox("Match case##customCase", ref caseSensitive))
        {
            configuration.CustomWordsCaseSensitive = caseSensitive;
            configuration.Save();
        }

        ImGui.TextUnformatted("Custom words and phrases, one per line:");
        var words = configuration.CustomWords;
        if (ImGui.InputTextMultiline("##customWords", ref words, Configuration.MaxCustomWordsLength, ListBoxSize()))
        {
            configuration.CustomWords = words;
        }
        // Saved once editing ends rather than on every keystroke
        if (ImGui.IsItemDeactivatedAfterEdit())
        {
            configuration.Save();
        }
    }

    private void DrawSoundOptions()
    {
        var playSound = configuration.PlaySound;
        if (ImGui.Checkbox("Play a sound", ref playSound))
        {
            configuration.PlaySound = playSound;
            configuration.Save();
        }

        using var indent = ImRaii.PushIndent();
        using var disabled = ImRaii.Disabled(!playSound);
        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 160f);
        using (var combo = ImRaii.Combo("Sound##sound", SoundLabels[configuration.SoundEffect - Configuration.MinSoundEffect]))
        {
            if (combo.Success)
            {
                for (var i = 0; i < SoundLabels.Length; i++)
                {
                    var soundEffect = Configuration.MinSoundEffect + i;
                    if (ImGui.Selectable(SoundLabels[i], soundEffect == configuration.SoundEffect))
                    {
                        configuration.SoundEffect = soundEffect;
                        configuration.Save();
                    }
                }
            }
        }

        ImGui.SameLine();
        using (ImRaii.Disabled(!alertSound.CanTest))
        {
            if (ImGui.Button("Test"))
            {
                alertSound.Test();
            }
        }

        ImGui.SetNextItemWidth(ImGuiHelpers.GlobalScale * 160f);
        var cooldown = configuration.CooldownSeconds;
        if (ImGui.SliderInt("Quiet time between sounds##cooldown", ref cooldown, 0, Configuration.MaxCooldownSeconds, "%d s",
                ImGuiSliderFlags.AlwaysClamp))
        {
            configuration.CooldownSeconds = cooldown;
            configuration.Save();
        }
    }

    private void DrawHighlightOptions()
    {
        var highlight = configuration.Highlight;
        if (ImGui.Checkbox("Colour the words that set off an alert in chat", ref highlight))
        {
            configuration.Highlight = highlight;
            configuration.Save();
        }

        UpdatePalette();
        using (ImRaii.PushIndent())
        using (ImRaii.Disabled(!highlight))
        {
            var textColor = configuration.HighlightTextColor;
            if (DrawColorPicker(TextPicker, ref textColor, colors, palette))
            {
                configuration.HighlightTextColor = textColor;
                configuration.Save();
            }

            ImGui.SameLine();
            var glowColor = configuration.HighlightGlowColor;
            if (DrawColorPicker(GlowPicker, ref glowColor, colors, palette))
            {
                configuration.HighlightGlowColor = glowColor;
                configuration.Save();
            }

            ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X * 3);
            DrawHighlightPreview();
            ImGui.TextDisabled("Your name and nicknames, custom words, and 'you' words from someone targeting you");
        }

        var highlightTabs = configuration.HighlightTabs;
        if (ImGui.Checkbox("Colour the chat tabs an alert arrives in, until opened", ref highlightTabs))
        {
            configuration.HighlightTabs = highlightTabs;
            configuration.Save();
        }

        using var indent = ImRaii.PushIndent();
        using var disabled = ImRaii.Disabled(!highlightTabs);
        var tabColor = configuration.TabHighlightColor;
        if (DrawColorPicker(TabPicker, ref tabColor, colors, palette, allowNone: false))
        {
            configuration.TabHighlightColor = tabColor;
            configuration.Save();
        }
    }

    /// <summary>
    /// A sample chat line with your first name and "you" highlighted by the same code as chat, drawn by
    /// Dalamud's SeString renderer, which draws colours and glow as the game does.
    /// </summary>
    private void DrawHighlightPreview()
    {
        var inputs = (Plugin.PlayerState.CharacterName, configuration.HighlightTextColor, configuration.HighlightGlowColor);
        if (inputs != previewInputs)
        {
            previewInputs = inputs;
            var name = inputs.CharacterName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? PreviewFallbackName;
            var line = new SeStringBuilder().AddText(PreviewBefore + name + PreviewMiddle + PreviewYou + PreviewAfter).Build();
            highlighter.Begin(line);
            highlighter.Matches.Add((PreviewBefore.Length, name.Length));
            // As chat colours it from a sender targeting you
            highlighter.Matches.Add((PreviewBefore.Length + name.Length + PreviewMiddle.Length, PreviewYou.Length));
            previewBytes = highlighter.Highlight(line).Encode();
        }

        ImGui.AlignTextToFramePadding();
        // Opacity left to ImGui's, so the line dims with the rest when highlighting is off
        ImGuiHelpers.SeStringWrapped(previewBytes, new SeStringDrawParams());
    }

    /// <summary>A swatch that opens a grid of the palette's colours, plus None if allowed. True when one is picked.</summary>
    private static bool DrawColorPicker(PickerIds ids, ref ushort key, Dictionary<ushort, Vector4> colors, List<List<ushort>> palette, bool allowNone = true)
    {
        var swatch = new Vector2(ImGui.GetFrameHeight());
        if (key != Configuration.NoColor && colors.TryGetValue(key, out var current))
        {
            if (ImGui.ColorButton(ids.Swatch, current, ImGuiColorEditFlags.NoTooltip, swatch))
            {
                ImGui.OpenPopup(ids.Popup);
            }
        }
        else if (ImGui.Button(ids.None))
        {
            ImGui.OpenPopup(ids.Popup);
        }
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted(ids.Label);

        using var popup = ImRaii.Popup(ids.Popup);
        if (!popup.Success)
        {
            return false;
        }

        var picked = false;
        if (allowNone && ImGui.Selectable("None", key == Configuration.NoColor))
        {
            key = Configuration.NoColor;
            picked = true;
        }

        // Drawn row by row across the columns; a short column leaves a gap at its foot
        for (var y = 0; y < ColorPalette.ColumnHeight; y++)
        {
            for (var x = 0; x < palette.Count; x++)
            {
                if (x > 0)
                {
                    ImGui.SameLine();
                }

                var column = palette[x];
                if (y >= column.Count)
                {
                    ImGui.Dummy(swatch);
                    continue;
                }

                var row = column[y];
                // Interpolated only while the popup is open; the row number is the tooltip
                if (ImGui.ColorButton($"{row}##{ids.Id}", colors[row], ImGuiColorEditFlags.None, swatch))
                {
                    key = row;
                    picked = true;
                    ImGui.CloseCurrentPopup();
                }
            }
        }
        return picked;
    }

    private static string TriggerLabel(TriggerKind kind) => kind switch
    {
        TriggerKind.Name => "Another player says your name or a nickname",
        TriggerKind.Custom => "Someone says one of your custom words or phrases",
        TriggerKind.Emote => "Someone uses an emote on you",
        TriggerKind.You => "Someone targeting you uses a 'you' word",
        TriggerKind.TargetSpeaks => "Your current target speaks on any channel",
        _ => kind.ToString(),
    };

    private static string ColumnHeader(TriggerKind kind) => kind switch
    {
        TriggerKind.Name => "Name",
        TriggerKind.Custom => "Custom",
        TriggerKind.You => "You",
        _ => kind.ToString(),
    };

    private static string ChannelLabel(ChatChannel channel) => channel switch
    {
        ChatChannel.FreeCompany => "Free Company",
        ChatChannel.NoviceNetwork => "Novice Network",
        >= ChatChannel.Linkshell1 and <= ChatChannel.Linkshell8 => $"Linkshell {channel - ChatChannel.Linkshell1 + 1}",
        >= ChatChannel.CrossLinkshell1 and <= ChatChannel.CrossLinkshell8 => $"Cross-world Linkshell {channel - ChatChannel.CrossLinkshell1 + 1}",
        ChatChannel.CustomEmote => "Custom emotes",
        _ => channel.ToString(),
    };
}
