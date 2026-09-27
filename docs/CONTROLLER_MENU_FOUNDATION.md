# Controller Menu Foundation — functional single-controller baseline

One visible **Controller** category contains Quick Setup, Overview, Bind Controls, Test Controls and Force Feedback. All five pages call existing game services. This is the functional single-active-controller baseline; it does not add simultaneous USB-device aggregation or claim persistent physical identity support.

## Player workflow

1. Open Options → Controller. The device row identifies the selected device; the status line identifies the actual active device and any temporary fallback. Changing the device is now a preview until Apply. Keyboard bindings remain available.
2. **Quick Setup:** select the current controller and Start Setup. Capture steering left, steering right, accelerator and brake, accepting each assignment. Optionally capture shift up/down or keep each existing shift assignment. Release controls between captures. Review all six assignments before Apply. Cancel restores the draft that existed before Setup began; leaving the page or changing device also cancels Setup. No file is written until Apply.
3. **Overview:** toggle between saved and draft assignments. Controller assignments and all three keyboard slots are visible. Available Devices shows actual discovery status, including disconnected choices, and scrolls for longer lists.
4. **Bind Controls:** choose an action and controller/keyboard slot, then Rebind or Clear. Capture uses the existing armed/release/timeout logic. An occupied controller control is rejected with the conflicting action's name; it no longer swaps another action. Clear the named assignment explicitly, then rebind. Keyboard conflicts and reserved recovery keys retain existing validation. Clearing a disconnected controller's saved binding is allowed; capturing requires an active controller.
5. **Test Controls:** Start Live Test and exercise bindings. The page shows evaluated draft steering/pedal demand and ten action states. It uses the same binding evaluator as gameplay, before native steering response, deadzone and smoothing, which are unchanged. It does not read raw hardware again or display the packet that the menu has neutralized. Focus loss and capture suppression are labelled unavailable/guarded rather than presented as a successful zero-value test. The existing reconnect guard can suppress controller actions while keyboard recovery remains active. Physical Escape stops the test; page buttons also leave it. Held tested inputs must release before they can navigate the menu afterward; Escape remains available if a device cannot return to rest.
6. **Force Feedback:** edit enable, output selection, strength and inversion. Apply saves only these four options, retaining unrelated unsaved option edits. Output discovery and existing runtime stop/ownership/focus protections are reused. The page contains no force test and does not need motor output.

Apply on binding/setup/overview/test pages saves the binding drafts and previewed device selection. Apply on Force Feedback saves only its settings; select another Controller page to apply a pending input-device/binding change. Discard Draft restores saved controller bindings, selected input device and FFB settings without discarding unrelated option edits. Leaving the options/menu without Apply preserves saved configurations. If bindings save but the separate device-preference file fails, the message explicitly reports that partial save; the selection remains pending.

## Implementation boundary

- `Idas3PauseMenu.Controller.cs` is part of the existing pause menu, not a second application or binding engine. The old Wheel category is no longer visible. Legacy programmatic selection of tab 4 redirects to Controller → Force Feedback.
- `Idas3ControlBindings` owns checkpoint/restore for guided setup, explicit conflict rejection and one `EvaluateDriving` implementation used by saved gameplay and draft preview. Existing normalization, pedal rest/direction, paired-stick radial context and held-input/reconnect guards remain in place. Saved versions 1–3 are still accepted.
- `Idas3ControllerDevices` supports a nonpersistent selection preview with Apply/Cancel. Discovery and single-active-device selection remain the same; no save schema change is introduced.
- `Idas3GameOptions.ApplyWheelSettings` follows the existing scoped HUD transaction pattern, so applying FFB does not apply unrelated audio/display drafts.
- `Idas3SceneGame` supplies the test sample immediately after the normal physical-input Poll. It blocks tested inputs from menu actions, other managed menus and the submitted game packet. The existing menu/pause lifecycle still stops FFB. Native steering/physics and managed/native force calculation code are unchanged.

## Validation performed locally

39 portable checks compile the actual binding and game-option classes with small platform/serialization adapters. They cover controller/keyboard conflict rejection without unrelated changes, explicit clearing/rebinding, unsaved preservation, Setup checkpoint restoration, gameplay/preview parity, draft-versus-saved evaluation, custom wheel and reversed pedal normalization, capture/reconnect guards, keyboard recovery, old-format loading without writes, FFB-only Apply and failed-apply rollback. The adapters are not a Unity serializer or native player; their limits are intentional.

Run with .NET SDK 8:

```text
dotnet run --project Tests/ControllerFoundation/ControllerFoundation.csproj --configuration Release
```

C# syntax parsing also checks the source tree. Existing Unity menu/device smoke expectations were updated for the single visible Controller category and explicit occupied-binding rejection. The new `Idas3ControllerFoundationChecks.Run` is an explicit Unity editor check; it is not called during normal startup. It uses an isolated temporary preference directory and a synthetic gamepad filtered from physical-device discovery, and performs no FFB output.

## Required Windows acceptance — not performed here

Use a full game checkout and the existing Windows toolchain with licensed Unity 6000.6.0f1 / Input System 1.19.0. This feature belongs to the game, so the isolated DeviceIdentityProbe build does **not** validate it.

1. Import/compile the game and run `-batchmode -quit -projectPath <checkout> -executeMethod Idas3ControllerFoundationChecks.Run -logFile <log>`. Inspect the exit status and `Verification/controller-foundation/unity-checks.txt`. Run the existing controller menu, controls, controller-device and FFB lifecycle checks with the project's established diagnostic entrypoints.
2. Build the full Windows game using its existing build workflow. Verify the five pages render and are usable with mouse, keyboard and supported controller navigation; inspect long device/binding names and small windows. No visual acceptance is claimed from syntax checks.
3. Back up an existing settings directory. Verify open/cancel/discard leave saved files unchanged; test device preview then Cancel, Apply and restart; verify keyboard mappings and old controller profiles persist. Verify a conflict changes no other action and an explicit clear/rebind affects only the requested slot.
4. Complete Setup, cancel midway with prior draft edits present, and review before saving. Verify optional shift skips preserve assignments. Test device disconnect/change during capture, timeout, Escape, held controls and keyboard recovery.
5. In Test Controls verify real evaluated assigned axes/buttons, including reversed pedals and rebound keyboard keys; confirm native submitted gameplay input remains neutral throughout the test, including online/attract contexts. Verify focus loss and held-input exit guards. This is not a drive or motor acceptance test.
6. Check all four FFB settings and output status, cancellation and persistence. Confirm the existing stop-in-menu/focus protections with the established automated/mock-backend checks. No new motor test is required or included in this milestone.

Windows compilation, Unity JsonUtility behavior, rendered UI, physical controller behavior and the full player are unverified in this environment. Do not mark the milestone hardware-accepted or fully validated until those results are reviewed. No later multi-device functionality or force tuning is implemented.
