# Say My Name

A Dalamud plugin for FINAL FANTASY XIV, started from [goatcorp/SamplePlugin](https://github.com/goatcorp/SamplePlugin).

## Prerequisites

- XIVLauncher (or XIVLauncher.Core on Linux) with the game run under Dalamud at least once
- .NET 10 SDK
- Dalamud is found at the launcher's default dev path; set `DALAMUD_HOME` to override it

## Building

```bash
dotnet build
```

Output: `SayMyName/bin/x64/Debug/SayMyName.dll`

Warnings are treated as errors in every project (`Directory.Build.props`).

## Testing

Game-independent logic lives in `SayMyName.Core` (plain `net10.0`, no Dalamud reference), so it can be tested anywhere:

```bash
dotnet test SayMyName.Core.Tests
```

CI runs these tests on Linux for pushes and PRs to `main`.

## Testing in-game

Dalamud loads the plugin from one dev folder outside every checkout, `~/.xlcore/devPlugins/SayMyName` (override with `SAYMYNAME_DEV_DIR`). Any checkout or worktree can deploy to it, but only one at a time: deploying claims the folder, and other checkouts are refused until the claim is released, so a build under test is never overwritten.

```bash
scripts/dev-plugin deploy [task-ref]   # build this checkout, claim the folder, copy the plugin in
scripts/dev-plugin status              # who holds the folder, on which branch and commit
scripts/dev-plugin release             # done testing: free the folder for other checkouts
```

The checkout holding the claim can redeploy as often as it likes. A claim left behind by an abandoned session is cleared with `scripts/dev-plugin release --force`, which only you can run: a project hook blocks agents from it.

One-time setup:

1. `/xlsettings` → Experimental → add `Z:\home\<you>\.xlcore\devPlugins\SayMyName\SayMyName.dll` to Dev Plugin Locations (and remove any location inside a checkout).
2. `/xlplugins` → Dev Tools → Installed Dev Plugins → enable Say My Name.

`/saymyname` (or the plugin installer's buttons) opens the settings window. `/xllog` shows the plugin log, which starts with the loaded build's commit, e.g. `Say My Name 0.0.0.1+60d7b0d… loaded`.

[docs/smoke-test.md](docs/smoke-test.md) is the in-game checklist, and describes `/saymyname inject`, which fakes chat lines from other players when loaded as a dev plugin.

## License

AGPL-3.0, inherited from SamplePlugin. See [LICENSE.md](LICENSE.md).
