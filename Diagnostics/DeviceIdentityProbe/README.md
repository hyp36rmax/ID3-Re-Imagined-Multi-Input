# M1-A-P1 — Windows device identity probe

This standalone diagnostic records metadata and input observations. It never selects a gameplay device or force target. All Unity-to-Windows pairings remain hypotheses. No Windows build or hardware result is implied by the presence of this source.

## Build and run on Windows x64

Required: Git, PowerShell, CMake 3.24+, Visual Studio C++ x64 build tools with Windows SDK, and licensed Unity **6000.6.0f1** with Windows Mono build support. Unity resolves Input System **1.19.0** from the isolated manifest; package restoration may need network access. Close any editor using the generated diagnostic project before rebuilding. No administrator access is required by the probe.

From the repository root:

```powershell
.\Diagnostics\DeviceIdentityProbe\Build-Probe.ps1
# For a nondefault installation:
.\Diagnostics\DeviceIdentityProbe\Build-Probe.ps1 -UnityEditor 'D:\Unity\6000.6.0f1\Editor\Unity.exe'
.\Diagnostics\DeviceIdentityProbe\build\player\Id3IdentityProbe.exe --campaign rig01
```

The script reuses the repository's vswhere/vcvarsall/NMake approach. It builds only the metadata DLL and a disposable copy of this isolated Unity project under `build/projects/<source-digest>`. It does not import the game project. The build creates a scene containing the diagnostic component and camera, enforces the exact engine/package versions, and runs a Unity JsonUtility round-trip check. Generated projects, binaries, and caches are ignored. Inspect `build/unity-build.log` on failure. Distribute the entire `build/player` directory, not just the executable.

Build identity is Git HEAD plus SHA-256 of the diagnostic native source, Unity assets/settings/package manifest, and build script. Both native and managed captures include this identity; mismatch is an explicit collection error. It fingerprints source, not toolchain binaries or the installed driver stack.

## Captures and privacy

Output is separate from game saves:

```text
%LOCALAPPDATA%\ID3IdentityProbe\Campaigns\rig01\
  campaign-key.secret
  share\<session>\capture.jsonl
  share\<session>\summary.txt
  share\<session>\steps.txt              (after recording an observation)
  private\<session>\capture.raw.jsonl
```

Share only the `share` directory. The private capture retains full descriptions, identifiers, capabilities, control paths and tester labels. The key and private logs are ordinary local files protected by the Windows account's filesystem permissions, not encrypted storage. Keep the campaign key; do not publish it. Use a fresh campaign name for unrelated investigations. To compare captures on another machine, securely transfer the same campaign key before its first run; the name alone does not reproduce tokens.

A random 32-byte campaign key drives domain-separated HMAC-SHA256 pseudonyms. The same serial or Windows path has the same field token across launches/reconnects in a campaign. Windows paths, PnP IDs and container GUIDs are case-normalized; serials retain exact case. Instance and parent fields use the same domain so parent relationships remain comparable. Endpoint IDs include the backend prefix: compare `path` field tokens to identify cross-backend aliases. Unity IDs, XInput slots, and fallback Raw Input handles are additionally session-scoped in endpoint tokens. Raw numeric Unity IDs/slot numbers remain in the record for diagnosis but are meaningful only together with its session.

Names, layouts, control paths, arbitrary capabilities and free-text observation labels are tokenized. Numeric metadata and the explicitly typed numeric HID descriptor remain readable. Unknown fields are tokenized by default. Equality is preserved, not anonymity against someone holding the campaign key. A stable token establishes equality of the reported string, not persistent physical identity.

## Record format and collected evidence

`capture.jsonl` is UTF-8, one schema-version-1 record per line. Records carry probe/build/native-build identity, engine/package versions, campaign/session IDs, UTC completion and inventory-start timestamps, sequence, inventory generation, step, observation token, tester verdict, endpoints, candidates, and collection errors. Kinds: session-start/end, inventory, input-sample, device-change, tester-observation and collection-fatal. Runtime records use `evidenceClass=runtime-observed`; synthetic checks live only in `Tests` and never enter a campaign.

