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

## Loading in-game

1. `/xlsettings` → Experimental → add the full path of `SayMyName.dll` to Dev Plugin Locations.
2. `/xlplugins` → Dev Tools → Installed Dev Plugins → enable Say My Name.
3. `/saymyname` (or the plugin installer's buttons) opens the settings window; `/xllog` shows the plugin log.

## License

AGPL-3.0, inherited from SamplePlugin. See [LICENSE.md](LICENSE.md).
