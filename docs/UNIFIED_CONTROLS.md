# Unified Controls: owner UAT correction

## What this replaces

The owner built and launched `73e87d617a467776803c9ff59d6754ff78fa829d` on Windows, reported passing native tests and ZIP integrity, and found broken wheel Start/Options, repeating POV navigation, awkward setup, overlapping names, and an unnecessary test-start step. These are owner hardware observations. Earlier automated tests did not establish hardware or visual acceptance.

One Controls category now contains Quick Setup, Devices, Bindings, Test Inputs, and Force Feedback. The owner’s 862 × 569 concept is the layout reference: dark panel, restrained type, red tab borders and device/status rows. The concept's device names are examples, not inferred hardware. This local checkout has no Unity renderer; matching screenshots and visual acceptance remain pending.

Quick Setup immediately starts Steering → Gas → Brake → Shift Up → Shift Down. There is no Wheel/Gamepad/Keyboard choice. Steering defaults to Axis: rest at center, move right once, and the existing evaluator derives both directions. Buttons / Keys captures Left then Right within that same stage. Each capture has a visible six-second window and explicit Confirm, Retry and Cancel. Changing steering type restores the pre-steering checkpoint and restarts only that stage. Retry/timeout preserves the pre-capture assignment; Cancel restores the whole pre-setup draft. Gas/brake accept axes or digital controls, and shifts accept digital controls. Keyboard input joins the existing capture/evaluator through values already read by Poll; no additional hardware polling or alternate binding engine was added. Existing reserved-key and conflict checks remain in force. Captured keys cannot also confirm: wizard keyboard commands require the capture/release guard to clear.

Bindings has Driving (10 actions), Menu (8 actions), and Keyboard (all 10 keyboard driving actions plus the 8 menu/recovery actions) groups. Every selected group fits in the panel without scrolling. Keyboard retains three existing slots. Menu assignments are shared between Menu and Keyboard, with fixed recovery keys retained; menu capture accepts supported keys and device controls. Compact cells identify the control first. Full driver names remain in Devices and diagnostics. Headless geometry checks cover **full game windows** of 1280×720 and 862×569, not internal content dimensions. The existing 1040×680 logical Options canvas scales with a margin; at 862×569 the smallest new group text is about 12.87 screen pixels. Geometry checks establish bounds and row inventory, not actual font rendering or readability. Rendered inspection at both sizes remains required.

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

Upstream can replace/remove this UI by replacing the pause-menu adapter and its render/navigation hooks. Snapshot discovery, bindings, saved schemas and FFB remain independent. `Idas3ControlBindings.Setup.cs` adds only steering pairing and compact display helpers. Menu release/activation ownership lives in `Idas3ControlBindings.Menu.cs` independently of the view.

## Configuration ownership

Opening settings loads existing keyboard/controller profiles and the previously selected wheel assignments. Merely opening/navigating/testing writes nothing.

- Original controller/keyboard storage and schema are unchanged. Bindings edits to the loaded original controller profile are explicit edits to that profile; keyboard slots still share the existing keyboard configuration.
- Quick Setup uses the existing separate version-2 `experimental-input.json`. It does not replace original controller profiles. The word “experimental” is a storage detail, not a player mode switch.
- After wheel setup, controller binding edits apply to those separate assignments. The Devices page's explicit “Use saved controller setup” returns to the original controller profile on Save; separate wheel assignments remain stored. Re-entering Quick Setup starts from retained assignments, and Cancel restores the previous selection/edits.
- Menu assignments live in the separate file, even when original driving controls are selected. Adding a menu assignment does not switch driving ownership. All menu packets now have exclusive explicit-menu/recovery ownership, including when original driving controls are selected. Raw controller menu defaults and automatic driving aliases are bypassed. Separate wheel driving has exclusive controller ownership.
- Save Changes persists pending bindings, separate assignments, selected-device edits and FFB through the existing coordinator. Unrelated audio/display drafts remain pending. Successful setup Save opens Test Inputs.
- Cancel Setup restores the pre-setup in-memory checkpoint, including earlier pending edits. Discard restores the last successfully saved Controls settings. Closing Options discards remaining unsaved edits.
- Saves span independent files. On partial failure, completed writes stay completed; remaining edits are retained, the error identifies the failed component, and closing is blocked until retry or explicit Discard. A checkpoint is retired after a binding write so Cancel cannot roll back over a successful persistence step. Starting another wizard is blocked until the partial failure is resolved.

No persistent physical identity was added. Reconnect/restart still requires explicit reassignment. Display numbers only distinguish current-session endpoints; long driver names stay intact in provider evidence/logs and Devices, while binding cells show clipped compact labels with the control first.

## Input routing corrections

The host path was traced in `Idas3SceneGame`: one provider Poll → `EvaluateMenuNavigation` → `ApplyDriving` or `ApplyMenu` → attract/music/pause routing → native bridge. The previous `ApplyMenuCore` automatically mapped generic-device steering/pedals/shifts into menu keys. Raw pad bits also survived when explicit assignments were idle. This confirms competing paths in source; it does **not** establish the exact cause of the owner's continuous POV movement without a hardware trace.

