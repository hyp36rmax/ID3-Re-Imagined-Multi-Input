> Current UI: [Unified Controls](UNIFIED_CONTROLS.md) replaces both old entry points. The storage boundaries below remain; references to separate CONTROLS/WHEEL screens describe the prior interface.

# Input configuration ownership and pre-push review

Reviewed starting source: `2a32bfdfed106e11a840f43272b7c7e06b1b7f37` on `ID3-Multi-device-input`. On 2026-09-27, local HEAD and the live origin branch matched, with no unpublished commits and a clean tree. Main/default remained `377e4ad9451a34ed4bc0acf735038372d8dec876`. This follow-up is local only.

## What CONTROLS and WHEEL share

Categories are two views of the same editing session, not independent input providers. The saved active mode and draft mode are labelled separately. Navigating between categories neither saves nor selects another gameplay mapper.

| State | Owner and persistence | Effect of editing |
| --- | --- | --- |
| Capture, conflicts, calibration, evaluation | `Idas3ControlBindings`, shared by both categories | One capture state and one evaluator; no temporary mapper |
| Existing controller draft/profiles | `draft` / `draftProfiles`, alongside `current` / `savedProfiles` | Explicit editing in Existing controls changes that selected profile only when Save Changes is chosen |
| Original keyboard and controller settings | `controls.json`, existing schema, versions 1–3 still readable | Original keyboard slots are shared across modes. Explicit keyboard changes save here. Controller profiles are not replaced by multi-input assignments |
| New wheel/multi-device assignments | `experimental-input.json`, version 2 (reads version 1) | Separate ten driving actions, eight menu actions, menu activation and enabled mode. New Wheel Setup uses this store even when starting in Existing controls |
| Device selection | `Idas3ControllerDevices`, `controller-device.json` | Nonpersistent preview until Save; Cancel/Discard restores the saved selection. Multi-input references per-action endpoints and does not use this selection to aggregate input |
| FFB | `Idas3GameOptions.Current` / `Draft`, existing options file | Four shared fields: enable, strength, inversion, output device. Save scopes these fields and retains unrelated drafts. Unchanged FFB now does not rewrite the options file |

Both categories share the existing draft and the separate multi-input draft; switching pages does not copy one over the other. Original files and schemas are unchanged. There is no automatic import or rewrite when opening the menu. Loading an older supported original schema migrates in memory through the existing reader; only explicitly saving original changes writes that file.

## Non-destructive setup and deliberate edits

Before this review, Quick Setup in Existing controls modified the original controller draft. Saving that setup could replace the player's original assignments. **New Wheel Setup now selects separate multi-input assignments after taking a checkpoint.** It works with a supported single controller as well as multiple input devices and does not filter for FFB. The experimental file provides the isolation boundary without duplicating the mapper. The navigation follow-up extends that file to version 2 while retaining version-1 loading.

Cancel Setup restores the prior draft and mode. Leaving unfinished Setup also restores that checkpoint. Completing Setup and choosing Save Changes persists its assignments to the separate file; the original controller profiles remain intact. INPUT: EXISTING followed by Save Changes returns to those original mappings, subject to the existing release/reconnect guard. This sample disables force output whenever the multi-input mode is saved or being previewed; New Wheel Setup therefore does not provide FFB yet.

**Existing controls remains an intentional editor, not a read-only backup.** Direct rebinding there followed by Save changes the selected original profile. The footer now says so and points to New Wheel Setup for preservation. Keyboard edits are also intentional changes to the original shared keyboard configuration. FFB changes intentionally update shared options. Pending original/keyboard/device edits made before starting Setup remain pending and are included in the shared Save action; Setup does not erase or hide them. The preservation tests isolate new wheel assignment changes from such explicit edits.

Save is the same coordinator in either category: original bindings if dirty, separate multi-input preferences if dirty, device selection if changed, then FFB if changed. Separate files are not an atomic transaction. On failure, later writes stop; the message identifies completed steps and pending edits. Retry or Discard remains available. Discard restores each component's last successful save and preserves unrelated audio/display drafts; it cannot undo a component already saved. Closing after partial failure is guarded. Application shutdown does not persist unsaved drafts.

## Gameplay and reconnect contract

`ExperimentalEnabled` chooses the saved controller path. `ExperimentalDraftEnabled` chooses capture, labels and Test Controls only. The host polls the existing provider once and passes its coherent frame into bindings. In multi-input mode, `ExperimentalScope` supplies the per-action control dictionary to the original evaluator and clears the legacy raw pad contribution. Keyboard recovery remains available. Menu/testing neutralization remains a separate host safety boundary.

Preferences persist display names, local control paths and calibration, not physical identity. Session tokens and connection generations are runtime-only. Disconnect removes usable samples, reconnect requires explicit reassignment, and restarting loads preferences labelled **reassign**. There is no automatic association by model name, enumeration order, VID/PID or XInput slot. Invalid, stale and out-of-order samples are unusable; release guards apply before newly available/assigned held controls can drive actions. The original single-controller provider still uses its existing profile keys; identical devices may share a legacy profile. That compatibility behavior is not physical identity and is not reused as a multi-input enrollment decision. Known native/Unity XInput mirrors remain suppressed by the provider; unrelated HID devices are not removed solely for matching VID/PID.

