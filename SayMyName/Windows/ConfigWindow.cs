using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using SayMyName.Core;

namespace SayMyName.Windows;

public class ConfigWindow : Window, IDisposable
{
    // Rows, channels shared by the most triggers first so the full rows sit together
    private static readonly IReadOnlyList<ChatChannel> Channels = Triggers.ChannelsByUsage;

    // ImGui's default (0.6) is easy to miss on a disabled trigger's column
    private const float DisabledTriggerAlpha = 0.3f;

    private readonly Configuration configuration;
    private readonly NameAlert nameAlert;

    // ImGui IDs and labels built once rather than every frame
    private readonly string[] triggerLabels = new string[Triggers.All.Count];
    private readonly string[] columnHeaders = new string[Triggers.All.Count];
    private readonly string[] channelLabels = new string[Channels.Count];
    private readonly string?[,] channelCheckboxIds = new string?[Triggers.All.Count, Channels.Count];

    public ConfigWindow(Plugin plugin) : base("Say My Name###SayMyNameSettings")
    {
        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(480, 360),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
        Size = new Vector2(560, 620);
        SizeCondition = ImGuiCond.FirstUseEver;

        configuration = plugin.Configuration;
        nameAlert = plugin.NameAlert;

        for (var t = 0; t < Triggers.All.Count; t++)
        {
            var kind = Triggers.All[t];
            triggerLabels[t] = TriggerLabel(kind);
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
    }

    public void Dispose() { }

    public override void Draw()
    {
        ImGui.TextUnformatted("Alert when:");
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
        }

        ImGuiHelpers.ScaledDummy(6f);
        DrawSoundOptions();

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextUnformatted("Listen on these channels (tells are never checked):");

        using var table = ImRaii.Table("##channels", 1 + Triggers.All.Count,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit);
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

        // Header row drawn by hand so a disabled trigger's header is dimmed with its column
        ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
        ImGui.TableNextColumn();
        ImGui.TableHeader("Channel");
        for (var t = 0; t < Triggers.All.Count; t++)
        {
            ImGui.TableNextColumn();
            using (ImRaii.Disabled(!configuration.Triggers[Triggers.All[t]].Enabled))
            {
                ImGui.TableHeader(columnHeaders[t]);
            }
        }

        for (var c = 0; c < Channels.Count; c++)
        {
            ImGui.TableNextRow();
            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            ImGui.TextUnformatted(channelLabels[c]);

            for (var t = 0; t < Triggers.All.Count; t++)
            {
                ImGui.TableNextColumn();
                var id = channelCheckboxIds[t, c];
                if (id == null)
                {
                    continue;
                }

                var settings = configuration.Triggers[Triggers.All[t]];
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
        var soundEffect = configuration.SoundEffect;
        if (ImGui.SliderInt("Sound##sound", ref soundEffect, Configuration.MinSoundEffect, Configuration.MaxSoundEffect, "<se.%d>",
                ImGuiSliderFlags.AlwaysClamp))
        {
            configuration.SoundEffect = soundEffect;
            configuration.Save();
        }

        ImGui.SameLine();
        if (ImGui.Button("Test"))
        {
            nameAlert.PlaySound();
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

    private static string TriggerLabel(TriggerKind kind) => kind switch
    {
        TriggerKind.Name => "Another player says your name",
        TriggerKind.Custom => "Someone says one of your custom words or phrases",
        TriggerKind.Targeted => "Someone emotes at you, or targets you and uses a 'you' message",
        TriggerKind.TargetSpeaks => "Your current target speaks",
        _ => kind.ToString(),
    };

    private static string ColumnHeader(TriggerKind kind) => kind switch
    {
        TriggerKind.Name => "Name",
        TriggerKind.Custom => "Custom",
        TriggerKind.Targeted => "Targeted",
        TriggerKind.TargetSpeaks => "Target",
        _ => kind.ToString(),
    };

    private static string ChannelLabel(ChatChannel channel) => channel switch
    {
        ChatChannel.CrossParty => "Cross-world Party",
        ChatChannel.FreeCompany => "Free Company",
        ChatChannel.PvPTeam => "PvP Team",
        ChatChannel.NoviceNetwork => "Novice Network",
        >= ChatChannel.Linkshell1 and <= ChatChannel.Linkshell8 => $"Linkshell {channel - ChatChannel.Linkshell1 + 1}",
        >= ChatChannel.CrossLinkshell1 and <= ChatChannel.CrossLinkshell8 => $"Cross-world Linkshell {channel - ChatChannel.CrossLinkshell1 + 1}",
        ChatChannel.StandardEmote => "Emotes",
        ChatChannel.CustomEmote => "Custom emotes",
        _ => channel.ToString(),
    };
}
