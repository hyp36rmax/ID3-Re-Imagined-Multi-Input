# Bounded sample acceptance and evidence

Status: NOT TESTED on Windows or hardware. First require a complete game player package with exact source SHA and passing game/native reports. Record the Windows version, Unity/toolchain reports, actual hardware models, driver versions, owner-confirmed operating mode and whether pedals/shifter connect through the base or separate USB. Copy driver-reported names exactly; do not infer hardware from them. Fanatec combinations remain pending until observed.

Reuse the dedicated root `%LOCALAPPDATA%\ID3IdentityProbe\UAT`, with a separate `MultiInputSample\<date>-<short-sha>` run folder so gameplay evidence cannot be mistaken for the M1-A-P1 identity campaign. Create `share` for reviewed results/build reports and `private` for unreviewed screenshots/logs. Keep original-game saves and probe campaign keys/raw captures outside the share folder. Write `share/results.md` using this template:

```text
Source SHA / package checksum:
Windows / Unity / MSVC / SDK:
Wheel, pedals, shifter (actual hardware, driver, mode, USB/base connections):
Driver-reported names:
Procedure and expected result:
Actual observations (baseline / restart):
Capture/build result: PASS / FAIL / UNRESOLVED / NOT TESTED
Gameplay result: PASS / FAIL / UNRESOLVED / NOT TESTED
Physical identity: UNRESOLVED
FFB: DISABLED in experimental mode; no force test
Evidence filenames reviewed for sharing:
Limitations / next decision:
```

## Baseline

Follow the short setup guide. Before Save Changes, cancel one Setup and verify the prior draft returns. Bind steering to the wheel, accelerator/brake to pedals and shifts to the shifter; observe the actual supplying device for each. An endpoint exposed through a base is one source; separate USB hardware should appear as separate observed endpoints. Confirm no unrelated action changes, conflicts are explicit, keyboard recovery works, and long names/status can be read in Overview and Setup review.

Test Controls must show simultaneous proportional steering, accelerator, brake and shifts while gameplay remains blocked. Check reversed/resting pedal endpoints, return to rest, save while a control is held, then release and press again. Saved/draft toggle must be accurate. Keep an unrelated audio option draft pending while saving Controller changes and confirm it remains pending. Do not deliberately damage personal files to simulate persistence failures; portable/Unity synthetic checks cover that logic, and any observed real failure must retain pending edits with accurate partial-save status.

After Test Controls, perform a short offline drive and record simultaneous input, keyboard recovery and the explicit FFB-disabled state. Do not tune forces or run motor output. If the rig is unavailable, mark hardware NOT TESTED; a synthetic check does not replace it.

## Application restart

Close the sample and reopen the same package. Attach observations to the same evidence run folder with a new session note. Experimental names/calibration should remain visible as **reassign**, with neutral controller actions until explicitly recaptured and saved. Keyboard recovery should still work. This expected reassignment is a sample limitation, not an identity failure being hidden. Confirm existing-controls mode can be restored without altering its saved bindings.

Disconnect/reconnect behavior is covered synthetically; physical reconnect/reorder, identical-device, backend-transition and broader Fanatec cases remain pending review after baseline/restart evidence. Do not expand the full nine-stage identity campaign here.

## Sharing

Place only reviewed notes, package checksum/result reports and intentionally selected screenshots into `share`. Review for names, paths, credentials and personal data before ZIP export. Compress **only that share folder**, never the entire UAT/save/campaign root. Keep raw Player.log private unless a reviewed/redacted excerpt is needed. Record the resulting ZIP path and checksum in the developer log.

The separate identity probe retains its existing UI, dedicated folders, restart attachment and allowlisted Export ZIP flow; use its packaged `docs/UAT.md` for baseline/restart without gameplay or force output. A probe capture PASS does not validate this game's input or menu.

## Separate CONTROLS and WHEEL regression

Use Options → WHEEL for the five setup pages. Confirm CONTROLS remains a separate Controller & Keyboard table. Edit in either category, switch to the other, and verify the same pending assignment plus labelled saved/draft mode. Save once; reload and confirm. Repeat with Discard and with unfinished Setup cancellation; verify prior saved files and unrelated option drafts survive. Exercise directional page/category navigation and all footer buttons. Test Controls must stop on leaving WHEEL. Check draft FFB ON with saved OFF: the pending label and backend reason must explain the distinction without motor output. Include a supported non-FFB input device and long driver names. Record rendered screenshots only from the matching actual player. These checks are pending hardware/Unity evidence, not implied passes.

New Wheel Setup must label and use the separate configuration even when starting from Existing controls. Confirm original controller profiles remain after saving and switching back. Cancel must restore the prior mode as well as assignments. Intentionally editing Existing controls, shared keyboard slots or FFB remains an explicit change to original/shared settings; test those separately from preservation of new wheel assignments.
