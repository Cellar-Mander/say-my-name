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

Mutation testing checks whether those tests would catch plausible faults: [Stryker.NET](https://stryker-mutator.io/docs/stryker-net/introduction/) makes small changes to `SayMyName.Core` and reruns the tests against each one. It runs in full nightly and on demand. It also runs on every PR that touches `SayMyName.Core` or its tests, but only on mutants in code or tests the PR changed; label the PR `mutation` for a full run, e.g. when it deletes tests. It is advisory only, with no score threshold. To run it locally (about a minute and a half):

```bash
dotnet tool restore
cd SayMyName.Core.Tests && dotnet stryker
```

`--since:main` limits a local run to your changes like on PRs, but only in a regular clone: in a git worktree Stryker diffs the main checkout instead.

Before merging a Core PR, check the PR run's summary for surviving mutants, which are the faults the tests missed, and for "no coverage" ones, whose line no test reaches. The full HTML and JSON reports are under `StrykerOutput/`, or in the `mutation-report` artifact in CI. `tools/mutant-triage` lists them and records what became of each one:

```bash
python3 tools/mutant-triage run before        # full run, kept as "before"
python3 tools/mutant-triage list --context 3  # survivors and no-coverage mutants, each with a KEY
python3 tools/mutant-triage rerun SayMyName.Core/NameMatcher.cs
python3 tools/mutant-triage record KEY killed # or suppressed, follow-up or spec, with --note
python3 tools/mutant-triage run after
python3 tools/mutant-triage diff before after
python3 tools/mutant-triage summary before after   # outcome table for the PR description
```

It reads its commands from `.claude/checks` (key=value):

```
mutation_run=cd SayMyName.Core.Tests && dotnet stryker
mutation_rerun=cd SayMyName.Core.Tests && dotnet stryker --mutate "**/{name}"
spec=docs/design.md
```

Triage each one:

1. **Look for an input that tells the mutant apart from the code.** Read the mutant's change and think of an input on which the two give different results. Add it as a plain example test, as a new `InlineData` row where one fits; a throwaway script may try many inputs, but commit only the one that kills the mutant. For a no-coverage mutant, first write a test that reaches its line. Confirm the kill on that file alone: `python3 tools/mutant-triage rerun SayMyName.Core/NameMatcher.cs` (or `dotnet stryker --mutate "**/NameMatcher.cs"`).
2. **No such input means the mutant is probably equivalent.** That can't be proven in general, so say why in a suppression just before the code, naming only the mutator involved: `// Stryker disable once Equality : words are never empty, so space is never 0`. The reviewer confirms each reason.
3. **Check the behaviour against [the design](docs/design.md):**
   - stated there: a real gap, so the test stays;
   - not stated and not important, such as which way an exact rounding boundary falls: suppress it, or, where the mutant shows the code is redundant, simplify it in a follow-up task;
   - not stated but maybe important: ask in a PR comment.

A suppression covers every mutant of that kind in the code that follows it, so keep it narrow:

- Before a statement, it covers the whole statement, and for an `if`, a loop or a member, everything inside. Before a condition on its own line it covers only that condition (see `NameMatcher.Find`). It also covers the condition's other mutants of that kind, such as `<` turned into `>`; the tests still catch those faults, but Stryker no longer checks that they do.
- Diff a full run's report against one from before the change with `mutant-triage diff before after`: a suppression should turn only the intended survivors, and that condition's other mutants of the same kind, into Ignored. It lists every mutant the tests caught before that is now ignored; confirm a condition's own with `record KEY suppressed --note <why>`, and narrow the comment for any other.

`stryker-config.json` sets no ignore rules: Core has no logging, exceptions or message text, and its `ToString` calls save channels by name, so they have to be mutated like any other code.

## Checking workflows

Every PR runs the Workflow lint workflow: [actionlint](https://github.com/rhysd/actionlint) (with shellcheck on `run:` scripts) and [zizmor](https://docs.zizmor.sh) for security issues. To run them locally, install both (and shellcheck), then from the repo root:

```bash
actionlint && zizmor .
```

Pin each action to a full commit SHA with its version in a comment, as Dependabot updates them that way. Give each workflow a `permissions:` block, and a job that needs more its own. Check out with `persist-credentials: false` unless a later step pushes with git. A zizmor finding you accept goes in `.github/zizmor.yml` with the reason.

## Untrusted input

Chat lines come from other players, and settings text is whatever the user types. Matching on either must stay linear in the line's length, so no backtracking regex. New user-configurable lists and text fields need a cap. A PR that touches either says so under Risk in its description and says how those two points hold. The PR template asks the same, but agent-written PR bodies skip it.

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

   It makes branch `release-prep/v0.1.2.0` from `origin/main`, sets `<Version>`, dates the Unreleased entries, builds, runs the tests and opens the PR. In the PR, check the list of changes since the last release (⚠ marks plugin code changes with no CHANGELOG entry) and any note that your local Dalamud differs from the one CI builds against (see below). Then merge it. Merging publishes nothing.
3. Optionally, put it out for testing first (see [Testing builds](#testing-builds)).
4. Publish it:

   ```bash
   scripts/release publish 0.1.2.0
   ```

   This runs the Release workflow on `main` (also under Actions → Release → Run workflow, channel `stable`). It tags `main` `v0.1.2.0`, builds, creates the GitHub release with the version's CHANGELOG section as its notes (also put as plain text in the manifest, for the plugin installer's Changelogs), and opens a PR titled "Update repo.json for v0.1.2.0". It refuses if `v0.1.2.0` exists or if Unreleased has entries, which would be left out of the notes: move them under the `## 0.1.2.0` heading in a PR first.
5. Check that PR's version and download links, then merge it. The workflow starts the required checks on it itself; the PR's own runs may wait under "Approve and run workflows", which merging doesn't need.
6. Check what installs get (raw.githubusercontent.com caches `repo.json` for a few minutes, so a just-merged PR can still show the old version):

   ```bash
   scripts/release verify 0.1.2.0
   ```

### Testing builds

A testing build is offered only to installs that opt in: in `/xlsettings` → Experimental, tick "Get plugin testing builds", then in the plugin installer's Installed Plugins tab (`/xlplugins`) right-click Say My Name and select "Receive plugin testing versions". Other installs stay on the stable release. Once `main`'s `<Version>` is above the last release (step 2):

```bash
scripts/release publish 0.1.2.0 --testing
```

The workflow (channel `testing`) tags `main` with the next `v0.1.2.0-rc<n>`, which the run's summary names, creates a prerelease and opens the `repo.json` PR. That PR sets only the testing fields (`TestingAssemblyVersion`, `TestingDalamudApiLevel`, `DownloadLinkTesting`, `TestingChangelog`); check that the stable fields are unchanged, then merge it and check it with `scripts/release verify 0.1.2.0-rc1`. Its notes are the version's CHANGELOG section with any Unreleased entries above it.

To fix something found in testing, merge the fix to `main` as usual (its CHANGELOG entry goes under Unreleased) and publish again: it becomes `-rc2`. The stable release (step 4) removes the testing fields, so opted-in installs move to it too. A testing build is refused while an unmerged stable `repo.json` PR is open, since it would replace it.

### Other ways to publish

Pushing a tag by hand (`git tag v0.1.2.0 <commit> && git push origin v0.1.2.0`, or `v0.1.2.0-rc1` for a testing build) also publishes that commit, skipping the workflow's checks. A test tag such as `v0.1.2.0-test1` makes a prerelease and pushes `repo.json` to the `release-test` branch instead of opening a PR; delete both afterwards:

```bash
gh release delete v0.1.2.0-test1 --cleanup-tag --yes && git push origin --delete release-test
```

CI builds against a pinned Dalamud (`.github/actions/install-dalamud/action.yml`), while the launcher updates your local one by itself. When `prepare` notes that they differ and you've tested with the newer one, move the pin as that file's comment describes.

One-time repository settings the workflow needs:

- Settings → Actions → General: "Allow GitHub Actions to create and approve pull requests", for the `repo.json` PR.
