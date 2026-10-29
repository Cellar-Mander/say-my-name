<img src="SayMyName/images/icon.png" alt="Say My Name logo: a speech bubble saying &quot;Hey You&quot;" width="128" align="right">

# Say My Name

A Dalamud plugin for FINAL FANTASY XIV that alerts you when other players call for you in chat, so you notice even while busy or looking away.

- **Name**: your character's name or nicknames, even stretched (Aliiice) or spelled out (A-L-I-C-E)
- **Custom**: your own words and phrases
- **You**: a player targeting you says "you", "your", "thank you" and the like
- **Emote**: a player uses an emote on you, including motion-only emotes
- **Target Speaks**: your current target says anything (off by default)

Each alert can play a sound, colour the matched words and colour inactive chat tabs that show the line. Choose the channels each alert listens on, a quiet time between sounds, and the colours.

## Installing

Say My Name is installed from its own plugin repository, not the official Dalamud one.

1. In game, type `/xlsettings` and open the **Experimental** tab.
2. Under **Custom Plugin Repositories**, paste this URL, click **+**, make sure it is ticked, then click **Save**:
   ```
   https://raw.githubusercontent.com/Cellar-Mander/say-my-name/main/repo.json
   ```
3. Type `/xlplugins`, search for **Say My Name** and install it.

Updates arrive through the plugin installer like any other plugin.

## Using

Type `/saymyname`, or click the plugin's settings button in `/xlplugins`, to choose which alerts are on and how they look and sound.

## Good to know

- This is a third-party plugin. The Dalamud team has not reviewed it, which is why Dalamud warns you when you add a custom repository.
- Everything happens on your computer. The plugin reads chat as it arrives and sends nothing anywhere.
- Parts of this plugin were written with the help of AI coding tools. Every change is reviewed, tested and released by the author.

## Feedback

Found a bug or have an idea? [Open an issue](https://github.com/Cellar-Mander/say-my-name/issues).

To build or test the plugin from source, see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT. See [LICENSE.md](LICENSE.md).
