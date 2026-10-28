using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using SayMyName.Core;
using static SayMyName.Core.ChatChannel;

namespace SayMyName;

// `/saymyname selftest`: runs the solo cases of docs/smoke-test.md that need no second player and
// no look at the screen, through injected lines only, and logs PASS/FAIL per case. Settings are
// changed in memory for the run and put back afterwards, never saved.
public sealed partial class TestHarness
{
    // Frames to wait for an injected line to come back through the chat events
    private const int SelfTestTimeout = 120;

    private sealed class SelfTestLine(string text)
    {
        public string Text { get; } = text;
        public XivChatType? HandledKind { get; set; }
        public XivChatType? UnhandledKind { get; set; }
        public List<string> Coloured { get; } = [];
        public bool TextColour { get; set; }
        public bool Glow { get; set; }
        public int Alerts { get; set; }
        public int Frames { get; set; }
        public Action? Verify { get; set; }
    }

    private IEnumerator<SelfTestLine?>? selfTest;
    private SelfTestLine? selfTestLine;
    private string savedConfiguration = "";
    private int passed;
    private int failed;

    private bool StartSelfTest(string args)
    {
        if (args.Length > 0)
        {
            return false;
        }
        if (selfTest != null)
        {
            Plugin.ChatGui.PrintError("Test harness: self-test already running");
            return true;
        }
        // Closing the window saves, which would save the test's settings
        if (settingsOpen())
        {
            Plugin.ChatGui.PrintError("Test harness: close the settings window before the self-test");
            return true;
        }
        if (Plugin.PlayerState.CharacterName.Split(' ') is not [_, _])
        {
            Plugin.ChatGui.PrintError("Test harness: log in before the self-test");
            return true;
        }

        savedConfiguration = JsonConvert.SerializeObject(configuration);
        passed = failed = 0;
        selfTestLine = null;
        selfTest = SelfTestScript();
        alertSound.Alerted = () => { if (selfTestLine != null) { selfTestLine.Alerts++; } };
        Plugin.Framework.Update += OnSelfTestUpdate;
        Plugin.Log.Information("Self-test: started");
        Plugin.ChatGui.Print("Test harness: self-test running, see /xllog");
        return true;
    }

