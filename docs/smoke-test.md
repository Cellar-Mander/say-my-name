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

For the Emote and You triggers, two more:

```
/saymyname targeting-me <First> <Last>   # injected lines from this name count as targeting you
/saymyname targeting-me                  # stop pretending
/saymyname emotes                        # toggle logging every emote the Emote trigger sees
```

For the Target Speaks trigger:

```
/saymyname my-target <First> <Last>      # injected lines from this name count as from your current target
/saymyname my-target                     # stop pretending
```

`targeting-me` only skips the search for the sender among nearby players; everything else in the trigger runs as normal. With `emotes` on, each emote nearby logs a line like `Test harness: emote 105 from A Friend at Some Player, logged=True`, with `at you` when it is aimed at you.

To try colouring chat tab names (task 7d98e301):

```
/saymyname tabcolor <tab 1-5> <RRGGBB>        # colour the tab's name once; logs when the game changes it back
/saymyname tabcolor <tab 1-5> <RRGGBB> keep   # also re-apply it every frame
/saymyname tabcolor off                       # restore the original colour
/saymyname tabwatch                           # for 10 s, log each tab's message count and colours when they change
```

To run the automatable solo cases below in one go:

```
/saymyname selftest
```

Close the settings window first. It injects each case's line, waits for it to come back through the chat events, and logs `Self-test PASS: <case>` or `Self-test FAIL: <case> (<detail>)`, then `Self-test: <n> passed, <m> failed` (also printed in chat). It changes the settings in memory for the run, mutes the alert sound while counting alerts, and puts the settings back afterwards without saving them. Don't change settings while it runs. Cases marked *(self-test)* need no other check; those marked *(self-test: ...)* are covered only for the part named, so finish them by hand. Unmarked cases stay manual: how things look, settings persistence, and anything needing a second player or real chat. On Linux, after a deploy, `grep 'Self-test' ~/.xlcore/logs/dalamud.log | tail` shows the result.

Malformed arguments print the usage as a chat error. Plugins installed normally (not as a dev plugin) don't have these subcommands.

Below, *Me* is your character's name and *A Friend* any other name.

## Checklist

### Loading

- [ ] `/xllog` shows `Say My Name <version>+<commit> loaded` with the commit you deployed.
- [ ] `/saymyname` and the installer's settings and open buttons each toggle the settings window.
- [ ] `/saymyname inject Say` (too few arguments) and `/saymyname inject Nowhere A Friend hi` print the usage error. *(self-test: both are rejected; look for the chat error yourself)*

### Harness

- [ ] `/saymyname inject Say A Friend hello` shows the line in chat in Say colour with the sender name, and all three log lines appear. If `ChatMessage got` is missing, Dalamud's `Print` does not reach the chat events and trigger tests through the harness mean nothing. *(self-test: the log lines and chat type; look at the colour yourself)*
- [ ] The same with `Linkshell2`, `CrossLinkshell1`, `CrossLinkshell5` and `CustomEmote`: the log shows the matching chat type each time. *(self-test)*

### Settings persist

- [ ] Untick a channel for Name, untick the You trigger, untick Match case, pick sound `<se.7>`, set quiet time to 0. Disable and re-enable the plugin in `/xlplugins`: all five are as you left them.
- [ ] The channel table has no Emotes row and no Emote column; You offers only Say, Party, Alliance and Custom emotes; Target Speaks only Say and Custom emotes. Say and Custom emotes come first, then Party and Alliance.
- [ ] With a config saved before Emote and You existed (Targeted unticked, Emotes unticked for it): after loading, both Emote and You are unticked, and You keeps Say and Custom emotes.
- [ ] With a config saved before the Emotes row was removed, with the Emote trigger ticked but Emotes unticked for it: after loading, the Emote trigger is unticked.
- [ ] Untick a trigger: its column header and checkboxes dim.

### Settings sections

- [ ] On first open (no saved config, or one saved before this feature), only Alert when is expanded; Sound and Channels show just their headers.
- [ ] Collapse Alert when and expand Channels. Disable and re-enable the plugin in `/xlplugins` and reopen the window: only Channels is open.
- [ ] Collapse all three and reload: all stay closed.
- [ ] With all three open at the minimum window size, the channel table scrolls inside the window and its header row stays visible.

### Name trigger