Endpoints carry backend, connected state, runtime ID or slot where applicable, observed connection generation, fields and controls. Each field has `name`, `value`, `status`, `source` and `error`. Missing, unsupported, unknown, disconnected and error states are explicit where queried. API errors carry the API name and numeric error code (Win32, CONFIGRET, HRESULT or NTSTATUS according to the API). A missing optional serial is not silently promoted to identity evidence.

| Source | Evidence | Limits |
|---|---|---|
| Unity public Input System | Device ID, description, serial as reported, capabilities, layout, control paths, numeric game-control samples, device-change events | Public description does not expose Windows interface/PnP/container identity; these fields are unsupported. No runtime-ID mapping is invented. |
| SetupAPI / Configuration Manager | Present HID interface paths, PnP instance, immediate parent ID, container GUID, location paths | Location is topology evidence; container provenance is unknown. Does not recursively enumerate every ancestor or all non-HID device classes. |
| HID metadata queries | Serial from HidD_GetSerialNumberString, VID/PID, usage and report sizes | Driver may deny metadata access or omit/duplicate serials. Handles request zero desired access and shared read/write. |
| Raw Input list/info | HID endpoint names/paths, VID/PID, usage | Inventory only; no WM_INPUT registration or replacement of Unity registration. |
| DirectInput8 | Instance/product GUIDs, name/type, GUID-and-path property, VID/PID, axes/buttons/POVs, capability flags including FFB | Property may be unsupported. FFB capability is not proof of an operational actuator. No acquisition or effects. |
| XInput read APIs | Slots 0–3, state/packet, buttons/axes, type/subtype/flags | Slot is session/location-like evidence, not physical identity. No serial or automatic Unity mirror mapping. |

Native inventories run on one background worker about every two seconds after completion. Unity state is sampled when the native result completes, so the combined snapshot is not atomic; start/end timestamps expose that interval. Unity game axes/buttons are observed at 10 Hz with a 0.01 change threshold. Keyboard/pointer values are not recorded. Brief presses and fast native disconnect/reconnect cycles may be missed. Connection generations count observed disappearances/reappearances per session, not guaranteed physical plug cycles. Keep devices disconnected for at least four seconds and hold test controls for three seconds. Logs grow while running; close the probe after each stage.

## Candidate rules (diagnostic only)

- Unity/HID pairs: compare serial, VID/PID and usage. Contradictions reject a pair. Missing unit evidence, duplicate serials/collections and incomplete inventory remain unresolved. Even a unique equal reported serial is an unresolved physical hypothesis awaiting hardware evidence.
- DirectInput/HID and Raw Input/HID pairs: an exact case-insensitive Windows path can establish `endpoint-alias-observed` only when unique on both sides, relevant paths are present, enumeration succeeded, and VID/PID do not conflict. This proves a current endpoint alias, not portable physical identity.
- Shared containers remain unresolved endpoint groups and never establish a unique actuator. Names, model IDs, enumeration order, location and activity never establish physical identity by themselves.
- XInput/Unity pairs are possible mirrors with no slot-to-Unity mapping. Tester labels and movement are observations only. No candidate chooses, persists, enrolls or routes a device.

## Windows procedure and criteria, defined before testing

Use one campaign throughout. For each stage select its step in the player, enter a short physical-control label (for example `wheel-A steering`, `pedals-A brake`, `shifter-A third`), move only that control for at least three seconds, then click **Record observation / capture now**. Wait at least four seconds for inventory completion. Repeat per control. Labels remain plaintext only in private logs. Keep a private hardware/port ledger so similar units can be distinguished by the tester. Do not infer a binding from correlated activity.

Verdicts describe the stage's evidence: **pass** means the specified capture behavior was observed; it does not certify physical identity. **unresolved** means missing/ambiguous evidence prevents the proposed association. **fail** means a probe invariant or expected capture behavior failed. **untested** means hardware or the test environment was unavailable. Record a verdict and explanation with the observation button. Collection errors can make an association unresolved even when logging itself passes.

