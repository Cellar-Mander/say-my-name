using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Plugin.SelfTest;
using Dalamud.Plugin.Services;
using Newtonsoft.Json;
using SayMyName.Core;
using static SayMyName.Core.ChatChannel;

namespace SayMyName;

// Dalamud's self-test window (/xldev, Dalamud menu, Open Self-Test; group SayMyName): the solo cases of docs/smoke-test.md
// that need no second player and no look at the screen, one step per check, through injected lines
// only. Each case sets up its own settings, so any case passes when run alone. Settings are changed
// in memory while cases run and put back afterwards, never saved.
public sealed partial class TestHarness
{
    // Frames to wait for an injected line to come back through the chat events
    private const int SelfTestTimeout = 120;
    // Frames with no case run before the settings are put back: Dalamud gives a skipped case no
    // CleanUp, and runs no case while its window is closed
    private const int SelfTestIdleFrames = 60;

    private const string Friend = "Selftest Friend";
    private const string Other = "Someone Else";

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
    }

    // One check: sets up the settings, sends its lines (yielding each until it is back) and checks them
    private sealed class SelfTestCase(TestHarness harness, string name, Func<IEnumerable<SelfTestLine>> body) : ISelfTestStep
    {
        public string Name => name;
        public Func<IEnumerable<SelfTestLine>> Body => body;
        public SelfTestStepResult RunStep() => harness.RunCase(this);
        // Dalamud may call this twice per case; the second call finds nothing to put back
        public void CleanUp() => harness.EndSelfTest();
    }

    private readonly Configuration selfTestDefaults = NewDefaults();

    // The user's settings while cases run, else null
    private string? savedConfiguration;
    private SelfTestCase? runningCase;
    private IEnumerator<SelfTestLine>? caseSteps;
    private SelfTestLine? selfTestLine;
    private bool caseFailed;
    private int caseChecks;
    private int idleFrames;
    private string me = "";
    private string first = "";

    private static Configuration NewDefaults()
    {
        var defaults = new Configuration();
        defaults.Normalize();
        return defaults;
    }

    // Called by Dalamud's self-test window once per frame while the case is current
    private SelfTestStepResult RunCase(SelfTestCase test)
    {
        idleFrames = 0;
        if (runningCase != test && !StartCase(test))
        {
            return SelfTestStepResult.Fail;
        }
        if (selfTestLine is { UnhandledKind: null } waiting && ++waiting.Frames < SelfTestTimeout)
        {
            return SelfTestStepResult.Waiting;
        }

        try
        {
            if (caseSteps!.MoveNext())
            {
                selfTestLine = caseSteps.Current;
                return SelfTestStepResult.Waiting;
            }
        }
        catch (Exception e)
        {
            caseFailed = true;
            Plugin.Log.Error(e, $"Self-test FAIL: {test.Name} (exception)");
        }

        var result = caseFailed || caseChecks == 0 ? SelfTestStepResult.Fail : SelfTestStepResult.Pass;
        StopCase();
        return result;
    }

    private bool StartCase(SelfTestCase test)
    {
        // A skipped case gets no CleanUp, so it may still be running
        StopCase();
        string? refused = null;
        // The chat events and settings are touched on the framework thread
        if (!Plugin.Framework.IsInFrameworkUpdateThread)
        {
            refused = "not on the framework thread";
        }
        // The settings window shows and edits the test's settings
        else if (settingsOpen())
        {
            refused = "close the settings window first";
        }
        else if (Plugin.PlayerState.CharacterName.Split(' ') is not [_, _])
        {
            refused = "log in first";
        }
        if (refused != null)
        {
            Plugin.Log.Warning($"Self-test FAIL: {test.Name} ({refused})");
            return false;
        }

        if (savedConfiguration == null)
        {
            savedConfiguration = JsonConvert.SerializeObject(configuration);
            configuration.SavesBlocked = true;
            alertSound.Alerted = () => { if (selfTestLine != null) { selfTestLine.Alerts++; } };
            Plugin.Framework.Update += OnSelfTestUpdate;
            Plugin.Log.Debug("Self-test: settings held");
        }
        me = Plugin.PlayerState.CharacterName;
        first = me.Split(' ')[0];
        runningCase = test;
        caseSteps = test.Body().GetEnumerator();
        caseFailed = false;
        caseChecks = 0;
        return true;
    }

    private void StopCase()
    {
        caseSteps?.Dispose();
        caseSteps = null;
        runningCase = null;
        selfTestLine = null;
    }

    // Puts the user's settings back; a case that was running starts again from scratch if resumed
    private void EndSelfTest()
    {
        StopCase();
        if (savedConfiguration == null)
        {
            return;
        }

        Plugin.Framework.Update -= OnSelfTestUpdate;
        alertSound.Alerted = null;
        fakeTargeter = fakeTarget = null;
        JsonConvert.PopulateObject(savedConfiguration, configuration, new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
        configuration.SavesBlocked = false;
        savedConfiguration = null;
        Plugin.Log.Debug("Self-test: settings put back");
    }

    private void OnSelfTestUpdate(IFramework framework)
    {
        if (++idleFrames > SelfTestIdleFrames)
        {
            EndSelfTest();
        }
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
        caseChecks++;
        if (ok)
        {
            Plugin.Log.Information($"Self-test PASS: {name}");
        }
        else
        {
            caseFailed = true;
            Plugin.Log.Warning($"Self-test FAIL: {name}{(detail.Length > 0 ? $" ({detail})" : "")}");
        }
    }

    // A ChatChannel, or an XivChatType the harness injects that is no channel of its own (cross-world party)
    private SelfTestLine Send(Enum channel, string sender, string text)
    {
        var line = new SelfTestLine(text);
        if (!Inject($"{channel} {sender} {text}"))
        {
            throw new InvalidOperationException($"inject {channel} {sender} {text} refused");
        }
        return line;
    }

    // Your own lines often come with no player link; Party Icons then puts a job tag before your name
    private SelfTestLine SendTaggedOwn(ChatChannel channel, string text)
    {
        var line = new SelfTestLine(text);
        InjectLine(ChatChannels.ToChatType(channel), new SeString(new TextPayload("WAR"), new TextPayload(" "), new TextPayload(me)), text);
        return line;
    }

    // Your own unlinked line whose shown name another plugin replaces (Player Tags' random names)
    private SelfTestLine SendRenamedOwn(ChatChannel channel, string text)
    {
        var line = new SelfTestLine(text);
        rewriteSender.Add(text);
        InjectLine(ChatChannels.ToChatType(channel), new SeString(new TextPayload(me)), text);
        return line;
    }

    private static string Arrival(SelfTestLine line) => line.UnhandledKind == null ? "line never arrived" : "";

    private TriggerSettings Trigger(TriggerKind kind) => configuration.Triggers[kind];

    // Every trigger off with its default channels, quiet time 0, highlight colours at their defaults
    private void Baseline(params TriggerKind[] enabled)
    {
        foreach (var kind in Triggers.All)
        {
            configuration.Triggers[kind] = Triggers.Default(kind);
            configuration.Triggers[kind].Enabled = enabled.Contains(kind);
        }
        configuration.NameCaseSensitive = true;
        configuration.Nicknames = "";
        configuration.CustomWords = "";
        configuration.CustomWordsCaseSensitive = false;
        configuration.TargetSpeaksOnlyTargetingYou = false;
        configuration.PlaySound = true;
        configuration.CooldownSeconds = 0;
        configuration.Highlight = true;
        configuration.HighlightTextColor = selfTestDefaults.HighlightTextColor;
        configuration.HighlightGlowColor = selfTestDefaults.HighlightGlowColor;
        // Tab colouring isn't checked here, and would leave tabs coloured after the run
        configuration.HighlightTabs = false;
        fakeTargeter = fakeTarget = null;
        alertSound.ResetQuietTime();
    }

    private SelfTestCase Case(string name, Func<IEnumerable<SelfTestLine>> body) => new(this, name, body);

    // A case with no line to send
    private SelfTestCase Now(string name, Func<bool> ok, Func<string>? detail = null) => Case(name, () => NowBody(name, ok, detail));

    private IEnumerable<SelfTestLine> NowBody(string name, Func<bool> ok, Func<string>? detail)
    {
        Check(name, ok(), detail?.Invoke() ?? "");
        yield break;
    }

    // A case that sets up, sends one line and checks it once it is back (or timed out)
    private SelfTestCase OneLine(string name, Action setup, Func<SelfTestLine> send, Func<SelfTestLine, (bool Ok, string Detail)> verify) =>
        Case(name, () => OneLineBody(name, setup, send, verify));

    private IEnumerable<SelfTestLine> OneLineBody(string name, Action setup, Func<SelfTestLine> send, Func<SelfTestLine, (bool Ok, string Detail)> verify)
    {
        setup();
        var sent = send();
        yield return sent;
        var (ok, detail) = verify(sent);
        Check(name, ok, detail);
    }

    // Whether the line alerted
    private SelfTestCase Sound(string name, bool expected, Action setup, Func<SelfTestLine> send) =>
        OneLine(name, setup, send, s => (s.UnhandledKind != null && s.Alerts > 0 == expected, Arrival(s) is { Length: > 0 } a ? a : $"{s.Alerts} alerts"));

    // Which words of the line were coloured
    private SelfTestCase Coloured(string name, Func<string[]> expected, Action setup, Func<SelfTestLine> send) =>
        OneLine(name, setup, send, s => (s.UnhandledKind != null && s.Coloured.SequenceEqual(expected()), Arrival(s) is { Length: > 0 } a ? a : $"coloured [{string.Join(", ", s.Coloured)}]"));

    private IEnumerable<SelfTestLine> QuietTimeBody(string name)
    {
        Baseline(TriggerKind.Name);
        configuration.CooldownSeconds = 10;
        var firstLine = Send(Say, Friend, $"hi {first}");
        yield return firstLine;
        var secondLine = Send(Say, Friend, $"hi {first} again");
        yield return secondLine;
        Check(name, firstLine.Alerts + secondLine.Alerts == 1, $"{firstLine.Alerts} + {secondLine.Alerts} alerts");
    }

    /// <summary>The self-test cases, for Dalamud's self-test registry.</summary>
    public IEnumerable<ISelfTestStep> SelfTestSteps()
    {
        // Loading
        yield return Now("Loading: inject with too few arguments is a usage error", () => !Inject("Say"));
        yield return Now("Loading: inject on an unknown channel is a usage error", () => !Inject("Nowhere A Friend hi"));

        // Harness
        foreach (var channel in new[] { Say, Linkshell2, CrossLinkshell1, CrossLinkshell5, CustomEmote })
        {
            var type = ChatChannels.ToChatType(channel);
            yield return OneLine($"Harness: {channel} line reaches ChatMessage and ChatMessageUnhandled as {type}",
                () => Baseline(), () => Send(channel, Friend, "selftest hello"),
                s => (s.HandledKind == type && s.UnhandledKind == type, $"ChatMessage {s.HandledKind?.ToString() ?? "missing"}, ChatMessageUnhandled {s.UnhandledKind?.ToString() ?? "missing"}"));
        }

        // Name trigger
        void Name() => Baseline(TriggerKind.Name);
        void NameNoSay() { Name(); Trigger(TriggerKind.Name).Channels.Remove(Say); }
        void NameNoSayNoParty() { NameNoSay(); Trigger(TriggerKind.Name).Channels.Remove(Party); }
        void Nicknames() { Name(); configuration.Nicknames = "Zuzu\nBig Zed"; }
        yield return Sound("Name: first name on Say sounds", true, Name, () => Send(Say, Friend, $"hi {first}"));
        yield return Sound("Name: full name sounds", true, Name, () => Send(Say, Friend, $"hi {me}"));
        yield return Sound("Name: name inside other text sounds", true, Name, () => Send(Say, Friend, $"{first}!"));
        yield return Sound("Name: last letter repeated sounds", true, Name, () => Send(Say, Friend, $"hi {first}{first[^1]}{first[^1]}"));
        // Spelling out joins letters only, so a name with an apostrophe or hyphen is left to the checklist
        yield return Case("Name: spelled out with hyphens sounds", () => first.All(char.IsLetter)
            ? OneLineBody("Name: spelled out with hyphens sounds", Name, () => Send(Say, Friend, $"hi {string.Join('-', first.ToUpperInvariant().ToCharArray())}"),
                s => (s.UnhandledKind != null && s.Alerts > 0, Arrival(s) is { Length: > 0 } a ? a : $"{s.Alerts} alerts"))
            : NowBody("Name: spelled out with hyphens (not applicable: name has non-letters)", () => true, null));
        yield return Sound("Name: Say unticked, Say is silent", false, NameNoSay, () => Send(Say, Friend, $"hi {first}"));
        yield return Sound("Name: Say unticked, Party sounds", true, NameNoSay, () => Send(Party, Friend, $"hi {first}"));
        yield return Sound("Name: CrossParty sounds through Party", true, Name, () => Send(XivChatType.CrossParty, Friend, $"hi {first}"));
        yield return Sound("Name: Party unticked, CrossParty is silent", false, NameNoSayNoParty, () => Send(XivChatType.CrossParty, Friend, $"hi {first}"));
        yield return Sound("Name: trigger unticked, silent", false, () => Baseline(), () => Send(Say, Friend, $"hi {first}"));
        yield return Sound("Name: Match case on, lower-case name is silent", false, Name, () => Send(Say, Friend, $"hi {first.ToLowerInvariant()}"));
        yield return Sound("Name: Match case off, lower-case name sounds", true, () => { Name(); configuration.NameCaseSensitive = false; },
            () => Send(Say, Friend, $"hi {first.ToLowerInvariant()}"));
        yield return Case("Name: quiet time 10 s, two lines sound once", () => QuietTimeBody("Name: quiet time 10 s, two lines sound once"));
        yield return Sound("Name: Play a sound unticked, silent", false, () => { Name(); configuration.PlaySound = false; }, () => Send(Say, Friend, $"hi {first}"));
        yield return Sound("Name: your own line is silent", false, Name, () => Send(Say, me, $"hi {first}"));
        yield return Sound("Name: your own unlinked line with a job tag is silent", false, Name, () => SendTaggedOwn(Say, $"hey {first}"));
        yield return Sound("Name: your own unlinked line renamed by a plugin is silent", false, Name, () => SendRenamedOwn(Say, $"yo {first}"));
        yield return Sound("Name: nickname sounds", true, Nicknames, () => Send(Say, Friend, "hi Zuzu"));
        yield return Sound("Name: multi-word nickname, possessive sounds", true, Nicknames, () => Send(Say, Friend, "is that Big Zed's?"));
        yield return Sound("Name: nickname stretched sounds", true, Nicknames, () => Send(Say, Friend, "Zuzuuu!"));
        yield return Sound("Name: part of a multi-word nickname is silent", false, Nicknames, () => Send(Say, Friend, "Zed"));
        yield return Sound("Name: Match case on, lower-case nickname is silent", false, Nicknames, () => Send(Say, Friend, "hi zuzu"));
        yield return Coloured("Highlight: nickname coloured without possessive", () => ["Big Zed"], Nicknames, () => Send(Say, Friend, "Big Zed's turn"));

        // Custom trigger
        void Custom() { Baseline(TriggerKind.Custom); configuration.CustomWords = "raid\ngood morning"; }
        void CustomCase() { Custom(); configuration.CustomWordsCaseSensitive = true; }
        void CustomNoSay() { Custom(); Trigger(TriggerKind.Custom).Channels.Remove(Say); }
        foreach (var text in new[] { "raid tonight?", "Good morning!", "RAID" })
        {
            yield return Sound($"Custom: '{text}' sounds", true, Custom, () => Send(Say, Friend, text));
        }
        foreach (var text in new[] { "afraid", "good", "morning", "raiiid" })
        {
            yield return Sound($"Custom: '{text}' is silent", false, Custom, () => Send(Say, Friend, text));
        }
        yield return Sound("Custom: Match case on, 'RAID' is silent", false, CustomCase, () => Send(Say, Friend, "RAID"));
        yield return Sound("Custom: Match case on, 'raid' sounds", true, CustomCase, () => Send(Say, Friend, "raid"));
        yield return Sound("Custom: Say unticked, Say is silent", false, CustomNoSay, () => Send(Say, Friend, "raid"));
        yield return Sound("Custom: Say unticked, Party sounds", true, CustomNoSay, () => Send(Party, Friend, "raid"));
        yield return Sound("Custom: your own line is silent", false, Custom, () => Send(Say, me, "raid"));
        yield return Sound("Custom: your own unlinked line with a job tag is silent", false, Custom, () => SendTaggedOwn(Say, "raid time"));
        yield return Sound("Custom: empty list is silent", false, () => Baseline(TriggerKind.Custom), () => Send(Say, Friend, "raid"));
        yield return Sound("Custom: trigger unticked, silent", false, () => { Baseline(); configuration.CustomWords = "raid"; }, () => Send(Say, Friend, "raid"));

        // Highlight
        void Highlight() { Baseline(TriggerKind.Name, TriggerKind.Custom, TriggerKind.You); configuration.CustomWords = "raid"; }
        void HighlightNoGlow() { Highlight(); configuration.HighlightGlowColor = Configuration.NoColor; }
        void HighlightTargeted() { Highlight(); fakeTargeter = Friend; }
        yield return Coloured("Highlight: name and custom word coloured", () => [first, "raid"], Highlight, () => Send(Say, Friend, $"hi {first}, raid?"));
        yield return OneLine("Highlight: text colour and glow both applied", Highlight, () => Send(Say, Friend, $"hi {first}, raid?"),
            s => (s.TextColour && s.Glow, Arrival(s)));
        yield return Coloured("Highlight: possessive colours the name without 's", () => [first, "raid"], Highlight, () => Send(Say, Friend, $"{first}'s raid"));
        yield return Coloured("Highlight: name and word inside other words not coloured", () => [], Highlight, () => Send(Say, Friend, $"Prim{first.ToLowerInvariant()} afraid"));
        yield return OneLine("Highlight: glow None, text colour only", HighlightNoGlow, () => Send(Say, Friend, $"hi {first}"),
            s => (s.TextColour && !s.Glow && s.Coloured.SequenceEqual([first]), Arrival(s)));
        yield return Coloured("Highlight: text and glow None, line unchanged", () => [], () => { HighlightNoGlow(); configuration.HighlightTextColor = Configuration.NoColor; },
            () => Send(Say, Friend, $"hi {first}"));
        yield return OneLine("Highlight: Play a sound unticked, coloured without sound", () => { Highlight(); configuration.PlaySound = false; }, () => Send(Say, Friend, $"hi {first}"),
            s => (s.Alerts == 0 && s.Coloured.SequenceEqual([first]), Arrival(s)));
        yield return OneLine("Highlight: Highlight unticked, sound without colour", () => { Highlight(); configuration.Highlight = false; }, () => Send(Say, Friend, $"hi {first}"),
            s => (s.Alerts > 0 && s.Coloured.Count == 0, Arrival(s)));
        void HighlightNameNoSay() { Highlight(); Trigger(TriggerKind.Name).Channels.Remove(Say); }
        yield return Coloured("Highlight: Say unticked for Name, Say not coloured", () => [], HighlightNameNoSay, () => Send(Say, Friend, $"hi {first}"));
        yield return Coloured("Highlight: Say unticked for Name, Party coloured", () => [first], HighlightNameNoSay, () => Send(Party, Friend, $"hi {first}"));
        yield return Coloured("Highlight: your own line not coloured", () => [], Highlight, () => Send(Say, me, $"hi {first}"));
        yield return Coloured("Highlight: 'you' words coloured from a sender targeting you", () => ["youuu", "yours"], HighlightTargeted, () => Send(Say, Friend, "thank youuu, is this yours?"));
        yield return Coloured("Highlight: 'you' words coloured on CustomEmote", () => ["youuu", "yours"], HighlightTargeted, () => Send(CustomEmote, Friend, "thank youuu, is this yours?"));
        yield return Coloured("Highlight: 'you' words not coloured from a sender not targeting you", () => [], HighlightTargeted, () => Send(Say, Other, "thank you"));
        yield return Coloured("Highlight: Play a sound unticked, 'you' still coloured", () => ["you"], () => { HighlightTargeted(); configuration.PlaySound = false; },
            () => Send(Say, Friend, "thank you"));

        // You trigger, solo
        void You() { Baseline(TriggerKind.You); fakeTargeter = Friend; }
        foreach (var text in new[] { "thank you", "is this your bag", "it is yours", "you're welcome" })
        {
            yield return Sound($"You: '{text}' from a sender targeting you sounds", true, You, () => Send(Say, Friend, text));
        }
        yield return Sound("You: CustomEmote sounds", true, You, () => Send(CustomEmote, Friend, "waves at you"));
        yield return Sound("You: no 'you' word is silent", false, You, () => Send(Say, Friend, "hello"));
        yield return Sound("You: sender not targeting you is silent", false, You, () => Send(Say, Other, "thank you"));
        foreach (var channel in new Enum[] { Party, XivChatType.CrossParty })
        {
            yield return Sound($"You: {channel} sounds", true, You, () => Send(channel, Friend, "thank you"));
        }
        yield return Sound("You: Alliance is silent by default", false, You, () => Send(Alliance, Friend, "thank you"));
        yield return Sound("You: Alliance ticked, Alliance sounds", true, () => { You(); Trigger(TriggerKind.You).Channels.Add(Alliance); }, () => Send(Alliance, Friend, "thank you"));
        yield return Sound("You: Linkshell1 is silent", false, You, () => Send(Linkshell1, Friend, "thank you"));
        void YouNoParty() { You(); Trigger(TriggerKind.You).Channels.Remove(Party); }
        yield return Sound("You: Party unticked, Party is silent", false, YouNoParty, () => Send(Party, Friend, "thank you"));
        yield return Sound("You: Party unticked, Say sounds", true, YouNoParty, () => Send(Say, Friend, "thank you"));
        yield return Sound("You: Say unticked, Say is silent", false, () => { You(); Trigger(TriggerKind.You).Channels.Remove(Say); }, () => Send(Say, Friend, "thank you"));
        yield return Sound("You: not pretending, silent", false, () => Baseline(TriggerKind.You), () => Send(Say, Friend, "thank you"));

        // Target Speaks trigger, solo
        var fresh = selfTestDefaults.Triggers[TriggerKind.TargetSpeaks];
        yield return Now("Target Speaks: on by default", () => fresh.Enabled, () => $"enabled={fresh.Enabled}");
        void TargetSpeaks() { Baseline(TriggerKind.TargetSpeaks); fakeTarget = Friend; }
        void OnlyTargetingYou() { TargetSpeaks(); configuration.TargetSpeaksOnlyTargetingYou = true; }
        void OnlyTargetingYouTargeted() { OnlyTargetingYou(); fakeTargeter = Friend; }
        yield return Sound("Target Speaks: Say sounds", true, TargetSpeaks, () => Send(Say, Friend, "hello"));
        yield return OneLine("Target Speaks: line not coloured", TargetSpeaks, () => Send(Say, Friend, "hello"), s => (s.UnhandledKind != null && s.Coloured.Count == 0, Arrival(s)));
        yield return Sound("Target Speaks: CustomEmote sounds", true, TargetSpeaks, () => Send(CustomEmote, Friend, "waves"));
        yield return Sound("Target Speaks: StandardEmote is silent", false, TargetSpeaks, () => Send(StandardEmote, Friend, "waves"));
        yield return Sound("Target Speaks: someone else is silent", false, TargetSpeaks, () => Send(Say, Other, "hello"));
        yield return Sound("Target Speaks: Party sounds", true, TargetSpeaks, () => Send(Party, Friend, "hello"));
        yield return Sound("Target Speaks: Linkshell1 sounds", true, TargetSpeaks, () => Send(Linkshell1, Friend, "hello"));
        yield return Sound("Target Speaks: Play a sound unticked, silent", false, () => { TargetSpeaks(); configuration.PlaySound = false; }, () => Send(Say, Friend, "hello"));
        yield return Now("Target Speaks: only while targeting you by default", () => selfTestDefaults.TargetSpeaksOnlyTargetingYou);
        yield return Sound("Target Speaks: only while targeting you, not targeting you is silent", false, OnlyTargetingYou, () => Send(Say, Friend, "hello"));
        yield return Sound("Target Speaks: only while targeting you, targeting you sounds", true, OnlyTargetingYouTargeted, () => Send(Say, Friend, "hello"));
        yield return Sound("Target Speaks: only while targeting you, Linkshell1 sounds", true, OnlyTargetingYouTargeted, () => Send(Linkshell1, Friend, "hello"));
        yield return Sound("Target Speaks: only while targeting you, targeting you but not your target is silent", false,
            () => { OnlyTargetingYouTargeted(); fakeTarget = null; }, () => Send(Say, Friend, "hello"));
        yield return Sound("Target Speaks: not pretending, silent", false, () => Baseline(TriggerKind.TargetSpeaks), () => Send(Say, Friend, "hello"));
    }
}