- [ ] `/saymyname inject Say A Friend hi <Me first name>` plays the chosen sound. *(self-test)*
- [ ] Same with your full name, and with your name inside other text (e.g. `<Me first name>!`). *(self-test)*
- [ ] Untick Say for Name, inject on Say: no sound. Inject on Party: sound. *(self-test)*
- [ ] The channel table has a Party row and no Cross-world Party row. Inject on CrossParty: sound. Untick Party for Name, inject on CrossParty: no sound. *(self-test)*
- [ ] Untick the Name trigger: no sound on any channel. *(self-test)*
- [ ] With Match case on, your name in lower case: no sound. Turn it off: sound. *(self-test)*
- [ ] Your first name stretched (its last letter repeated, or a vowel repeated 3 times, e.g. `Aliiice`) and spelled out (`A-L-I-C-E`, `a l i c e`): sound. With a vowel doubled inside (`Aliice`): no sound. *(self-test: last letter repeated, and spelled out with hyphens for a name of letters only)*
- [ ] With quiet time at 10 s, two injections within 10 s: one sound. After 10 s: sound again. *(self-test: the two lines within 10 s)*
- [ ] Untick Play a sound: no sound. *(self-test)*
- [ ] `/saymyname inject Say <Me> hi <Me first name>` (you as the sender): no sound. *(self-test)*
- [ ] Real chat: say your own name in /say: no sound.

### Custom trigger

Turn off the Name trigger first, so only custom words can sound.

- [ ] Enter `raid` and `good morning` on separate lines. `/saymyname inject Say A Friend raid tonight?`: sound. Same with `Good morning!` and `RAID`. *(self-test)*
- [ ] `inject Say A Friend afraid`, `... good`, `... morning`, `... raiiid`: no sound. *(self-test)*
- [ ] Tick Match case for custom words: `RAID` no longer sounds, `raid` still does. *(self-test)*
- [ ] Untick Say for Custom, `inject Say A Friend raid`: no sound. Inject on Party: sound. *(self-test)*
- [ ] `/saymyname inject Say <Me> raid` (you as the sender): no sound. *(self-test)*
- [ ] Clear the list: nothing sounds. Untick the Custom trigger: nothing sounds. *(self-test)*
- [ ] Type a list, close the window without clicking elsewhere, reload the plugin: the list is kept.

### Highlight

Turn on the Name and Custom triggers, enter `raid` as a custom word, and set quiet time to 0.

- [ ] Highlight is on by default: `/saymyname inject Say A Friend hi <Me first name>, raid?` shows your name and `raid` in pale yellow with an orange glow; the rest of the line, and the sender name, keep the Say colour. *(self-test: which words get colour payloads; look at the colours yourself)*
- [ ] `inject Say A Friend <Me first name>'s raid`: only the name is coloured, not the `'s`. `inject Say A Friend Primrose afraid`: nothing coloured. `inject Say A Friend <Me first name, last letter repeated> and <Me first name spelled with hyphens>`: both whole words are coloured. *(self-test: name and possessive, and nothing coloured inside other words)*
- [ ] Pick another text colour and glow in the Highlight section: the next injected line uses them. Pick None for glow: text colour only. None for both: lines are unchanged. *(self-test: which payloads are added)*
- [ ] Untick Play a sound: lines are still coloured, with no sound. Untick Highlight, tick Play a sound: sound, no colour. *(self-test: sound and colour payloads)*
- [ ] Untick Say for Name: `inject Say A Friend hi <Me first name>` is not coloured; on Party it is. *(self-test)*
- [ ] `/saymyname inject Say <Me> hi <Me first name>` (you as the sender): not coloured. *(self-test)*
- [ ] A real line with an item or map link and your name after it: the link still works and only your name is coloured.
- [ ] The Text and Glow pop-ups are a full grid (10 swatches high): greys in the first column(s), then hue left to right from red round to magenta, each column light at the top to dark at the bottom. Hovering a swatch shows its row number.
- [ ] Next to the pickers, the preview `hey <Me first name>, thank you!` shows your name in the picked text colour and glow, and updates as you pick. Inject a line with your name: its colours and glow match the preview (the font may differ). With None for both it is plain; with Highlight off it dims.
- [ ] Switch the game's UI colour theme to Light, with a glow whose swatch changes: the swatches, preview and a newly injected line all use the Light theme's colours. Switch back.
- [ ] `/saymyname targeting-me A Friend`, `/saymyname inject Say A Friend thank youuu, is this yours?`: `youuu` and `yours` are coloured. Same on `CustomEmote`. `inject Say Someone Else thank you`: not coloured. Untick Play a sound: still coloured. *(self-test: colour payloads)*
- [ ] Reload the plugin: the Highlight setting and both colours are kept.
- [ ] With a second chat tab that shows Say open in the background, `inject Say A Friend hi <Me first name>`: that tab's name turns the tab colour and pulses, fading to the same dull beige and back at the same rate as the game's green for an unread tell; next to another unread tab they pulse in step. Opening the tab clears it.

