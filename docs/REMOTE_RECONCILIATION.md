# Working-branch reconciliation — 2026-09-27

## History and boundaries

The clean starting branch was `ID3-Multi-device-input` at
`ab6902189b11be7302d0f0c6cdc93dec0db0636e`. Live origin advertised
`80923197cb739fecc3e91da20992e19ebcc75882` for both the working branch and main.
A fetch confirmed 16 local-only commits and one remote-only commit, with common
base `377e4ad9451a34ed4bc0acf735038372d8dec876`. There was no merge in progress.

`backup/controls-before-remote-merge-ab69021` preserves the starting local HEAD.
The reconciliation is a two-parent merge of that HEAD and `8092319` (release .38).
It preserves both histories, without reset, rebase, force push or modification of
main. Local main was already at `8092319`; this task did not move it. Publication
is pending owner review. A normal fast-forward is possible while origin remains
an ancestor of the merge; check live origin again before publication.

## Conflict decisions

Three files conflicted. Resolutions were made by hunk and checked against both
parents; no whole-file ours/theirs replacement was used.

- `Assets/Scripts/Idas3ControlBindings.cs`: retain the snapshot-aware poll,
  shared evaluator/capture, explicit menu dispatcher, test isolation, original
  configuration guards and context-aware Online command. Keep per-control
  reconnect guards in the existing `.Reconnect.cs` partial, not a second copy.
  Adopt upstream capture timeout diagnostics. Do not call upstream `PollRig`.
- `Assets/Scripts/Idas3ControllerDevices.cs`: retain immutable snapshots, validity,
  endpoint generations, read-error handling and snapshot invalidation. Adopt
  upstream HID diagnostics and automatic fallback when the active controller
  disappears with switching locked by an open menu. This does not replace a
  live menu controller, override Keyboard only, resolve physical identity or
  automatically reattach experimental session assignments.
- `Assets/Scripts/Idas3SceneGame.cs`: keep snapshot polling, evaluated test input
  isolation, menu contexts and the experimental profile-change guard. Adopt
  source diagnostics and the isolated provider for the explicit Online smoke.

The incoming `Idas3ControlBindings.Rig.cs` and `Idas3WheelRigChecks.cs` (and their
metadata) are deliberately absent. That backend aggregates saved generic profiles
and recreates navigation aliases. It conflicts with explicit session assignments,
original-configuration preservation and exclusive navigation ownership. Their
source remains available in the remote parent. Existing production multi-input
checks cover simultaneous assigned input instead; this is not a second engine.

The Controls module, pause-menu integration, experimental persistence, menu/setup/
reconnect partials and GitHub Actions workflows are unchanged from the local
parent. Original configurations, scoped Save/Discard, mixed keyboard/device
capture, dedicated Online entry and disabled experimental FFB are preserved.

## Other upstream changes reviewed and retained

| Area | Resolution |
| --- | --- |
| Build and release | Retain .38 version changes in build settings, client/server upload policy, release docs and tests. Preserve `BuildMultiInputPlayer`, its separate product/version and sample updater disablement. No release or server deployment performed. |
| Mirrors | Retain native main/UI changes and rear-view app tests for all driving modes. |
| Tsubaki | Retain converter/helper, generated side paths/collision, scenery/native checks and Python tests. |
| Wet shadows | Retain course material and shader changes with shadow diagnostics. |
| HUD | Retain speed palettes/catalog, imported meter changes and maximum-gear telemetry v4; native struct remains 40 bytes. Retain associated native/managed diagnostics. |
| Font | Retain bundled Noto font, license, importer metadata, build guard, Sound Room shared-resource lifetime changes and font/Wine diagnostics. |
| Updater | Retain resolved installation/temp base directories, nested no-link protections, diagnostic logging, cache checks and isolated Windows/Wine flow tests. Preserve development-sample update isolation. |
| Controller smoke | Combine upstream recovery expectations with existing explicit conflict-rejection checks; do not restore occupied-binding swaps. |
| Reconnect diagnostic | Retain upstream production-code checks; explicitly evaluate canonical navigation before testing Pause and reject unassigned raw menu confirmation. Add a portable entry point to run the same diagnostic. |
| Native DLL | Retain the exact incoming DLL blob. It is an upstream binary, not a DLL compiled or validated from this merged source. |
| Historical reports | Retain upstream release/audit reports as historical reports. Their Wine, rendered and hardware-related statements are not new validation of this branch. |

Legacy rendered controller/Online smokes still contain assumptions about the old
menu and automatic pad aliases. They have not passed against unified Controls;
use the current Controls acceptance guide and explicitly assigned navigation.
Updating those old rendered fixtures is pending, not evidence of a runtime pass.

## Validation

Portable production-code checks passed: 215 multi-input/setup/view/navigation,
96 snapshot/provider, 57 Controller, 46 UAT storage/lifecycle/export, 34 diagnostic,
9 package checks, access audit and native JSON compilation/execution. The provider
suite still compares unchanged behavior against the pre-.38 provider. Two old
four-assertion parity scenarios are replaced with three explicit recovery
assertions, explaining 101 to 96 checks; the intentional fallback difference is
not hidden by changing the baseline.

The adapted production reconnect diagnostic passed all 16 checks, including
menu-locked replacement, Keyboard only, stale-input invalidation, independent
held-axis release and keyboard recovery. Leaderboard tests passed 41/41.
Two Tsubaki tests passed against exported merged-index assets: original center/
height/unaffected edge preservation and native collision indices/endpoints.
The large all-weather mesh comparison was not run in this sparse checkout.

C# syntax parsing passed for 122 top-level runtime scripts, four Controls files
and 39 editor files with `UNITY_EDITOR`. This is not Unity semantic compilation.
Whitespace checks, unresolved-index checks and conflict-marker scans passed.

Synthetic timing: complete eight-device/32-control provider and mapper fixture
was about 11,912 bytes/frame and 103 microseconds; focused menu dispatch was
0 bytes/evaluation. These are portable adapters, not target hardware timing.

Reproduce with .NET 8, Python 3.11+, clang++ and Node supporting node:sqlite:

```sh
python3 Tools/CI/Run-Portable.py
dotnet run --project Tests/MultiInput/MultiInput.csproj -- -idas3-reconnect-check-output /absolute/fresh/evidence-folder
(cd Leaderboard && node --test test/*.test.mjs)
```

Portable logs are written to `Verification/ci/portable`; reconnect writes
`report.json` in the fresh output folder. Neither test writes player preferences.
This merge has no new CI run, Windows native build, Unity build, rendered UI,
Wine run, runnable player or hardware result. The Mac has no Unity editor or
connected Windows execution environment. The owner's prior activated Windows
Hub/baseline build is not invalidated and does not validate this merge.

After owner review, recheck origin and publish by normal fast-forward. Build the
exact merge commit in the owner's separate full Windows checkout using the
existing sample build workflow; retain native/Unity/package logs. Follow
`docs/UNIFIED_CONTROLS.md` for mixed steering/pedals/shifts, explicit navigation,
Online, Save/Discard and original-configuration checks. Reconnect/restart still
requires explicit reassignment; physical matching and experimental FFB remain
out of scope. Do not overwrite the owner's installed game.
