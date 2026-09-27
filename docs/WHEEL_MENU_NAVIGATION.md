# WHEEL navigation and upstream reconciliation

## Player workflow

Use **WHEEL → Bind Controls → Menu** for Up, Down, Left, Right, Confirm, Back / Cancel, Pause / Open Menu and Start (frontend). The Driving group keeps the driving assignments and keyboard slots. Select INPUT: MULTI to edit the separate configuration. The experimental driving Pause row links to Menu/Pause; older experimental driving Pause assignments do not implicitly become navigation. Existing CONTROLS mode keeps its original menu conventions and saved profile format.

Each Menu action uses the existing Rebind / Clear / Back chooser and capture service. Buttons, paddles, shifter buttons, individual POV directions and directional axes use the same session-scoped endpoint references as driving. Move one control at a time; a diagonal during capture is ambiguous and is rejected. Capture each POV cardinal direction separately. A duplicate source/direction within Menu is rejected with the conflicting action. One source may intentionally be used in Driving and Menu; changing groups does not silently assign it.

Quick Setup offers:

- **POV / Buttons:** enter the Menu assignment group and explicitly capture each desired action. No device layout is guessed.
- **Arcade Navigation:** propose Up/Down from shift up/down, Left/Right from steering, Confirm from accelerator and Back from brake. Review the actual device/control names in the Menu group before Save Changes. Pause and Start retain their current independent assignments and can be captured there. Missing driving assignments remain unassigned. A proposal conflicting with retained navigation is rejected without replacing the menu draft.
- **Keep Current Nav:** leave current navigation assignments unchanged.

Finish the driving setup review before choosing optional navigation. Setup's checkpoint covers driving, menu assignments, mode and activation point. Cancel Setup/Discard restores the prior checkpoint/saved state. A completed setup can be reviewed across pages. Save Changes from either category uses the same ordered persistence coordinator and partial-failure handling. Original controller files are not migrated into the new store.

## Deliberate axis processing

Menu navigation is separate from driving response. Default activation is **75% of calibrated directional travel**. Return **below 40%** to re-arm. One event is emitted per excursion; there is no hold-repeat setting or repeat path for these assignments. Confirm and Back never repeat. Existing keyboard recovery/navigation remains available.

Menu capture stores the measured rest and extent at the first detected motion. It does not require a wheel's mechanical stop and never changes driving calibration. For arcade proposals, the initial menu calibration is copied from the reviewed driving assignment; use Rebind in Menu to establish a comfortable menu-specific extent. **Menu Axis Activation Point** changes only menu activation, in 5% steps from 45–95%; the automatic release point stays 40% so hysteresis remains valid. These are adjustable development values, not hardware-validated defaults.

Menu entry, category/page and native frontend-stage transitions, context/test changes, focus changes, saving/reassigning and unavailable/reconnected endpoints disarm navigation. Paired axis directions must observe a shared neutral region after entry; an opposite deflection alone does not satisfy that guard. A held button/POV must release; an axis/pedal must return below the re-arm threshold before a fresh event. Disconnection affects only that endpoint's actions. Reconnection and application restart require explicit reassignment; saved friendly names and paths are not persistent physical identity.

Opposing directions cancel. A diagonal consumes both excursions and emits only the vertical direction; holding the horizontal component afterward cannot produce a second event. Simultaneous command priority is Pause, Back, Confirm, Start, then directions. This is deterministic and documented so physical acceptance can assess it. Capture rejects ambiguous simultaneous sources rather than inferring aliases. Existing known native/Unity XInput mirror suppression and synthetic-axis filtering remain intact.

Test Controls displays normalized directional menu travel, activation/re-arm/between-threshold regions, availability, armed state, current event and last event. Draft events are not forwarded into the menu packet. The host also blocks surrounding menus and gameplay while testing. Escape and pointer page buttons remain exits; held controls must re-arm after leaving. An unavailable source is labelled unavailable, not shown as a successful zero test.

## Start, Confirm and Pause

`Native/src/main.cpp` accepts Return, A or Start to confirm frontend selection. Outside the frontend, Start/Escape have additional pause and finish-sequence skip semantics; they are not universally equivalent to Return/A. Managed Options, multiplayer and music overlays use Confirm (Return/A) and Back; host pause uses Escape. Therefore eight explicit actions are exposed rather than conflating Start, Confirm and Pause:

- Confirm emits Return in menu context.
- Back emits Backspace and native B as one semantic event (existing consumers choose a single Back branch).
- Start emits the native Start bit for native frontend/results semantics; it is not a substitute for managed Confirm.
- Pause/Open Menu is routed through the host's existing Escape/pause ownership rules, including its title-screen protection. Keyboard Escape and the original keyboard Pause assignment remain available.

During driving only the saved driving evaluator supplies controller gameplay input; normal menu actions are not merged into it. A fresh Pause request is handled by the host, which neutralizes the transition packet. During menus the explicit menu adapter clears raw controller fields and emits only assigned events. Test previews cannot inject those events into surrounding UI. No extra hardware poll or parallel aggregation engine was added.

