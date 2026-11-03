# Say My Name: design

What the plugin does and why, written from the player's side. It was reverse-engineered from the 0.1.x prototype, its docs and its task history (task a1d13ea8), so that the prototype can be checked against it, or rebuilt from it (task 8cecdd3b).

## How to use this document

- **The behaviour sections are the spec.** Where they say what should happen, that is the intent, even where the prototype does something else. Known gaps are marked *Pending* with the task that closes them.
- **It does not say how.** No class names, game functions, hooks, file layout, saved-settings format or algorithms. A rebuild is free to choose all of those, as long as the behaviour, constraints and carry-over of settings hold.
- **Appendix A lists facts about the game, Dalamud and other plugins** that the prototype learnt the hard way. They are facts about the environment, not about how to deal with them.
- Matching rules are given as examples. The examples are the acceptance cases: a rebuild should agree with every row.

## Purpose

Players miss chat aimed at them while they are crafting, in a duty, tabbed out or reading something else. Say My Name notices chat that is probably meant for the player and makes it hard to miss, with a sound, coloured words and coloured chat tabs.

It is for players who socialise in busy places (roleplay, venues, cities, Free Companies, parties) and who want to know when someone is calling for them, without reading every line.

## Principles

These hold for every feature.

1. **Only chat aimed at you.** Alerts are for things another player says or does to the player. The player's own lines never set off an alert or get coloured, even when another plugin has tagged or renamed the sender.
2. **Nothing leaves the computer.** The plugin reads chat as it arrives and sends nothing anywhere. It never writes to the server or acts in the game on the player's behalf.
3. **No hidden information.** An alert should not tell the player something the normal game would not let them see, and should respect other players' choices, such as turning off their emote log messages. See Appendix A on D17 and risk 42888d53.
4. **Respect other plugins.** A line another plugin has hidden does not alert. The plugin keeps working alongside common chat plugins (see Compatibility). This is best effort: Dalamud's limits and other plugins' own choices, which we don't control, may sometimes rule it out.
5. **Quiet by default.** Busy channels and social noise start off; one quiet time stops a run of alerts from becoming a run of sounds.
6. **No cost to the game.** Checking a line must not cause a noticeable frame drop, even in a crowded city with fast chat.
7. **Settings survive.** Settings persist across reloads and game restarts, and every release keeps the settings players already saved, including when a setting's meaning or default changes.

## Concepts

- **Trigger:** a kind of chat that sets off an alert. There are five: Name, Custom, You, Emote and Target Speaks. Each can be turned on or off.
- **Response:** what an alert does: play a sound, colour the words that set it off, colour the inactive chat tabs that show the line.
- **Channel:** a chat channel a player writes in. Tells are never listened to (see Non-goals).

| Channel | Name, Custom: offered | You: offered | On by default |
|---|---|---|---|
| Say | yes | yes | yes |
| Custom emotes (`/em`) | yes | yes | yes |
| Party (including in cross-world parties) | yes | yes | yes |
| Alliance | yes | yes | no |
| Yell, Shout | yes | no | yes |
| Free Company, Novice Network | yes | no | no |
| Linkshell 1–8, Cross-world Linkshell 1–8 | yes | no | no |

Large groups talk all day, mostly not to the player, so they start off. You is offered only where the sender is likely to be nearby, because it has to check what the sender is targeting. Emote listens only to standard emotes, and Target Speaks to every channel a player writes in; neither has a channel choice.

Not offered as channel choices:

- **Cross-world Party:** the Party row covers party chat wherever the party is formed (96deea68). Players can't tell the two apart, and testing suggests cross-world parties use the regular party channel anyway.
- **PvP Team:** PvP teams were removed from the game (6f97e412).
- **Standard emotes:** the game writes these lines, not a player, so only the Emote trigger listens to them (6b78f60).
- **Tells:** see Non-goals.

## Triggers

### Name

Another player says the player's character name or one of their nicknames. **On by default.**

