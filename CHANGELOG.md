# Changelog

All notable changes to Say My Name. Versions match the plugin version shown in `/xlplugins`.

## Unreleased

### Added

- A volume slider under the sound choice sets how loud the alert plays, from 5% to 100% of the game's own volume. It starts at 100%, as loud as before.

## 1.0.0.0 - 2026-11-04

### Added

- `/xlplugins` → Changelogs shows what changed in each release.
- Names, nicknames and 'you' words also match when stuttered ("A-Alice", "y-you"), and when spelled out with the first letter repeated ("A-A-L-I-C-E") or stretched ("A-L-I-C-E-E").

### Changed

- In settings, nicknames and custom words are edited as a list, a row per entry with buttons to add and remove. Each list holds up to 12 entries of up to 64 characters. Longer lists saved by earlier versions are kept and still match.
- In settings, the notes on the Emote alert and on chat colouring moved into (?) markers beside those settings; hover one to read it.
- The Emote alert now only goes off for emotes that show in one of your chat tabs. Emotes used without a log message (motion only) no longer alert, and are no longer written to chat ending "[Say My Name]". The word "you" in an emote at you is coloured.

### Fixed

- Custom words and phrases, and nicknames of more than one word, now match when chat puts extra spaces between their words ("good  morning").
- A spelled-out name no longer alerts when a single letter sits right next to it, as in "I A-L-I-C-E" or "I,A-L-I-C-E".

## 0.1.1.0 - 2026-10-31

### Added

- Hovering a highlight colour (Text, Glow, Tab name) in settings shows its colour details, as the colour picker does.

### Changed

- New defaults: the Target Speaks alert starts on and the Emote alert starts off; the sound is `<se.16>`; the highlight text, glow and tab colours changed. Settings you have already saved are kept.

### Fixed

- Your own lines no longer set off alerts or get coloured when Party Icons or Player Tags tag or rename the sender.
- The settings preview colours "you" as chat does, not only your name.
- The plugin installer shows the plugin's icon instead of a question mark.

## 0.1.0.0 - 2026-10-30

First release.

### Added

- **Name** alert: your character's name or nicknames, even stretched (Aliiice) or spelled out (A-L-I-C-E).
- **Custom** alert: your own words and phrases.
- **You** alert: a player targeting you says "you", "your", "thou", "thine" and the like.
- **Emote** alert: a player uses an emote on you, including motion-only emotes.
- **Target Speaks** alert: your current target says something while targeting you.
- Each alert can play a sound, colour the matched words and colour inactive chat tabs that show the line.
- Choose the chat channels the Name, Custom and You alerts listen on, a quiet time between sounds, and the colours.
- Settings window, opened with `/saymyname` or the settings button in `/xlplugins`.
