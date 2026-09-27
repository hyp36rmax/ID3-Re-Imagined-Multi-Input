# Windows multi-input development sample

This sample adds explicit action assignments across connected controllers. It is an experimental extension of the existing WHEEL menu and binding evaluator. Source and portable checks are implemented; no playable build or hardware pass is established until the matching Windows package and its reports exist.

## Quick start after a successful build

1. Extract the **entire** `ID3-MultiInput-<commit>.zip` into a new folder. Keep the EXE, UnityPlayer, data and Mono folders together. Check the exact source commit in READ ME.txt against the accompanying PASS build reports. A probe ZIP is a different application and cannot validate gameplay.
2. Supply your own supported original GDS-0033 dump: `rom/gds-0033.chd`, or `rom/gds-0033.cue` plus `rom/gds-0033-track1.bin`, `rom/gds-0033-track2.bin` and `rom/gds-0033-track3.bin`, beside the EXE. The ROM gate validates the dump. No ROM, personal save or music is distributed. Run `InitialDUnity.exe`. This product uses separate settings/saves under `%USERPROFILE%\AppData\LocalLow\Chris\Initial D Multi Input Sample`; release updates and community-time uploads are disabled for this sample. Use offline play for this test.
3. Open Options → WHEEL. Select **INPUT: EXISTING** once to switch the draft to **INPUT: MULTI**. Choose New Wheel Setup in Quick Setup (it also selects the separate mode automatically if starting from Existing controls). Center the wheel and rest the pedals before capture. Steering is one stage with sequential left/right prompts, followed by accelerator, brake and optional shifts. Move only the intended control. Each action records the actual supplying endpoint; moving several controls together is rejected rather than guessed.
4. Review device/control assignments. Overview can show saved or draft assignments with wrapped driver names and reassignment status. Bind Controls can rebind or clear only the chosen action; clear a named conflict explicitly. Keyboard slots and Escape/F1 recovery stay available. Cancel Setup restores the earlier draft. **Save Changes** saves the shared CONTROLS/WHEEL settings only, preserving unrelated option drafts. On a partial failure, retry or explicitly discard; earlier successful writes remain saved.
5. Use Test Controls before driving. Values are evaluated assigned input before native response processing; gameplay is blocked during the test. Check steering, both pedals and shifts together. Release newly saved controls before using them. Escape leaves the test. Complete a short offline drive only after this check.
6. To return to existing controls, toggle **INPUT: MULTI** back to **INPUT: EXISTING**, then Save Changes. Existing mappings are preserved. Discard cancels a mode change. Experimental preferences stay in a separate `experimental-input.json` file; no automatic import or migration is performed.

**FFB is disabled while multi-input is enabled or previewed.** Steering-to-output ownership has not been validated. Existing FFB settings are preserved for the existing-controls path. There is no motor-test feature or force tuning in this sample.

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

Portable checks currently cover 38 multi-input, 101 provider/snapshot and 50 Controller cases. The eight-device/32-control combined fixture measured 11,912 bytes and 98.87 microseconds per frame in this local run (~0.71 MB/s at 60 frames/s). Provider-only allocation is 11,817.4 bytes per tick; the small five-source integration alone measured 64 bytes/poll. These are synthetic .NET measurements, not Unity GC or hardware frame times. Active lookup metadata is replaced on inventory change; the provider's existing session tombstones and caller-retained historical frames remain separate memory costs. Broad optimization is deferred until target-player measurements justify it.

CONTROLS keeps the familiar keyboard/controller table and edits the same draft shown in WHEEL. The saved active mode and draft mode are labelled separately. Choose input mode in WHEEL; merely changing category does not change the mapping engine or save assignments. Save Changes / Discard Changes affect both categories together.