- The first and last names each match on their own. The player can add any number of nicknames, up to a cap (see Settings). Each is matched on its own, in the same way as the name, and as a whole: "Big Al" matches "Big Aaal's" but not "Big" alone. A run of whitespace in chat text counts as the one space between a nickname's words, so "Big   Al" matches too.
- Matches are whole words. An apostrophe or hyphen between letters joins a word, except a trailing possessive *'s*. Straight and typographic apostrophes are the same.
- Names also match **stretched** (a letter repeated to 3 or more copies, or the last letter repeated any number of times), **spelled out** (single letters split all by single hyphens or all by single spaces, in any case) and **stuttered** (the first letter repeated with hyphens before the name, as written or stretched). In a spelling, the first letter may repeat any number of times and the others as when stretched. A single letter right next to a spelling, whatever is between them, means it isn't the name.
- **Match case**, on by default, matches a name only as written, so Rose doesn't fire on "a rose". The extra letters of a stretched name, and spelled-out names, match in any case.

| Name *Alice*, Match case on | Alerts? | Coloured |
|---|---|---|
| `hi Alice` / `Alice!` / `Alice Smith` | yes | the name |
| `Alice's sword` | yes | `Alice`, not `'s` |
| `Aliiice` / `Alicee` / `Aaalice` | yes | the whole word |
| `A-L-I-C-E` / `a l i c e` | yes | the whole word |
| `A-A-L-I-C-E` / `A-L-I-I-I-C-E` / `A-L-I-C-E-E` | yes | the whole word |
| `A-Alice` / `a-A-Aliiice` (a stutter) | yes | the stutter and the name |
| `A-L-L-I-C-E` (a letter doubled inside) / `L-Alice` | no | |
| `Aliice` (a vowel doubled inside) | no | |
| `alice` | no (yes with Match case off) | |
| `Malice`, `Alice-Ann`, `Alice'ka` | no | |
| `Big  Zed` (nickname *Big Zed*, two spaces) | yes | `Big  Zed`, spaces included |
| `h a l i c e` style runs, where the letters run on from other single letters | no | |
| `I A-L-I-C-E` / `I,A-L-I-C-E` / `A A-L-I-C-E`, a single letter right next to the spelling | no | |
| The player's own line saying their name | no | |

Further cases: a name like Pol must not match "pool", and Bob must not match "boob". For nicknames *Zuzu* and *Big Zed*: `Zuzu`, `Big Zed's?` and `Zuzuuu!` alert, while `Zed` alone does not, and with Match case on neither does `zuzu`.

### Custom

Someone says one of the player's own words or phrases. The player keeps a list of them, up to a cap (see Settings), and each entry is matched on its own. **On by default, with an empty list.**

- Matches are whole words or phrases, only as written: no stretching or spelling out. Runs of whitespace inside a phrase count as one space, both in the list and in chat text; the colouring covers the whole phrase as written in chat, spaces included.
- **Match case** is off by default.
- When a line matches both Name and Custom, it is one alert.

| List `raid`, `good morning` | Alerts? |
|---|---|
| `raid tonight?` / `RAID` / `Good morning!` | yes (`RAID` no with Match case on) |
| `good  morning` (two spaces) | yes, `good  morning` coloured |
| `afraid` / `raiiid` / `good` / `morning` | no |
| The player's own line | no |

### You

A player who is **targeting the player** uses a word that addresses them. **On by default.**

- The words are: you, your, yours, yous, yourself, yourselves, you're, you'll, you've, you'd, y'all, thou, thee, thy, thine. They match in any case, with or without the apostrophe, stretched, spelled out or stuttered (`y-you`).
- "youth" and "bayou" don't match.
- The sender must be targeting the player when the line arrives. A 'you' word from anyone else, or a line without one, does nothing.
- Custom emotes count: `/em waves at you` from someone targeting the player alerts here, not under Emote.

| Line from A Friend | A Friend targeting the player? | Alerts? |
|---|---|---|
| `thank you` / `is this yours?` / `thank youuu` / `y'all` / `thee` | yes | yes, and the 'you' word is coloured |
| `hello` | yes | no |
| `thank you` | no | no |
| `/em waves at you` | yes | yes |
| `/em waves` | yes | no |