    private void StopSelfTest()
    {
        if (selfTest == null)
        {
            return;
        }

        Plugin.Framework.Update -= OnSelfTestUpdate;
        selfTest.Dispose();
        selfTest = null;
        selfTestLine = null;
        alertSound.Alerted = null;
        fakeTargeter = fakeTarget = null;
        JsonConvert.PopulateObject(savedConfiguration, configuration, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        var summary = $"Self-test: {passed} passed, {failed} failed";
        Plugin.Log.Information(summary);
        Plugin.ChatGui.Print($"Test harness: {summary}");
    }

    // One step per frame: a step injects a line and waits until it has come back (or timed out)
    private void OnSelfTestUpdate(IFramework framework)
    {
        if (selfTestLine is { UnhandledKind: null } waiting && ++waiting.Frames < SelfTestTimeout)
        {
            return;
        }

        try
        {
            selfTestLine?.Verify?.Invoke();
            if (selfTest!.MoveNext())
            {
                selfTestLine = selfTest.Current;
                return;
            }
        }
        catch (Exception e)
        {
            failed++;
            Plugin.Log.Error(e, "Self-test FAIL: exception");
        }
        StopSelfTest();
    }

    // Called by the harness's chat handlers, which run after the triggers' (subscribed earlier)
    private void SelfTestSaw(IChatMessage message, bool unhandled)
    {
        if (selfTestLine is not { } line || message.Message.TextValue != line.Text)
        {
            return;
        }
        if (!unhandled)
        {
            line.HandledKind = message.LogKind;
            return;
        }

        line.UnhandledKind = message.LogKind;
        // ChatHighlighter puts colour payloads right before each highlighted piece
        Payload? previous = null;
        foreach (var payload in message.Message.Payloads)
        {
            line.TextColour |= payload is UIForegroundPayload { IsEnabled: true };
            line.Glow |= payload is UIGlowPayload { IsEnabled: true };
            if (payload is TextPayload text && previous is UIForegroundPayload { IsEnabled: true } or UIGlowPayload { IsEnabled: true })
            {
                line.Coloured.Add(text.Text ?? "");
            }
            previous = payload;
        }
    }

    private void Check(string name, bool ok, string detail = "")
    {
        if (ok)
        {
            passed++;
            Plugin.Log.Information($"Self-test PASS: {name}");
        }
        else
        {
            failed++;
            Plugin.Log.Warning($"Self-test FAIL: {name}{(detail.Length > 0 ? $" ({detail})" : "")}");
        }
    }

    private IEnumerator<SelfTestLine?> SelfTestScript()
    {
        var me = Plugin.PlayerState.CharacterName;
        var first = me.Split(' ')[0];
        const string friend = "Selftest Friend";
        const string other = "Someone Else";
        var defaults = new Configuration();
        defaults.Normalize();
        SelfTestLine line = null!;

        SelfTestLine Send(ChatChannel channel, string sender, string text)
        {
            line = new SelfTestLine(text);
            if (!Inject($"{channel} {sender} {text}"))
            {
                throw new InvalidOperationException($"inject {channel} {sender} {text} refused");
            }
            return line;
        }

        string Arrival() => line.UnhandledKind == null ? "line never arrived" : "";
        // Send a line and, once it is back, check whether it alerted or which words were coloured
        SelfTestLine Sound(string name, bool expected, ChatChannel channel, string sender, string text)
        {
            var sent = Send(channel, sender, text);
            sent.Verify = () => Check(name, sent.UnhandledKind != null && sent.Alerts > 0 == expected, Arrival() is { Length: > 0 } a ? a : $"{sent.Alerts} alerts");
            return sent;
        }
        SelfTestLine Coloured(string name, string[] expected, ChatChannel channel, string sender, string text)
        {
            var sent = Send(channel, sender, text);
            sent.Verify = () => Check(name, sent.UnhandledKind != null && sent.Coloured.SequenceEqual(expected), Arrival() is { Length: > 0 } a ? a : $"coloured [{string.Join(", ", sent.Coloured)}]");
            return sent;
        }
        TriggerSettings Trigger(TriggerKind kind) => configuration.Triggers[kind];

        // Every trigger off with its default channels, quiet time 0, highlight colours at their defaults
        void Baseline(params TriggerKind[] enabled)
        {
            foreach (var kind in Triggers.All)
            {
                configuration.Triggers[kind] = Triggers.Default(kind);
                configuration.Triggers[kind].Enabled = enabled.Contains(kind);
            }
            configuration.NameCaseSensitive = true;
            configuration.CustomWords = "";
            configuration.CustomWordsCaseSensitive = false;
            configuration.PlaySound = true;
            configuration.CooldownSeconds = 0;
            configuration.Highlight = true;
            configuration.HighlightTextColor = defaults.HighlightTextColor;
            configuration.HighlightGlowColor = defaults.HighlightGlowColor;
            // Tab colouring isn't checked here, and would leave tabs coloured after the run
            configuration.HighlightTabs = false;
            fakeTargeter = fakeTarget = null;
        }

        // Loading
        Check("Loading: inject with too few arguments is a usage error", !Inject("Say"));
        Check("Loading: inject on an unknown channel is a usage error", !Inject("Nowhere A Friend hi"));

        // Harness
        Baseline();
        foreach (var channel in new[] { Say, Linkshell2, CrossLinkshell1, CrossLinkshell5, CustomEmote })
        {
            yield return Send(channel, friend, "selftest hello");
            var type = ChatChannels.ToChatType(channel);
            Check($"Harness: {channel} line reaches ChatMessage and ChatMessageUnhandled as {type}",
                line.HandledKind == type && line.UnhandledKind == type, $"ChatMessage {line.HandledKind?.ToString() ?? "missing"}, ChatMessageUnhandled {line.UnhandledKind?.ToString() ?? "missing"}");
        }

        // Name trigger
        Baseline(TriggerKind.Name);
        yield return Sound("Name: first name on Say sounds", true, Say, friend, $"hi {first}");
        yield return Sound("Name: full name sounds", true, Say, friend, $"hi {me}");
        yield return Sound("Name: name inside other text sounds", true, Say, friend, $"{first}!");
        yield return Sound("Name: last letter repeated sounds", true, Say, friend, $"hi {first}{first[^1]}{first[^1]}");
        // Spelling out joins letters only, so a name with an apostrophe or hyphen is left to the checklist
        if (first.All(char.IsLetter))
        {
            yield return Sound("Name: spelled out with hyphens sounds", true, Say, friend, $"hi {string.Join('-', first.ToUpperInvariant().ToCharArray())}");
        }
        Trigger(TriggerKind.Name).Channels.Remove(Say);
        yield return Sound("Name: Say unticked, Say is silent", false, Say, friend, $"hi {first}");
        yield return Sound("Name: Say unticked, Party sounds", true, Party, friend, $"hi {first}");
        yield return Sound("Name: CrossParty sounds through Party", true, CrossParty, friend, $"hi {first}");
        Trigger(TriggerKind.Name).Channels.Remove(Party);
        yield return Sound("Name: Party unticked, CrossParty is silent", false, CrossParty, friend, $"hi {first}");
        Baseline();
        yield return Sound("Name: trigger unticked, silent", false, Say, friend, $"hi {first}");
        Baseline(TriggerKind.Name);
        yield return Sound("Name: Match case on, lower-case name is silent", false, Say, friend, $"hi {first.ToLowerInvariant()}");
        configuration.NameCaseSensitive = false;
        yield return Sound("Name: Match case off, lower-case name sounds", true, Say, friend, $"hi {first.ToLowerInvariant()}");
        configuration.CooldownSeconds = 10;
        alertSound.ResetQuietTime();
        yield return Send(Say, friend, $"hi {first}");
        var firstAlerts = line.Alerts;
        yield return Send(Say, friend, $"hi {first} again");
        Check("Name: quiet time 10 s, two lines sound once", firstAlerts + line.Alerts == 1, $"{firstAlerts} + {line.Alerts} alerts");
        configuration.CooldownSeconds = 0;
        configuration.PlaySound = false;
        yield return Sound("Name: Play a sound unticked, silent", false, Say, friend, $"hi {first}");
        configuration.PlaySound = true;
        yield return Sound("Name: your own line is silent", false, Say, me, $"hi {first}");

        // Custom trigger
        Baseline(TriggerKind.Custom);
        configuration.CustomWords = "raid\ngood morning";
        foreach (var text in new[] { "raid tonight?", "Good morning!", "RAID" })
        {
            yield return Sound($"Custom: '{text}' sounds", true, Say, friend, text);
        }
        foreach (var text in new[] { "afraid", "good", "morning", "raiiid" })
        {
            yield return Sound($"Custom: '{text}' is silent", false, Say, friend, text);
        }
        configuration.CustomWordsCaseSensitive = true;
        yield return Sound("Custom: Match case on, 'RAID' is silent", false, Say, friend, "RAID");
        yield return Sound("Custom: Match case on, 'raid' sounds", true, Say, friend, "raid");
        configuration.CustomWordsCaseSensitive = false;
        Trigger(TriggerKind.Custom).Channels.Remove(Say);
        yield return Sound("Custom: Say unticked, Say is silent", false, Say, friend, "raid");
        yield return Sound("Custom: Say unticked, Party sounds", true, Party, friend, "raid");
        Trigger(TriggerKind.Custom).Channels.Add(Say);
        yield return Sound("Custom: your own line is silent", false, Say, me, "raid");
        configuration.CustomWords = "";
        yield return Sound("Custom: empty list is silent", false, Say, friend, "raid");
        configuration.CustomWords = "raid";
        Trigger(TriggerKind.Custom).Enabled = false;
        yield return Sound("Custom: trigger unticked, silent", false, Say, friend, "raid");

        // Highlight
        Baseline(TriggerKind.Name, TriggerKind.Custom, TriggerKind.You);
        configuration.CustomWords = "raid";
        yield return Coloured("Highlight: name and custom word coloured", [first, "raid"], Say, friend, $"hi {first}, raid?");
        Check("Highlight: text colour and glow both applied", line.TextColour && line.Glow);
        yield return Coloured("Highlight: possessive colours the name without 's", [first, "raid"], Say, friend, $"{first}'s raid");
        yield return Coloured("Highlight: name and word inside other words not coloured", [], Say, friend, $"Prim{first.ToLowerInvariant()} afraid");
        configuration.HighlightGlowColor = Configuration.NoColor;
        yield return Send(Say, friend, $"hi {first}");
        Check("Highlight: glow None, text colour only", line.TextColour && !line.Glow && line.Coloured.SequenceEqual([first]));
        configuration.HighlightTextColor = Configuration.NoColor;
        yield return Coloured("Highlight: text and glow None, line unchanged", [], Say, friend, $"hi {first}");
        configuration.HighlightTextColor = defaults.HighlightTextColor;
        configuration.HighlightGlowColor = defaults.HighlightGlowColor;
        configuration.PlaySound = false;
        yield return Send(Say, friend, $"hi {first}");
        Check("Highlight: Play a sound unticked, coloured without sound", line.Alerts == 0 && line.Coloured.SequenceEqual([first]));
        configuration.PlaySound = true;
        configuration.Highlight = false;
        yield return Send(Say, friend, $"hi {first}");
        Check("Highlight: Highlight unticked, sound without colour", line.Alerts > 0 && line.Coloured.Count == 0);
        configuration.Highlight = true;
        Trigger(TriggerKind.Name).Channels.Remove(Say);
        yield return Coloured("Highlight: Say unticked for Name, Say not coloured", [], Say, friend, $"hi {first}");
        yield return Coloured("Highlight: Say unticked for Name, Party coloured", [first], Party, friend, $"hi {first}");
        Trigger(TriggerKind.Name).Channels.Add(Say);
        yield return Coloured("Highlight: your own line not coloured", [], Say, me, $"hi {first}");
        fakeTargeter = friend;
        yield return Coloured("Highlight: 'you' words coloured from a sender targeting you", ["youuu", "yours"], Say, friend, "thank youuu, is this yours?");
        yield return Coloured("Highlight: 'you' words coloured on CustomEmote", ["youuu", "yours"], CustomEmote, friend, "thank youuu, is this yours?");
        yield return Coloured("Highlight: 'you' words not coloured from a sender not targeting you", [], Say, other, "thank you");
        configuration.PlaySound = false;
        yield return Coloured("Highlight: Play a sound unticked, 'you' still coloured", ["you"], Say, friend, "thank you");

        // You trigger, solo
        Baseline(TriggerKind.You);
        fakeTargeter = friend;
        foreach (var text in new[] { "thank you", "is this your bag", "it is yours", "you're welcome" })
        {
            yield return Sound($"You: '{text}' from a sender targeting you sounds", true, Say, friend, text);
        }
        yield return Sound("You: CustomEmote sounds", true, CustomEmote, friend, "waves at you");
        yield return Sound("You: no 'you' word is silent", false, Say, friend, "hello");
        yield return Sound("You: sender not targeting you is silent", false, Say, other, "thank you");
        foreach (var channel in new[] { Party, Alliance, CrossParty })
        {
            yield return Sound($"You: {channel} sounds", true, channel, friend, "thank you");
        }
        yield return Sound("You: Linkshell1 is silent", false, Linkshell1, friend, "thank you");
        Trigger(TriggerKind.You).Channels.Remove(Party);
        yield return Sound("You: Party unticked, Party is silent", false, Party, friend, "thank you");
        yield return Sound("You: Party unticked, Say sounds", true, Say, friend, "thank you");
        Trigger(TriggerKind.You).Channels.Add(Party);
        Trigger(TriggerKind.You).Channels.Remove(Say);
        yield return Sound("You: Say unticked, Say is silent", false, Say, friend, "thank you");
        Trigger(TriggerKind.You).Channels.Add(Say);
        fakeTargeter = null;
        yield return Sound("You: not pretending, silent", false, Say, friend, "thank you");

        // Target Speaks trigger, solo
        var fresh = defaults.Triggers[TriggerKind.TargetSpeaks];
        Check("Target Speaks: off by default with Say and Custom emotes",
            !fresh.Enabled && fresh.Channels.SetEquals([Say, CustomEmote]), $"enabled={fresh.Enabled} channels={string.Join(",", fresh.Channels)}");
        Baseline(TriggerKind.TargetSpeaks);
        fakeTarget = friend;
        yield return Sound("Target Speaks: Say sounds", true, Say, friend, "hello");
        Check("Target Speaks: line not coloured", line.Coloured.Count == 0);
        yield return Sound("Target Speaks: CustomEmote sounds", true, CustomEmote, friend, "waves");
        yield return Sound("Target Speaks: StandardEmote is silent", false, StandardEmote, friend, "waves");
        yield return Sound("Target Speaks: someone else is silent", false, Say, other, "hello");
        yield return Sound("Target Speaks: Party is silent", false, Party, friend, "hello");
        Trigger(TriggerKind.TargetSpeaks).Channels.Remove(Say);
        yield return Sound("Target Speaks: Say unticked, silent", false, Say, friend, "hello");
        Trigger(TriggerKind.TargetSpeaks).Channels.Add(Say);
        configuration.PlaySound = false;
        yield return Sound("Target Speaks: Play a sound unticked, silent", false, Say, friend, "hello");
        configuration.PlaySound = true;
        fakeTarget = null;
        yield return Sound("Target Speaks: not pretending, silent", false, Say, friend, "hello");
    }
}