`ApplyMenu` now always calls `ApplyExplicitMenu`, which clears raw keys/pad buttons/axes and emits only canonical menu events. `ApplyMenuCore` and its automatic driving aliases were removed. `EvaluateMenuNavigation` combines explicit assignments with keyboard recovery at one dispatch point. Each source must release before another event. Recovery latches are separate from device availability so reconnect cannot borrow a recovery key's armed state. Directions share a neutral gate: opposing directions cancel; diagonal/held direction changes cannot produce additional movement. Axes use the configured activation point and below-40% release, with calibrated rest checked in both directions. End-rest pedals do not count as movement at rest.

Frontend Start produces Enter only in frontend contexts; managed Confirm and Open Options are separate actions. `UpdateAttractOptions` opens the actual Options screen on Open Options in eligible frontend contexts (including after Start); racing uses the pause entry point. In gameplay the saved driving Pause and explicit Open Options action enter the same guarded pause dispatcher; other Menu actions do not dispatch into driving. `RoutePauseInput` receives canonical events and has no held-direction repeat. Music and multiplayer menu consumers also receive canonical packets without wheel-specific direction swapping. Native frontend pointer clicks remain an explicit one-press path after canonicalization; Keypad Enter already belongs to keyboard recovery and is not dispatched again. The old driving-Camera shortcut no longer provides implicit controller menu entry; use Open Options or the existing pointer controls.

Capture and Test Inputs consume their samples before gameplay/native submission. Capture completion, menu/test boundaries, focus changes, and device availability changes require release. The host's wizard Enter/Tab commands additionally require `!SuppressInput`, preventing a just-captured key from confirming itself. These are source and synthetic-test findings; frontend/Options/race acceptance still needs the Windows player.

## Build and evidence

The 2026-09-28 follow-up below supersedes the historical counts and review boundary in this section.

Run `python Tools/CI/Run-Portable.py` with Python 3.11+, .NET 8 and a C++ compiler. The local run passed 215 multi-input/setup/view checks, 101 provider/snapshot checks, 57 Controller checks, 46 UAT and 34 diagnostic checks, native JSON/access checks and 9 packaging cases. It compiles actual binding/setup/view code against headless adapters, exercises persistence, and retains logs under `Verification/ci/portable`. Headless GUI adapters test commands, all-action inventory, no-scroll use and scaled bounds, not pixel rendering. Unity editor checks in `Idas3ControllerFoundationChecks` and `Idas3MultiInputChecks` use the real module adapter; stale pre-unification row-number navigation was removed. They remain unexecuted here and are invoked by the full-game builder.

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
2. Open Controls → Quick Setup. Cancel one attempt and verify saved configuration is unchanged. Then bind wheel/stick Axis, gas, brake and shifts across the available devices. Repeat Steering with Buttons / Keys and confirm Left then Right; retry and switch type before confirming. Verify the six-second countdown, timeout/Retry, correct wheel direction, pedals resting at extremes, and explicit Confirm on every step. Save.
3. Test Inputs must already be live. Hold steering and both pedals while operating shifts. Verify proportional indicators, keyboard access, and no gameplay/menu actions from tested inputs. Leave with pointer or Escape.
4. Bind Start, Open Options / Menu, and needed navigation under Bindings → Menu, then Save Changes. Return to the title, release everything, press wheel Start, then Open Options in the frontend. In a race, verify Pause separately. A Start press must not Confirm a settings button or toggle Pause.
5. Press/hold/release each POV direction. Expect one event until release; test diagonal and opposing directions, held-at-entry, focus loss/regain, capture completion and leaving Test Inputs. Verify unassigned steering, resting pedals and driving keys do not navigate. Deliberately bind an axis/pedal to Menu and check activation/re-arm without changing driving calibration.
6. Run a short offline drive with simultaneous steering, pedals and shifts. Multi-input FFB must remain unavailable; no motor test or force tuning. Repeat Save/Discard and verify original files/presets and unrelated pending audio settings survive.
7. Capture real screenshots at 862 × 569 (the supplied reference size) and 1280 × 720 full game-window resolution: Devices with long names, Bindings, capture/review, Test Inputs and FFB. Record any clipping or focus failure; do not substitute a concept image.

Fanatec base-attached vs separate USB pedals/shifters, device operating modes, aliases, and model compatibility remain hardware cases. A driver name is not evidence that a particular hardware combination works. Original-game reports of digital-feeling input, limited multi-USB support and excessive FFB are not claimed fixed by this sample.

## Follow-up review boundary (2026-09-27)

Started clean on `ID3-Multi-device-input` at `048cec11616dbf088c9ced9707f746c22b5fa8a3`, preserving `0cff912fbfb1f3fc2bee1c367e63b0b61a22cf2b`. Live `ls-remote` showed both remote main and the working branch at `80923197cb739fecc3e91da20992e19ebcc75882`, a separate .38 release whose parent is `377e4ad9451a34ed4bc0acf735038372d8dec876`. It does not contain the 14 local feature commits present at the starting HEAD. This is divergence, not synchronization. No merge, reset, push or default-branch change was performed. Local main stays at the common base. Reconciliation/publication requires a separate review after this runnable-sample follow-up.

