# In-game smoke test

Run before merging anything that changes what the plugin does in-game. Each feature adds its cases here.

Setup: deploy with `scripts/dev-plugin deploy <task-ref>` (see "Testing in-game" in the README), log in, open `/xllog`. Release the dev folder with `scripts/dev-plugin release` when done.

## Test harness

Loaded as a dev plugin, `/saymyname` also takes:

```
/saymyname inject <channel> <First> <Last> <message>
```

It prints `<message>` to chat as if player `<First> <Last>` (on your current world) had sent it on `<channel>`, one of the trigger channels in `SayMyName.Core/Triggers.cs` (`Say`, `Party`, `Linkshell3`, `CustomEmote`, ...; any case). Nothing is sent to the server: only your client sees the line.

The log shows each step, so you can tell where a message stopped:

```
Test harness: injected Say from Test Player: hello Rose
Test harness: ChatMessage got Say from Test Player: hello Rose
Test harness: ChatMessageUnhandled got Say (source=...) from Test Player
```

Malformed arguments print the usage as a chat error. Plugins installed normally (not as a dev plugin) don't have the subcommand.

Below, *Me* is your character's name and *A Friend* any other name.

## Checklist

### Loading

- [ ] `/xllog` shows `Say My Name <version>+<commit> loaded` with the commit you deployed.
- [ ] `/saymyname` and the installer's settings and open buttons each toggle the settings window.
- [ ] `/saymyname inject Say` (too few arguments) and `/saymyname inject Nowhere A Friend hi` print the usage error.

### Harness

- [ ] `/saymyname inject Say A Friend hello` shows the line in chat in Say colour with the sender name, and all three log lines appear. If `ChatMessage got` is missing, Dalamud's `Print` does not reach the chat events and trigger tests through the harness mean nothing.
- [ ] The same with `Linkshell2`, `CrossLinkshell1`, `CrossLinkshell5` and `CustomEmote`: the log shows the matching chat type each time.

### Settings persist

- [ ] Untick a channel for Name, untick the Targeted trigger, untick Match case, pick sound `<se.7>`, set quiet time to 0. Disable and re-enable the plugin in `/xlplugins`: all five are as you left them.
- [ ] Targeted and Target Speaks offer only Say and the two emote channels.
- [ ] Untick a trigger: its column header and checkboxes dim.

### Name trigger

- [ ] `/saymyname inject Say A Friend hi <Me first name>` plays the chosen sound.
- [ ] Same with your full name, and with your name inside other text (e.g. `<Me first name>!`).
- [ ] Untick Say for Name, inject on Say: no sound. Inject on Party: sound.
- [ ] Untick the Name trigger: no sound on any channel.
- [ ] With Match case on, your name in lower case: no sound. Turn it off: sound.
- [ ] With quiet time at 10 s, two injections within 10 s: one sound. After 10 s: sound again.
- [ ] Untick Play a sound: no sound.
- [ ] `/saymyname inject Say <Me> hi <Me first name>` (you as the sender): no sound.
- [ ] Real chat: say your own name in /say: no sound.