## Configuration and extension boundaries

`experimental-input.json` is now **version 2**, adding eight `menuActions` plus `menuActivation`. Version 1 reads in memory with empty menu assignments and 75% activation; loading never writes the file or derives navigation from driving. Existing driving preferences remain present. A subsequent explicit changed save writes version 2 using the existing temporary-file/replace/previous-backup path. Original `controls.json`, controller profiles, device preference and options schemas are unchanged. Menu runtime endpoints, connection generations and latch state are not serialized.

`Idas3ControlBindings.Menu.cs` owns assignment/capture adaptation, proposals, menu latches and event translation. Calibration math is shared with the existing mapper. `Idas3ControlBindings.Reconnect.cs` owns adapted legacy per-control release guards. The host provides context/focus once after its ordinary Poll, and WHEEL renders/edits through existing menu services. Snapshot identity association remains unresolved; this change adds no physical matching or FFB tuning. Multi-input mode still disables FFB pending ownership validation.

## Upstream .38 decisions

Starting HEAD/live working branch: `ad3a18fecad4e1ddfdbddd81c3496edfcc87ac90`, clean and synchronized. Live upstream remains `80923197cb739fecc3e91da20992e19ebcc75882`. Compared overlapping provider/binding changes before editing. No merge or main/default change was performed; selective adaptation avoids replacing the current implementation.

- Adapted per-control reconnect guards for original pad axes/buttons and calibrated generic control paths. A held reconnect pedal no longer blocks a fresh paddle. Existing experimental per-assignment release guards remain.
- Adapted stable active generic-device selection: another connected generic component's activity cannot steal a connected generic active device. Explicit selection still works.
- Kept our existing menu-locked fallback behavior. Applying upstream's additional automatic fallback-under-lock change broke an original provider parity regression; that extra change was removed.
- Did **not** import `RigSamples`, `PollRig`, `rigAmounts` or implicit profile aggregation. Exactly one saved controller path is selected: existing single-active controls or explicit per-action multi-input.
- Retained/recreated relevant simultaneous-input, reconnect-held, calibrated-rest, fresh-paddle and stable-selection regressions in production-code portable tests. The broader .38 release is not represented as merged; unrelated changes require a separate review.

## Validation and remaining acceptance

Run `python3 Tools/CI/Run-Portable.py` with Python 3.11+, .NET 8 and a C++ compiler, or run `dotnet run --project Tests/MultiInput/MultiInput.csproj --property:UseSharedCompilation=false` for focused checks. Unity's sample builder also invokes `Idas3MultiInputChecks.Run`, now covering real Input System pedal navigation, menu group selection and JsonUtility menu round-trip. Those Unity checks require the pinned editor and have not run locally.

Current local results: 34 diagnostic, 46 UAT, 57 Controller, 101 snapshot/provider, 123 multi-input and 9 packaging checks pass, plus the access audit and portable native JSON test. The menu-only fixed-snapshot fixture measured 0.0 bytes/evaluation and about 0.22 microseconds; this excludes polling/rendering and is not Unity or hardware timing.

Portable coverage includes jitter/sub-threshold motion; one event per excursion; no repeats; hysteresis; left/right independence; reversed/asymmetric travel; either pedal rest endpoint; held entry/focus guards; POV diagonals/opposites; separate Confirm/Back/Start/Pause; keyboard recovery; draft-test packet isolation; explicit cross-group reuse; proposal conflicts/checkpoint cancellation; version-1 loading; failed save retention; original file preservation; reconnect/reassign; and per-control legacy reconnect guards.

On the actual rig, verify comfortable steering capture, pedal rest and noise, all POV cardinal/diagonal transitions, fresh Confirm/Back, focus loss, device removal/reassignment, pointer/Escape test exit, both categories' Save/Discard and restart. Capture actual rendered screenshots at target resolutions, including long names and the eight menu rows. No hardware acceptance or rendered UI pass is claimed here.

## Build evidence

[Actions run 36343935859 at ad3a18f](https://github.com/hyp36rmax/ID3-Re-Imagined-Multi-Input/actions/runs/36343935859): portable passed; Windows compiled **Idas3Unity.dll** and **Idas3WheelFeedback.dll** and all three native tests passed (steering smoothing, original FFB, original FFB owner). MSVC 19.44.35229 / Windows SDK 10.0.26100.0 were reported. This confirms the prior Native/tools checkout correction.

Unity activation inputs were empty, the Unity matrix skipped, and overall readiness failed. Native DLL/report artifacts are not a complete runnable player. No CI run was started for this local navigation follow-up. Next requirement: review this source, then provide the documented eligible activation route or use an already activated owner Windows machine with Unity 6000.6.0f1, Windows Mono support and full assets. Run the game and probe builders, inspect exact-commit logs/packages, then perform the bounded runtime acceptance. Unity compilation, rendered UI and hardware remain unverified.