## Responsibilities and extension points

- `Idas3ControllerDevices.Snapshots.cs` publishes from the existing polling loop; `Idas3DeviceSnapshot.cs` owns immutable frame/status/value contracts. `IIdas3ValidatedIdentityEvidenceSource` is a future evidence seam, not implemented enrollment or physical matching.
- `Idas3ControlBindings.MultiInput.cs` owns versioned source assignments, session validation and a scoped adapter into the existing mapper. `BeginWheelSetup` explicitly chooses isolated assignment ownership and returns a cancellation checkpoint.
- `Idas3PauseMenu.Controller.cs` handles WHEEL navigation and delegates drawing to five named page methods. `SaveControllerChanges` and `DiscardControllerChanges` are shared with CONTROLS in the existing pause menu. Persistence order lives in `Idas3ControllerSave`; presentation text lives in `Idas3ControllerStatus`. Wheel-field equality belongs to `Idas3GameOptions`, avoiding two definitions of pending FFB.
- This review expands our new production modules into multiline formatting, splits the large renderer by page, and makes navigation/save method names explicit. Original provider/binding/menu files are changed only at integration points, not reformatted repository-wide. Serialized field names remain unchanged.
- Upstream HEAD is `80923197cb739fecc3e91da20992e19ebcc75882` (.38). Its `RigSamples` / `PollRig` aggregation and reconnect/capture changes overlap these same integration points. Nothing was merged. Reconcile that overlap explicitly; do not retain both aggregation paths or claim merge readiness merely because portable tests pass.

## Validation and build handoff

From the repository root with .NET 8, Python 3.11+ and a C++ compiler:

```sh
python3 Tools/CI/Run-Portable.py
dotnet run --project Tests/MultiInput/MultiInput.csproj --property:UseSharedCompilation=false
dotnet run --project Tests/ControllerFoundation/ControllerFoundation.csproj --property:UseSharedCompilation=false
```

Local results: 34 diagnostic, 46 UAT, 57 Controller, 101 snapshot/provider, 52 multi-input and 9 packaging checks passed, plus the access audit and portable native JSON test. The 14 additional ownership checks execute production binding/provider/save code: new setup cancellation, byte-for-byte original bindings/device preservation through combined saving, two original profiles, return-to-existing evaluation, and exclusion of the held original pad from simultaneous multi-input evaluation. Controller checks additionally prove unchanged FFB avoids persistence/platform calls and retains unrelated drafts. Unity menu checks cover navigation and both Save/Discard entry points but have not run here. C# syntax parsing and actionlint are separate static checks, not a Unity semantic build.

### Historical Actions status at ownership review, checked 2026-09-27

[Run 36341394023 for 2a32bfd](https://github.com/hyp36rmax/ID3-Re-Imagined-Multi-Input/actions/runs/36341394023) completed with failure:

- Portable job: passed.
- Windows native job: failed during CMake generation, before DLL/test compilation. Windows Server 2022 10.0.20348, VS 2022 17.14.41, MSVC 19.44.35229, SDK 10.0.26100.0 were reported. Sparse checkout omitted `Native/tools`, including `check_original_music_sequence.cpp`, `capture_unity_lighting_panels.cpp`, and `check_course_oil_application.cpp`. This follow-up adds that directory to checkout; Windows rerun is pending.
- Unity readiness: executed but reported not ready. The route variable and all three activation inputs were empty. The Unity matrix was skipped. A green readiness job means the check ran, not that Unity built.
- Artifacts: portable reports and native failure reports only. **No runnable game/probe package.** No CI execution of this local follow-up has occurred.

The earlier d7a51f0 run also failed overall. No workflow was dispatched or rerun during this review. No credentials or repository settings changed.

Windows prerequisites remain Unity **6000.6.0f1**, Windows Mono support, valid activation, MSVC/Windows SDK, CMake, Python and a full game asset/Steamworks checkout. The Mac has no Unity or rendered player and its checkout is sparse. See [Windows handoff](WINDOWS_SAMPLE_BUILD.md) for exact approved-source build commands. Use an already activated owner Windows machine, or review/configure the documented eligible hosted activation route; no new licensing arrangement is assumed. Unity import/compilation, real JsonUtility checks, rendered UI, complete packaging and all physical hardware acceptance remain unverified.

Stop at local review. After approval, publish normally, inspect the exact-commit Windows/Unity results, and only then perform the bounded baseline/restart and CONTROLS/WHEEL acceptance. Persistent enrollment and expanded hardware campaigns remain separate work.

## Navigation follow-up

[Navigation ownership and upstream decisions](WHEEL_MENU_NAVIGATION.md) supersede the version-1/menu-derived behavior: menu assignments now persist explicitly in version 2. Per-control reconnect guards and stable generic selection were selectively adapted from .38; its aggregation was not imported. At published ad3a18f, Windows native compilation/tests now pass; Unity activation remains missing and no runnable player exists. The original file-preservation contract is unchanged.
