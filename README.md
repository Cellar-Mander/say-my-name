# Say My Name

A Dalamud plugin for FINAL FANTASY XIV, started from [goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin).

## Prerequisites

- The game, run under Dalamud at least once: [XIVLauncher](https://goatcorp.github.io/) on Windows, [XIVLauncher.Core](https://github.com/goatcorp/XIVLauncher.Core) on Linux
- .NET 10 SDK
- Git; on Windows, Git for Windows, whose Git Bash runs `scripts/dev-plugin`

The build finds Dalamud at the launcher's default dev path; set `DALAMUD_HOME` to point it elsewhere:

| OS | Default Dalamud path |
|---|---|
| Windows | `%AppData%\XIVLauncher\addon\Hooks\dev\` |
| Linux | `~/.xlcore/dalamud/Hooks/dev/` |
| macOS (XIV on Mac) | `~/Library/Application Support/XIV on Mac/dalamud/Hooks/dev/` |

## Building

```bash
dotnet build
```

Output: `SayMyName/bin/x64/Debug/SayMyName.dll`. Commands are the same on every OS.

Warnings are treated as errors in every project (`Directory.Build.props`).

## Testing

Game-independent logic lives in `SayMyName.Core` (plain `net10.0`, no Dalamud reference), so it can be tested anywhere:

```bash
dotnet test SayMyName.Core.Tests
```

CI runs these tests on Linux for pushes and PRs to `main`.

## Testing in-game

Dalamud loads the plugin from one dev folder outside every checkout (override with `SAYMYNAME_DEV_DIR`):

- Windows: `%AppData%\XIVLauncher\devPlugins\SayMyName`
- Linux: `~/.xlcore/devPlugins/SayMyName`

Any checkout or worktree can deploy to it, but only one at a time: deploying claims the folder, and other checkouts are refused until the claim is released, so a build under test is never overwritten.

On Linux (or in Git Bash on Windows):

```bash
scripts/dev-plugin deploy [task-ref]   # build this checkout, claim the folder, copy the plugin in
scripts/dev-plugin status              # who holds the folder, on which branch and commit
scripts/dev-plugin release             # done testing: free the folder for other checkouts
```

On Windows, from Command Prompt or PowerShell, run the same commands as `scripts\dev-plugin.cmd deploy [task-ref]` and so on. Both scripts share the claim, so you can mix them.

The checkout holding the claim can redeploy as often as it likes. A claim left behind by an abandoned session is cleared with `release --force`, which only you can run: a project hook blocks agents from it.

One-time setup:

1. `/xlsettings` → Experimental → add the dev folder's `SayMyName.dll` to Dev Plugin Locations (and remove any location inside a checkout). The game sees Windows paths; on Linux it runs under Wine, where `Z:` is the root of the Linux filesystem:
   - Windows: `%AppData%\XIVLauncher\devPlugins\SayMyName\SayMyName.dll`, with `%AppData%` replaced by what `echo %AppData%` prints in Command Prompt (usually `C:\Users\<you>\AppData\Roaming`, but the profile folder can differ from your account name)
   - Linux: `Z:\home\<you>\.xlcore\devPlugins\SayMyName\SayMyName.dll`
2. `/xlplugins` → Dev Tools → Installed Dev Plugins → enable Say My Name.

`/saymyname` (or the plugin installer's buttons) opens the settings window. `/xllog` shows the plugin log, which starts with the loaded build's commit, e.g. `Say My Name 0.0.0.1+60d7b0d… loaded`.

[docs/smoke-test.md](docs/smoke-test.md) is the in-game checklist, and describes `/saymyname inject`, which fakes chat lines from other players when loaded as a dev plugin.

## License

AGPL-3.0, inherited from SamplePlugin. See [LICENSE.md](LICENSE.md).