| Stage / selector | Exact action | Expected evidence and stage-specific criteria |
|---|---|---|
| 1 `baseline` | Connect wheel, separate pedals and shifter that are available; launch and label/move each independently. Capture resting state too. | Unity controls and available native endpoints, field provenance and explicit errors. Pass capture when expected available devices/changes appear; missing requested evidence is unresolved. Missing hardware is untested, not fabricated. |
| 2 `restart` | Close normally, relaunch with the same `--campaign rig01`, select restart, repeat the three observations. | New session; equal unchanged identifier field tokens; runtime-ID endpoint tokens are session-scoped. Token instability for identical reported strings under the same key is fail. Changed physical/driver strings are unresolved evidence, not a token failure. |
| 3 `same-port` | Select stage, record before; unplug one device for at least four seconds, record absence, reconnect to the same port, wait four seconds and repeat its labeled movement. | Device-change events where Unity emits them, disappearance/reappearance, connection generation where the same endpoint returns. Expected same-string tokens agree; absent/changed serial remains unresolved. |
| 4 `different-port` | Record before, unplug for four seconds and reconnect that unit to a different port; record the new port in the private label/ledger and repeat movement. | Compare serial separately from paths/PnP/location/container. Topology may change. Pass collection of both states; no physical reassociation solely from model or location. |
| 5 `reversed-order` | Record, disconnect the relevant devices for four seconds, reconnect them in reverse order with four-second gaps and repeat each movement. | Pair evidence independent of enumeration order. Any association justified only by list order is fail; identical candidates remain unresolved. |
| 6 `unrelated-controller` | Capture before, add an unrelated controller, wait, exercise it then the original controls, capture after removal. | Additional endpoints do not silently change existing identity claims. Missing unit evidence stays unresolved even if there was previously only one model. |
| 7 `identical-devices` | If available, connect two identical units or units reporting duplicate serials; label A/B physically, move independently, then swap order/ports and capture again. | Duplicate serials and indistinguishable candidates remain unresolved. An automatic unique physical assignment is fail. Otherwise mark untested. |
| 8 `xinput-slots` | If available, capture Unity/XInput devices; disconnect/reconnect controllers in a different order, recording which slots actually change and separate labeled movements. | Slots and possible Unity/native mirrors are visible, no asserted automatic mapping. Slot reuse does not preserve physical identity. If slots cannot be changed, that subcase is untested. |
| 9 `multiple-ffb` | If available, connect multiple FFB-capable devices or a composite device; capture all collections and label each input separately. Do not request force output. | DirectInput capabilities/path aliases and container groups are visible where supported. Shared container never selects an actuator. Missing hardware/subcase is untested. |

General fail conditions: any probe-created force/motor output or property mutation; raw device identifiers/free-text labels leaked into share output; missing error reporting on a failed query; contradictory candidates accepted as aliases; an ambiguous Unity/physical or container/actuator pairing treated as resolved; corrupted JSON; silent logging failure. Record driver/OS versions privately with the campaign. If an unexpected motor action occurs, stop the probe and investigate; this probe has no force-output API path.

Review `summary.txt` (latest completed inventory), `steps.txt` (tester verdicts), and all JSONL records across sessions. Pair candidate endpoint tokens with their endpoint records. Review private paths only locally when investigating field changes. Do not publish `private`, the campaign key, or Unity logs without separately inspecting their contents.

## Local checks and remaining validation

Portable tests require .NET SDK 8; they compile the actual shared C# model and use System.Text.Json for fixture round trips. They do not substitute for Unity JsonUtility or a Windows build.

```powershell
dotnet run --project .\Diagnostics\DeviceIdentityProbe\Tests\ProbeTests.csproj --configuration Release
python .\Diagnostics\DeviceIdentityProbe\Tests\audit.py
```

The C++ `Tests/json_test.cpp` exercises the native JSON helper without Windows. Compile with a C++17 compiler and parse its stdout as JSON. Synthetic tests cover missing/contradictory fields, duplicate candidates, incomplete inventories, endpoint-only aliases, generation tracking, permutation invariance and stable campaign/session pseudonyms. Static audit checks source scope and forbidden device-output calls; it is not a runtime driver guarantee.

At authoring: 34 portable synthetic checks pass; portable C++ JSON output and static audit pass. Windows C++ compilation, PowerShell execution, Unity project import/player build and JsonUtility self-test, real driver behavior and all nine hardware stages remain unperformed. The next gate is a Windows build and baseline capture, followed by owner review of the resulting evidence. Production identity resolution, enrollment/binding migration, input aggregation and steering-linked FFB routing remain outside this milestone.