### Emote

Another player uses a standard emote (such as `/pet`) with the player as its target. **Off by default**, since emotes are mostly social noise.

- Only emotes that the game writes to a chat tab the player can see count. An emote whose log message the emoter turned off (motion only) never alerts, is never written to chat and is never coloured. *Pending a09d9196: the prototype writes such emotes to chat itself, ending them with "[Say My Name]", and alerts on them.*
- The emote's line is coloured: the 'you' words, or the player's name in languages whose emote lines have no 'you' word (such as Japanese).
- The sound plays only if the line shows in a chat tab, so the player can always see who set it off.
- The settings say that this trigger needs a chat tab that shows standard emotes.
- An emote at someone else, or at nobody, does nothing. So do the player's own emotes.

### Target Speaks

The player's current target writes anything, on any channel a player writes in (not standard emotes or tells). **On by default.**

- An option chooses between **"Only messages sent while targeting you"** (the default), which needs the target to be targeting the player too, and **"Every message they send"**.
- It plays the sound only. It colours no words and no tabs, because the whole line is the reason, not a word in it.
- Targeting yourself, or the line coming from someone other than the target, does nothing.

## Responses

### Sound

- One sound for every trigger: one of the game's 16 chat sound effects (`<se.1>` to `<se.16>`), `<se.16>` by default. A Test button plays the selected sound, at most once a second.
- **Play a sound** turns it off for every trigger. Colouring still works.
- **Quiet time between sounds**, 0 to 60 seconds and 5 by default, is shared across all triggers. An alert within the quiet time still colours words and tabs, but makes no sound.
- *Requested, not decided:* volume control (30006a42).

### Colouring the words

