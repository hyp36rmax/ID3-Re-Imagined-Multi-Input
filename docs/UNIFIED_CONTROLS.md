# Unified Controls: owner UAT correction

## What this replaces

The owner built and launched `73e87d617a467776803c9ff59d6754ff78fa829d` on Windows, reported passing native tests and ZIP integrity, and found broken wheel Start/Options, repeating POV navigation, awkward setup, overlapping names, and an unnecessary test-start step. These are owner hardware observations. Earlier automated tests did not establish hardware or visual acceptance.

One Controls category now contains Quick Setup, Devices, Bindings, Test Inputs, and Force Feedback. The owner’s 862 × 569 concept is the layout reference: dark panel, restrained type, red tab borders and device/status rows. The concept's device names are examples, not inferred hardware. This local checkout has no Unity renderer; matching screenshots and visual acceptance remain pending.

Quick Setup opens a Wheel/Gamepad choice immediately. Wheel uses steering → gas → brake → Shift Up → Shift Down; gamepad uses a steering-stick instruction and gas/brake controls, with optional shift steps. Every capture has six seconds, an explicit Confirm, Retry, and Cancel. Steering asks for rightward movement from center and derives the other direction from the same signed axis. End-rest pedals remain supported; a button or an end-rest axis is rejected for steering. Capturing never confirms or advances itself. Pointer buttons and keyboard Tab/Enter operate the wizard; Escape cancels. Tested/captured hardware is consumed rather than forwarded into wizard navigation. The available local OutRun source was inspected read-only for signed steering and release-before-listening behavior; no OutRun files changed.

Test Inputs begins on page entry and ends on leaving. Indicators use the same evaluated assignments as gameplay, before native response processing. Unfocused/release-blocked samples are labelled unavailable. Pointer page tabs and keyboard Escape remain usable, even while gameplay input is suppressed.

## Integration and removal

`Assets/Scripts/ControlsSetup/Idas3ControlsSetupView.cs` is the view entry point. `IIdas3ControlsServices` supplies existing services and explicit commands:

- Discovery: `Idas3ControllerDevices.Snapshot`; no additional polling.
- Binding and test evaluation: `Idas3ControlBindings`; no alternate mapper.
- Setup state: `Idas3SetupSession`; checkpoints, timer and confirmation, not device reads.
- Persistence: adapter calls the existing `Idas3ControllerSave` coordinator.
- FFB: the existing `Idas3WheelFeedback` and scoped option adjustment; the view cannot send forces.
- Game adapter: `Idas3PauseMenu.Controller.cs`, plus small routing hooks in `Idas3PauseMenu.cs` and `Idas3SceneGame.cs` for opening, closing, gameplay blocking and menu ownership.

The old Wheel category index remains reserved internally to avoid renumbering unrelated categories and existing FFB adjustment code. It is not rendered; old SelectTab(4) callers map to Controls. The duplicate old Controls renderer is removed.

Upstream can replace/remove this UI by replacing the pause-menu adapter and its render/navigation hooks. Snapshot discovery, bindings, saved schemas and FFB remain independent. `Idas3ControlBindings.Setup.cs` adds only steering pairing and compact display helpers. `Idas3MenuExcursion` is the backend menu release guard and should remain when replacing the view.

## Configuration ownership

Opening settings loads existing keyboard/controller profiles and the previously selected wheel assignments. Merely opening/navigating/testing writes nothing.

- Original controller/keyboard storage and schema are unchanged. Bindings edits to the loaded original controller profile are explicit edits to that profile; keyboard slots still share the existing keyboard configuration.
- Quick Setup uses the existing separate version-2 `experimental-input.json`. It does not replace original controller profiles. The word “experimental” is a storage detail, not a player mode switch.
- After wheel setup, controller binding edits apply to those separate assignments. The Devices page's explicit “Use saved controller setup” returns to the original controller profile on Save; separate wheel assignments remain stored. Re-entering Quick Setup starts from retained assignments, and Cancel restores the previous selection/edits.
- Menu assignments live in the separate file, even when original driving controls are selected. Adding a menu assignment does not switch driving ownership. Explicit menu excursions suppress legacy aliases for that sample; outside those excursions the original controller's menu controls remain available. Separate wheel driving has exclusive controller ownership.
- Save Changes persists pending bindings, separate assignments, selected-device edits and FFB through the existing coordinator. Unrelated audio/display drafts remain pending. Successful setup Save opens Test Inputs.
- Cancel Setup restores the pre-setup in-memory checkpoint, including earlier pending edits. Discard restores the last successfully saved Controls settings. Closing Options discards remaining unsaved edits.
- Saves span independent files. On partial failure, completed writes stay completed; remaining edits are retained, the error identifies the failed component, and closing is blocked until retry or explicit Discard. A checkpoint is retired after a binding write so Cancel cannot roll back over a successful persistence step. Starting another wizard is blocked until the partial failure is resolved.