### Emote and You triggers

Set quiet time to 0 so every alert sounds, and turn on verbose logging (`/xllog` → log level Verbose) to see `Emote trigger:` and `You trigger:` lines.

Solo:

- [ ] `/xllog` has no `emote function not found` warning after loading.
- [ ] `/saymyname emotes`, then wait somewhere busy (e.g. a city aetheryte plaza): other players' emotes log `Test harness: emote ...` lines. For each one that also shows in chat (e.g. "X pets Y"), the log names the same emoter and target with `logged=True`. Lines with `logged=False` match emotes you saw but chat didn't show. If no lines appear while players emote, the hook doesn't see network emotes and the emote cases below will fail. `/saymyname emotes` again turns it off.
- [ ] `/saymyname targeting-me A Friend`, then `/saymyname inject Say A Friend thank you`: sound. Same with `your`, `yours`, `you're`, and on `CustomEmote`. *(self-test)*
- [ ] Still pretending: `inject Say A Friend hello`: no sound. `inject Say Someone Else thank you`: no sound. `inject Party A Friend thank you`: sound; same on `Alliance` and `CrossParty`. `inject Linkshell1 A Friend thank you`: no sound (not a You channel). *(self-test)*
- [ ] Untick Party for You, `inject Party A Friend thank you`: no sound; `inject Say A Friend thank you`: sound. Tick Party again. *(self-test)*
- [ ] Untick Say for You, `inject Say A Friend thank you`: no sound. `/saymyname targeting-me` (stop pretending), tick Say again, same inject: no sound. *(self-test)*

With a second player (*A Friend*, e.g. an alt on another client) near you:

- [ ] A Friend targets you and uses `/pet`: one sound, and the log shows `emote ... at you, logged=True`.
- [ ] A Friend targets you and uses `/pet motion` (motion only, no log message): one sound, `logged=False`. Same with their emote log messages turned off in their settings.
- [ ] A Friend targets someone else (or nobody) and emotes: no sound.
- [ ] Untick the Emote trigger, A Friend emotes at you: no sound; `/say thank you` from them still sounds.
- [ ] A Friend targets you and says `/say thank you`: sound. Same with `your`, `yours`, `you're`, and `/em waves at you` (a custom emote, handled by You, not Emote). `/em waves` without a 'you' word: no sound. Untick Custom emotes for You: `/em waves at you` no longer sounds, while standard emotes still do.
- [ ] A Friend targets you and says `/say hello`, or says `/say thank you` without targeting you: no sound.
- [ ] You target A Friend and emote at them, or `/say thank you`: no sound.
- [ ] Untick both the Emote and You triggers: none of the above sound.

### Target Speaks trigger

Set quiet time to 0, turn off the Name, Custom, Emote and You triggers so only this one can sound, and turn on verbose logging to see `TargetSpeaks trigger:` lines.

Solo:

- [ ] It is off by default on a fresh config, with Say and Custom emotes ticked. *(self-test)*
- [ ] Tick it. `/saymyname my-target A Friend`, then `/saymyname inject Say A Friend hello`: sound. Same on `CustomEmote`. On `StandardEmote`: no sound. *(self-test)*
- [ ] The line is not coloured, and no inactive chat tab changes colour. *(self-test: no colour payloads; check the tabs yourself)*
- [ ] Still pretending: `inject Say Someone Else hello`: no sound. `inject Party A Friend hello`: no sound (not a Target Speaks channel). *(self-test)*
- [ ] Untick Say for Target Speaks: `inject Say A Friend hello` no longer sounds. Untick Play a sound: nothing sounds. *(self-test)*
- [ ] `/saymyname my-target` (stop pretending), same inject: no sound. *(self-test)*

With a second player (*A Friend*) near you:

- [ ] Target A Friend. They `/say hello`: sound. They `/em waves`: sound. They `/pet` someone (or nobody): no sound.
- [ ] Target A Friend while a third player (or nobody) speaks or emotes at A Friend: no sound.
- [ ] Target someone else, or nobody, while A Friend speaks: no sound.
- [ ] Target yourself and `/say hello`: no sound.
