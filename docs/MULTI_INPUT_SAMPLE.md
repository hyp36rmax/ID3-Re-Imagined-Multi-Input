# Windows multi-input development sample

The owner built and launched baseline commit `73e87d617a467776803c9ff59d6754ff78fa829d` on Windows and reported native-test and ZIP-integrity success. Hardware UAT then exposed menu/setup failures. The unified Controls correction is described in [UNIFIED_CONTROLS.md](UNIFIED_CONTROLS.md); its Windows build and hardware acceptance are pending.

## Quick start after a successful corrected build

1. Extract the entire `ID3-MultiInput-<commit>.zip` into a new folder, separate from the installed game. Keep its README and build reports with the exact commit.
2. Supply your own supported GDS-0033 dump beside the EXE: `rom/gds-0033.chd`, or `rom/gds-0033.cue` plus `rom/gds-0033-track1.bin`, `rom/gds-0033-track2.bin`, and `rom/gds-0033-track3.bin`. The ROM gate validates it. Run `InitialDUnity.exe`. Separate settings/saves use `%USERPROFILE%\AppData\LocalLow\Chris\Initial D Multi Input Sample`; release updates and community uploads are disabled. Use offline play.
3. Open Controls → Quick Setup. Choose Wheel or Gamepad, rest controls, then follow each six-second capture. Turn right for steering; the other direction is derived. Confirm each detected input. Retry starts another window; Cancel restores prior settings. Shifts may retain existing assignments.
4. Review and Save. Test Inputs opens live immediately. Test wheel/stick, gas, brake and shifts together. The page consumes those inputs; leave with the pointer or keyboard Escape.
5. Bind distinct Start, Open Options / Pause, and desired navigation in Bindings → Menu. Save Changes, return to the title, release controls and test Start/Options/POV. Keyboard recovery remains available. Do a short offline drive only after checking assignments.

Force output remains disabled for the separate wheel setup. Existing FFB settings are retained; settings never run a motor test. Restart or reconnect requires reassignment.

## Current limits

- Assignments are valid for one endpoint connection in one application session. Restart keeps names/calibration visible but requires reassignment. Disconnect makes only affected actions neutral; reconnect requires explicit reassignment and release. A different device is never substituted automatically.
- Session tokens, connection generations, native slots, runtime IDs, model names and enumeration order do not establish persistent physical identity. Available identity evidence remains unresolved. No Windows/Unity association is invented.
- A missing/invalid/unavailable control or a sample older than 250 ms is neutral logical input; it is never replaced by a fabricated raw zero. Out-of-order frames are rejected. Capture uses the existing calibration and held-input safeguards.
- Native XInput retains authority over known Unity XInput-family mirrors. No HID device is suppressed by VID/PID or friendly name. Unknown HID/XInput aliases and backend transitions remain hardware cases; do not intentionally assign two representations of the same physical control.
- Supported controls are those already exposed by the provider. Driver-specific HID layouts, Fanatec base-attached versus separate USB pedals/shifters, operating modes, long-name rendering, and actual Windows device behavior remain untested. Driver names do not identify the base, rim, mode or compatibility.
- Native response, deadzones, smoothing, physics and force calculations are unchanged. Reports of digital-feeling input, limited multi-USB support and excessive FFB concern the original game; this sample is not a demonstrated fix for those reports.

Build owners: see [Windows build and Actions handoff](WINDOWS_SAMPLE_BUILD.md). Testers: see [bounded sample acceptance and evidence](MULTI_INPUT_UAT.md). Do not expand into the full identity campaign or force testing without review.

## Implementation and local evidence

`Idas3ControlBindings.MultiInput.cs` is part of the existing binding class. It builds a bounded current-inventory lookup of session/generation/local-path references, then uses the same capture calibration and driving evaluator. One provider Tick supplies both existing and experimental paths. Legacy raw controller bits are removed while experimental mappings are active. No second reader, persistent-identity resolver, backend replacement or binding migration is introduced.

See [Unified Controls](UNIFIED_CONTROLS.md) for current validation, module interfaces, configuration ownership and the owner rebuild procedure. Original controller profiles remain available; Quick Setup creates separate wheel assignments without replacing those profiles. No internal input-mode switch is exposed by the replacement interface.