No persistent physical identity was added. Reconnect/restart still requires explicit reassignment. Display numbers only distinguish current-session endpoints; long driver names stay intact in provider evidence/logs and Devices, while binding cells show clipped compact labels with the control first.

## Input routing corrections

The original path was provider → binding Poll → menu/driving packet → music/attract/pause handlers → native bridge. Legacy directions reached a repeating host handler; explicit menu bindings used separate excursion latches. Both are now guarded: legacy directions become one canonical event after neutral, and the pause handler no longer repeats a held direction. Explicit assignments retain their adjustable activation point and below-40% re-arm. No direction is also forwarded as a duplicate pad alias.

Start is emitted as native frontend Enter only in frontend contexts. It is not managed-settings Confirm or race Pause. Open Options / Pause has its own binding and opens attract Options directly; C/Y View Change remains a fallback. Capture, testing, context changes, focus loss and reconnect disarm or consume menu events. The UI does not claim these changes have passed on the owner's wheel until the retest below.

## Build and evidence

Run `python Tools/CI/Run-Portable.py` with Python 3.11+, .NET 8 and a C++ compiler. The local run passed 167 multi-input/setup/view checks, 101 provider/snapshot checks, 57 Controller checks, 46 UAT and 34 diagnostic checks, native JSON/access checks and 9 packaging cases. It compiles actual binding/setup/view code against headless adapters, exercises persistence, and retains logs under `Verification/ci/portable`. Headless GUI adapters test commands, not pixel layout. Unity editor checks in `Idas3ControllerFoundationChecks` were updated to use the real module adapter and are invoked by the full-game builder.

On the owner's already activated Windows machine, use a **new full checkout of the reviewed follow-up commit**, with the same successful toolchain as the baseline. Close Unity, then run from that checkout:

```powershell
git rev-parse HEAD
git status --short
$env:IDAS3_UNITY_EDITOR='C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
.\Tools\CI\Build-Windows.ps1 -Kind game
```

Use the actual installed editor path if different. Do not pass `-ReuseNative` across commits. The script builds both native DLLs, runs native and Unity checks, and packages the full game in `Verification\ci\game\ID3-MultiInput-<full-commit>.zip`. Retain `Verification\ci\native`, `Verification\ci\game`, and the Unity check reports. Only report a runnable correction after successful build, launch and retest. No hosted Unity activation is required for local Hub activation.

The owner's baseline checkout has generated native DLL changes and a ProjectSettings.asset change. Its exact settings diff was requested and is not available here. Do not keep/discard/commit those changes blindly. Leave that checkout intact; use a separate checkout for this build. This change does not modify ProjectSettings or generated DLLs.

## Short hardware retest

1. Extract the complete new ZIP into a new folder, keeping the installed game and its saves separate. Supply the owner's supported ROM as described in MULTI_INPUT_SAMPLE.md. Launch and record the package's exact commit.
2. Open Controls → Quick Setup. Cancel one attempt and verify saved configuration is unchanged. Then bind wheel, gas, brake and shifts. Verify the six-second countdown, timeout/Retry, correct wheel direction, pedals resting at extremes, and explicit Confirm on every step. Save.
3. Test Inputs must already be live. Hold steering and both pedals while operating shifts. Verify proportional indicators, keyboard access, and no gameplay/menu actions from tested inputs. Leave with pointer or Escape.
4. Bind Start, Open Options / Pause, and needed navigation under Bindings → Menu, then Save Changes. Return to the title, release everything, press wheel Start, then Open Options in an allowed attract context. In a race, verify Pause separately. A Start press must not Confirm a settings button or toggle Pause.
5. Press/hold/release each POV direction. Expect one event until release; test diagonal and opposing directions, held-at-entry, focus loss/regain, capture completion and leaving Test Inputs. Check optional axis navigation threshold/re-arm without changing driving calibration.
6. Run a short offline drive with simultaneous steering, pedals and shifts. Multi-input FFB must remain unavailable; no motor test or force tuning. Repeat Save/Discard and verify original files/presets and unrelated pending audio settings survive.
7. Capture real screenshots at 862 × 569 (the supplied reference size) and the owner's normal game resolution: Devices with long names, Bindings, capture/review, Test Inputs and FFB. Record any clipping or focus failure; do not substitute a concept image.

Fanatec base-attached vs separate USB pedals/shifters, device operating modes, aliases, and model compatibility remain hardware cases. A driver name is not evidence that a particular hardware combination works. Original-game reports of digital-feeling input, limited multi-USB support and excessive FFB are not claimed fixed by this sample.
