# Contributing

How to build, test and try out Say My Name from source. To just use the plugin, see the [README](README.md).

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

## Releasing

A release is the plugin's `<Version>` in `SayMyName/SayMyName.csproj` (four numbers, e.g. `0.1.2.0`), published from the tag `v<Version>`. Installs see it once `repo.json` on `main` points at it. `scripts/release` (Git Bash on Windows) and `.github/workflows/release.yml` do the mechanical steps:

1. Check that `CHANGELOG.md`'s Unreleased section covers every user-facing change. The Changelog check flags PRs that change plugin code without an entry; label a PR `no-changelog` when users won't notice it.
2. From a clean checkout, with `gh` signed in:

   ```bash
   scripts/release prepare 0.1.2.0
   ```

   It makes branch `release-prep/v0.1.2.0` from `origin/main`, sets `<Version>`, dates the Unreleased entries, builds, runs the tests and opens the PR. In the PR, check the list of changes since the last release (⚠ marks plugin code changes with no CHANGELOG entry) and any note that your local Dalamud differs from the one CI builds against (see below). Then merge it.
3. Merging starts the Release workflow, which waits for you: open the run in Actions and approve the `release` deployment under Review deployments. It then tags the merge commit, builds, creates the GitHub release with the version's CHANGELOG section as its notes, and opens a PR titled "Update repo.json for v0.1.2.0".
4. Check that PR's version and download links, then merge it. If GitHub holds its checks with "Approve and run workflows", approve them first.
5. Check what installs get (raw.githubusercontent.com caches `repo.json` for a few minutes, so a just-merged PR can still show the old version):

   ```bash
   scripts/release verify 0.1.2.0
   ```

Pushing a tag by hand (`git tag v0.1.2.0 <commit> && git push origin v0.1.2.0`) also publishes, without the approval step. A test tag such as `v0.1.2.0-test1` makes a prerelease and pushes `repo.json` to the `release-test` branch instead of opening a PR; delete both afterwards:

```bash
gh release delete v0.1.2.0-test1 --cleanup-tag --yes && git push origin --delete release-test
```

CI builds against a pinned Dalamud (`.github/actions/install-dalamud/action.yml`), while the launcher updates your local one by itself. When `prepare` notes that they differ and you've tested with the newer one, move the pin as that file's comment describes.

One-time repository settings the workflow needs:

- Settings → Actions → General: "Allow GitHub Actions to create and approve pull requests", for the `repo.json` PR.
- Settings → Environments: a `release` environment with you as required reviewer, limited to `main`. Without it, GitHub creates the environment unprotected on the first run and tags without asking.