Already satisfied before this follow-up: five unified tabs, live evaluated Test Inputs, preserved original configuration, scoped saving/partial-failure handling, driver-name preservation, and FFB protection. Changes here complete the immediate mixed-source setup flow, steering-type substeps, compact binding groups, exclusive menu ownership and current Unity batch-check entry points. No persistent reconnect matching or multi-input force output was added.

This machine remains macOS ARM without Unity; no Windows execution connection is available. The owner's successful baseline build/local Hub activation is retained as evidence, not confused with the unavailable hosted activation route. The immediate owner action is to import the review bundle into a separate worktree and run the existing Windows game build script. Build/launch logs and real screenshots—not this source review—must establish the corrected runnable sample.


## Hardware follow-up acceptance (2026-09-28)

Assignment activation now starts capture directly. Clear Slot and Cancel are in
that capture view. A same-context conflict asks “Already assigned to [action].
Replace this assignment?” Replace commits the replacement to the draft; Cancel
preserves both assignments. Save Changes remains the persistence boundary.
Separate wheel keyboard assignments shadow original keys only while that setup
is active; they do not rewrite the original keyboard profile. Driving/Menu reuse
and opposite signed axis directions remain legal.

Capture rejects simultaneous candidates and requires the entire hat to return to
neutral after a diagonal. This fixes a reproducible enumeration-dependent capture
case; the owner's exact DD2 raw reports remain unobserved. Direction labels follow
Unity's decoded Dpad children, with the original endpoint/control path retained.
Runtime diagonal navigation retains deterministic vertical priority and one event
per neutral-to-direction excursion; diagonal capture does not choose a cardinal.

Other Inputs consumes existing snapshots and sampled keys without another polling
loop. It shows assigned and unassigned activity together, up to six visible items,
with an overflow count and 0.6-second release indication. Analog hysteresis filters
small movement. Leave unassigned axes at rest when entering Test Inputs so their
rest can be sampled. Full driver names remain in Devices/evidence; compact activity
labels shorten long names without inferring hardware identity. No accessible
OutRun reference was supplied, and no OutRun files were modified.

Retest after review/publication and a successful full Windows build:

1. Select a Driving assignment and confirm: capture must open immediately. Keep
   the opening key/button/assigned menu axis held: it must not bind itself. Release,
   then bind. Try Clear Slot, Escape and pointer Cancel; unrelated slots must stay.
2. Assign a key/button already used by another Driving action. Cancel the conflict
   and verify both slots; repeat with Replace and verify only the conflicting slot
   clears. Repeat for Menu. Cancel with the source disconnected; no partial change
   may survive. Discard must restore saved state, including original keyboard data.
3. Bind steering left/right separately under Menu; reuse the Driving axis. Bind a
   deliberately selected pedal at its released rest (including maximum-rest pedals).
   Default activation is 75%; release below 40% before another event. Hold inputs
   while entering/capturing/leaving: no extra navigation or confirmation may occur.
4. Capture all four POV cardinals, checking displayed direction and resulting event.
   Attempt each diagonal, then settle on one direction without neutral: no binding
   may be accepted. Fully release and retry a cardinal. Record raw report, path,
   label, current DD2 mode and driver version if direction still differs.
5. On Test Inputs, hold steering, pedals, shifts and an unassigned button together;
   press Enter. Evaluated bars and Other Inputs must update without gameplay/menu
   actions. Release indicators should clear shortly; resting axes should be quiet.
   Disconnect, focus away and return; stale activity must disappear. Escape/pointer
   must remain usable. Check long labels and the six-item limit in the rendered UI.
6. Save, switch back to the saved controller setup and inspect its original mappings.
   Repeat Discard and setup cancellation with an unrelated audio draft pending.
   Reconnect/restart still needs explicit reassignment; no persistent matching is claimed.

Multi-input FFB remains disabled. The current interface lacks separate output-free
initialization acknowledgement and an independently owned, acknowledged test-pulse
contract. New Re-detect/Test Left/Test Right/Stop controls are not delivered or
claimed validated. See [the FFB audit](FFB_HARDWARE_FOLLOWUP.md) for exact existing
protections, proposed interface extension and DD2 evidence needed. Existing status
now says queued/driver-accepted request, with physical output explicitly unverified.

Portable validation: 287 multi-input checks, 96 provider/snapshot checks, 57 Controller,
46 UAT, 34 diagnostic, nine packaging cases, native JSON/access checks and the new
671-check fake-backend FFB owner suite. Counts include repeated mailbox checks, not
671 independent hardware scenarios. Unity raw Gamepad/HID hat fixtures were added;
their actual Unity execution, player compilation, rendering and DD2 hardware results
remain pending. This host has no Unity editor or accessible Windows executor.

Build a complete checkout of the final reviewed commit using the activated Windows
editor and the full-game command above, without `-ReuseNative`. Preserve the owner's
existing DLL/ProjectSettings changes in the old checkout; do not copy generated DLLs
into the new build. The ZIP and native/game logs are produced only by a successful
Windows run. Local source/test commits alone are not a runnable package.
