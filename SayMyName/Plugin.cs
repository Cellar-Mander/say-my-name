using System;
using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using SayMyName.Windows;

namespace SayMyName;

public sealed class Plugin : IDalamudPlugin
{
    [PluginService] internal static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] internal static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] internal static IClientState ClientState { get; private set; } = null!;
    [PluginService] internal static IPlayerState PlayerState { get; private set; } = null!;
    [PluginService] internal static IPluginLog Log { get; private set; } = null!;
    [PluginService] internal static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] internal static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] internal static IGameInteropProvider GameInterop { get; private set; } = null!;
    [PluginService] internal static IFramework Framework { get; private set; } = null!;
    [PluginService] internal static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
    [PluginService] internal static IDataManager DataManager { get; private set; } = null!;
    [PluginService] internal static IGameConfig GameConfig { get; private set; } = null!;

    private const string CommandName = "/saymyname";

    public Configuration Configuration { get; init; }
    public AlertSound AlertSound { get; init; }
    public ChatHighlighter Highlighter { get; init; }
    private TabHighlighter TabHighlighter { get; init; }
    private TextAlert TextAlert { get; init; }
    private TargetedAlert TargetedAlert { get; init; }
    // Dev plugin only: faking chat and targeting is a testing tool, not a user feature
    private TestHarness? TestHarness { get; init; }

    public readonly WindowSystem WindowSystem = new("SayMyName");
    private ConfigWindow ConfigWindow { get; init; }

    public Plugin()
    {
        Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
        Configuration.Normalize();

        // Commit hash included, to check which checkout's build the game has loaded
        Log.Information($"Say My Name {typeof(Plugin).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion} loaded");

        AlertSound = new AlertSound(Configuration);
        Highlighter = new ChatHighlighter(Configuration);
        TabHighlighter = new TabHighlighter(Configuration);
        TextAlert = new TextAlert(Configuration, AlertSound, Highlighter, TabHighlighter);
        TargetedAlert = new TargetedAlert(Configuration, AlertSound, Highlighter, TabHighlighter);
        if (PluginInterface.IsDev)
        {
            TestHarness = new TestHarness(Configuration, AlertSound, TargetedAlert, () => ConfigWindow?.IsOpen == true);
        }

        ConfigWindow = new ConfigWindow(this);
        WindowSystem.AddWindow(ConfigWindow);

        CommandManager.AddHandler(CommandName, new CommandInfo(OnCommand)
        {
            HelpMessage = TestHarness == null
                ? "Open the Say My Name settings"
                : $"Open the Say My Name settings, or (dev plugin) {CommandName} {TestHarness.Usage}"
        });

        // Tell the UI system that we want our windows to be drawn through the window system
        PluginInterface.UiBuilder.Draw += WindowSystem.Draw;

        // The plugin installer's settings and open buttons both lead to the settings window
        PluginInterface.UiBuilder.OpenConfigUi += ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi += ToggleConfigUi;
    }

    public void Dispose()
    {
        // Unregister all actions to not leak anything during disposal of plugin
        PluginInterface.UiBuilder.Draw -= WindowSystem.Draw;
        PluginInterface.UiBuilder.OpenConfigUi -= ToggleConfigUi;
        PluginInterface.UiBuilder.OpenMainUi -= ToggleConfigUi;

        WindowSystem.RemoveAllWindows();

        ConfigWindow.Dispose();
        // The harness first: it unhooks itself from TargetedAlert
        TestHarness?.Dispose();
        TextAlert.Dispose();
        TargetedAlert.Dispose();
        TabHighlighter.Dispose();

        CommandManager.RemoveHandler(CommandName);
    }

    private void OnCommand(string command, string args)
    {
        if (TestHarness?.Handle(args) != true)
        {
            ToggleConfigUi();
        }
    }

    public void ToggleConfigUi() => ConfigWindow.Toggle();
}
