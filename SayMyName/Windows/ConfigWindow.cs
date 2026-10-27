using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using SayMyName.Core;

namespace SayMyName.Windows;

public class ConfigWindow : Window, IDisposable
{
    private static readonly ChatChannel[] Channels = Enum.GetValues<ChatChannel>();

    private readonly Configuration configuration;

    // ImGui IDs and labels built once rather than every frame
    private readonly string[] triggerLabels = new string[Triggers.All.Count];
    private readonly string[] channelLabels = new string[Channels.Length];
    private readonly string?[,] channelCheckboxIds = new string?[Triggers.All.Count, Channels.Length];

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

        for (var t = 0; t < Triggers.All.Count; t++)
        {
            var kind = Triggers.All[t];
            triggerLabels[t] = TriggerLabel(kind);
            foreach (var channel in Triggers.AllowedChannels(kind))
            {
                channelCheckboxIds[t, (int)channel] = $"##{kind}-{channel}";
            }
        }

        for (var c = 0; c < Channels.Length; c++)
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
        }

        ImGuiHelpers.ScaledDummy(6f);
        ImGui.TextUnformatted("Listen on these channels (tells are never checked):");

        using var table = ImRaii.Table("##channels", 1 + Triggers.All.Count,
            ImGuiTableFlags.RowBg | ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.ScrollY | ImGuiTableFlags.SizingFixedFit);
        if (!table.Success)
        {
            return;
        }

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Channel", ImGuiTableColumnFlags.WidthStretch);
        foreach (var kind in Triggers.All)
        {
            ImGui.TableSetupColumn(ColumnHeader(kind));
        }
        ImGui.TableHeadersRow();

        for (var c = 0; c < Channels.Length; c++)
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