- Applies to Name, Custom, You and Emote. **On by default.** It works whether or not a sound plays.
- Only the words that set off the alert are coloured: names and nicknames (without a possessive *'s*), custom words, and 'you' words from someone targeting the player. The rest of the line, and the sender's name, keep the channel's colour.
- The player picks a **text colour** and a **glow** from the game's own UI colour palette. Either can be None; None for both leaves lines unchanged. Defaults: pale yellow text with an orange glow.
- The colour pickers show the palette as a grid sorted HSL-style: greys first, then hue running from red round to magenta, each hue from light to dark.
- The colours follow the game's UI theme (for example Light), as the game's own coloured text does.
- Item links, map links and other active parts of a line keep working.
- The settings show a live preview line, such as "Hey *Alice*, thank *you*!", that matches what chat will show.

### Colouring inactive chat tabs

- Applies to Name, Custom, You and Emote. **On by default.**
- Every inactive chat tab that shows the alerting line has its name coloured in the player's chosen colour, from the same palette. The colour pulses in step with the game's own unread-tab pulse, so it reads as "unread, and for you".
- It stays until the player opens that tab. Logging out clears it, as it clears the game's unread colour. *Bug 6df20e1a: tabs sometimes lose the colour before being opened; the cause is unknown.*
- The colour picker has no None; turning tab colouring off is its own switch.

## Settings

- Opened with `/saymyname` or the settings button in `/xlplugins`. The installer shows no separate open button.
- Every setting named above can be changed, and takes effect on the next line without a reload.
- Grouped in collapsible sections: triggers, sound, colouring, channels. Only the triggers section starts open, and which sections are open is remembered.
- Channels are chosen per trigger. A trigger that is off shows its channel choices dimmed.
- Hovering a colour shows its details, as the game's colour picker does.
- Nicknames and custom words are each a list of entries, managed as one setting. Each list has a cap on how many entries it holds, and each entry has a cap on its length. Both caps are to be decided (see Open questions). Edits to a list are kept even if the window is closed straight after editing.
- Settings saved by any earlier release load with their meaning kept. New defaults apply only to settings the player never saved.

The window's layout and wording may change. The settings and their defaults are the spec.

## Compatibility

| With | Expected |
|---|---|
| Chat 2 | Alerts, sound and word colour work, but the glow doesn't show. Tabs aren't coloured, because Chat 2 replaces the game's chat window. |
| Party Icons, Player Tags | The player's own lines are still recognised as theirs when these plugins add job, role or sender tags. Player Tags' random-name mode is untested. |
| The game's speech bubbles | Work as normal with coloured lines. |
| A plugin that hides a line | The hidden line doesn't alert. |

## Non-goals and decided limits

- **Tells.** Not listened to by any trigger. A tell is only ever sent to the player, and the game already has its own alert settings for tells.
- **Custom sound files, or other game UI sounds.** Not part of the core design. Custom sounds may be added later as a feature of their own (3c77dd0a). Note that the game's alarm sounds are too long, and most of its other UI sounds mean something in the game.
- **Alerts on NPC or system messages.** Only lines that players write, and emotes they use.
- **Revealing hidden emotes.** See Emote.
- **Anything that sends data or acts in game.**

## Open questions

- **Hidden information (42888d53):** whether D17 reviewers accept the You and Target Speaks checks of what another player is targeting. If not, the plan is to justify them, then cut them or default them off.
- **Localisation (85aae762):** which languages the settings should be in, and how matching should work in languages other than English. For example, You needs a 'you' word list per language, and Japanese has none.
- **Volume control (30006a42).**
- **List caps:** how many nicknames and custom words a list may hold, and how long each entry may be. The entry count depends on what is practical and on performance; the entry length on the longest name or phrase a player could need.
- **Lost tab colour (6df20e1a):** the cause needs a reproduction.
- **Dev testing:** a rebuild needs a way to feed in chat lines from fake players, and pretend targeting, to test triggers alone, and the solo cases run in Dalamud's self-test window. The behaviour is needed; how is open.

## Appendix A: facts about the environment

Learnt while building the prototype. These describe the game, Dalamud and other plugins, not a design.

**Chat lines**

- The game writes standard emote lines itself ("A Friend pets you."). They are never a player's text. `/em` custom emotes are player text with a sender.
- A player can turn off the log message for an emote (motion only). Nothing then reaches chat, though the motion is seen.
- The game has a separate cross-world party chat type, but no test has shown it in use. Testing suggests cross-world parties use the regular party chat channel.
- The sender text of a line carries marks around the name: party slot numbers, friend and cross-world icons (symbols or private-use glyphs). Other plugins add their own tags too (Party Icons' job prefix, Player Tags' tags).
- Chat text may use the typographic apostrophe (’) where a name has a straight one, or the reverse.
- Another plugin can hide a line before it is shown.
- Item and map links are parts of a line that must keep their structure if the text around them is recoloured.

**Chat tabs**

- The player chooses which channels each chat tab shows in the game's Log Filters settings. As of October 2026, FFXIVClientStructs had no mapping for those stored filters, so a plugin couldn't read them directly (7d98e301). Worth checking again for a rebuild.
- The game animates an unread tab's name every frame. In the default theme it holds green (#8CFF5A), fades in a straight line to beige (#C2BCA4) over about half a second, then snaps back, about every 0.64 s.
- Logging out clears the game's chat history and unread colours.
- Chat 2 replaces the game's chat window entirely, and doesn't draw the edge glow on coloured text.

**Colours and sounds**

- The game's UI colour palette has different colours under each UI theme (such as Light).
- The game has 16 chat sound effects, `<se.1>` to `<se.16>`. Its alarm sounds are long, and its other UI sounds are tied to game functions.

**Targeting and D17**

- The game shows the player their own target, and their target's target. It doesn't show what other nearby players are targeting.
- Dalamud's official repository (D17) reviews plugins against its rules, including on hidden information and on AI-written code. Criteria: https://github.com/goatcorp/DalamudPluginsD17. Submission (b2729ca6) is blocked on the AI policy.

## Appendix B: sources

README.md, CHANGELOG.md, docs/smoke-test.md, the settings window as a player sees it, the unit test cases (as examples of behaviour), and the notes on tasks a09d9196, 42888d53, 6df20e1a, 3c77dd0a, 30006a42, 85aae762, ee861e86 and b2729ca6.
