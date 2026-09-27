# Initial D diagnostic UAT — baseline and restart

This is an evidence-collection workflow for the isolated identity probe. A successful capture is not a successful physical-device association, and neither validates production multi-input or force feedback. No driving or force output is requested.

## Start and record

1. Build the probe using the diagnostic README. Confirm the build/commit and purpose. On Windows, launch `Id3IdentityProbe.exe --campaign rig01`. Without `--uat-run`, each launch creates a new UAT run; the UI shows its folder, run ID, state and restart arguments. Only milestone M1-A-P1 is implemented.
2. Enter hardware models/available controls, Windows and driver versions, and relevant settings in the setup field. Do not enter serial numbers, account names or private paths. Check the review-for-sharing box and save setup. Every change to these text fields clears that review checkbox.
3. Select **1. Baseline**. Enter a private physical-control label, move only that control for at least three seconds, then select **Record movement observation / capture now**. Wait at least four seconds. Repeat for available wheel/pedals/shifter controls and resting state. Native inventory and numeric input observation run automatically. Missing hardware is NOT TESTED.
4. Record expected-versus-actual behavior using the displayed expectation and the actual/observation fields. Select separate capture and identity verdicts, review the readable notes, and **Save step result**. Every save appends an attempt; earlier attempts remain. Describe which limited identity proposition you tested; matching tokens alone do not prove a physical unit. Default identity is UNRESOLVED.
5. Choose **Save for restart / pause run**, optionally export partial evidence, and copy the displayed arguments. Close the application. Relaunch the **same build** with `--campaign rig01 --uat-run <displayed-run-id>`. Reuse the existing key. Select **2. Application restart** (selected automatically on resume). Repeat observations and compare identifier-field tokens between sessions. Save the restart result. A capture PASS requires a prior baseline in a different process session.
6. Choose **Finish run**, then **Export shareable evidence ZIP**. **Open results folder** opens the current run. The export path is displayed. If stopping early, **Cancel run** preserves evidence and marks cancellation; export remains available. Return the ZIP for review and add findings/next decision to the developer history.

Expected baseline: available controls and metadata appear, errors are explicit, native/managed build identities agree. Expected restart: a new session with the same campaign/build and equal tokens for unchanged identifier strings; numeric Unity IDs and slots remain session-local. PASS capture is rejected without reviewed setup and a latest matching-build inventory free of collection-level errors. Field-level missing/error evidence is still useful and can leave identity UNRESOLVED.

PASS means the specified expectation was observed. FAIL means the expectation was violated. UNRESOLVED means evidence is insufficient or ambiguous. NOT TESTED means not performed or unavailable. Both steps must have a result before Finish; NOT TESTED is valid. The run's final verdict uses the latest attempt per step, with FAIL taking precedence, then UNRESOLVED, then NOT TESTED; PASS requires both latest results to be PASS. Capture and identity aggregate independently. Tester identity PASS is a human assertion scoped by the notes, not an automatic resolver decision.

## Storage and compatibility

```text
%LOCALAPPDATA%\ID3IdentityProbe\
  UAT\M1-A-P1\<UTC-time>-<random-GUID>\
    manifest.json                  current manifest, including reviewed setup
    plan.txt                       numbered procedure and expectations
    results.json                   all step attempts, expected/actual and two verdicts
    observations.json              same complete attempt records, including readable notes
    summary.txt                    state, verdicts, session count, concise results
    history\0000000001.json ...    immutable manifest revisions
    sessions\<process-session>\capture.jsonl
  UATState\M1-A-P1\<run-id>.lock   exclusive writer lock, never exported
  UATExports\<run-id>-<GUID>.zip
  Campaigns\rig01\
    campaign-key.secret            existing persistent key; never exported
    private\<session>\capture.raw.jsonl
    share\<session>\capture.jsonl, summary.txt, steps.txt
```

Manifest fields include schema version, source build/commit, milestone/run ID, campaign token, purpose, UTC start/update/end, state, per-session start/end/state/counts, setup notes and all results. Existing campaign behavior remains compatible: new process sessions still write original campaign logs, and sanitized records are also written into the UAT run. Existing campaigns are not moved, deleted or imported. Explicitly select an existing campaign name to reuse its key. Attach new sessions with the exact run ID; a changed campaign key or build is rejected. Older captures remain where they were and are not silently added to a new UAT run. The run links them conceptually by campaign token and new session IDs, without publishing private filesystem paths.

Run names use UTC milliseconds plus a random GUID and collision checks. Logs use create-new mode. One writer per run is enforced by an OS file lock; competing starts fail rather than overwriting evidence. Finish/cancel states cannot be reopened. Start a new run to retry a terminal run. Use the same campaign for cross-run equality where desired.

## Interruption and recovery

Run state remains `incomplete` until explicit Finish or Cancel. Closing normally marks only the process session closed; it does not complete or pass the run. Save for restart stops all current probe capture, retains the incomplete run and releases the writer lock when the app closes. Restart in the same still-open process is deliberately not allowed.

Records are flushed to disk per line. Immutable versioned manifest snapshots are written before atomic replacement of the convenience manifest/results/summary. On resume, the newest parseable manifest snapshot is loaded; stale active sessions are marked interrupted. A malformed latest snapshot is skipped without deleting it. The existing captures are retained, including any incomplete final line; a reviewer should disregard a truncated final JSONL line. The run stays incomplete and cannot appear passed until explicitly finished after review. Storage/device failure can still lose the last operation; no guarantee is made against disk failure or power-loss hardware behavior.

After a crash, relaunch with the original campaign and run ID, inspect old interrupted sessions and save/retest results as appropriate. For a recovery-only export, attach, immediately Save for restart, and export; the additional session is explicit. Do not repair data by deleting earlier evidence. To open an existing run for export without adding a session, launch the same build with `--campaign rig01 --uat-export <run-id>`. This works for partial, completed and cancelled runs; it takes the writer lock but does not alter the run or collect input. No resumption/edit path is provided for completed or cancelled runs.

## Shareable export boundary

Export is available only after capture stops. Each new ZIP has a unique name. Its explicit allowlist contains a regenerated manifest, plan, results, observations, summary, and recorded UAT session JSONL files. No recursive directory export is used. It excludes campaign keys, private raw logs, legacy campaign files, arbitrary files dropped into a run, raw Unity/build logs, lock files and temporary/history files. Partial/cancelled status is retained in the package. Keep the ZIP intact when returning evidence.

Device identifiers retain the existing campaign HMAC pseudonyms. Private physical-control labels stay tokenized in shareable records. The separate reviewed actual/observation/setup fields are deliberately readable and exported verbatim; the checkbox is a human review gate, not an automated personal-data detector. Use generic labels such as wheel-A and pedal-B. Diagnostic API/managed error types/codes remain in JSONL. If a build/runtime log has additional useful information, enter a reviewed sanitized summary in the actual/observation field; unreviewed log attachments are never auto-imported.

## Pending and validation boundary

Same/different-port reconnect, reversed order, unrelated controller, identical/duplicate-serial devices, XInput slots/mirrors and multiple-FFB/composite collections remain documented in the original README test matrix but are pending review, not selectable in this UAT runner. This work does not add production telemetry, binding resolution, multi-input aggregation, gameplay or FFB changes.

Portable tests exercise filesystem/session behavior and exports using synthetic records, including abrupt child-process exit and archive extraction. Existing identity tests still run. Actual Windows filesystem locking/recovery, Unity import/JsonUtility, UI/folder opening, DLL/player compilation, packaging in a built Mono player, and baseline/restart hardware captures require Windows validation and are not claimed here.

## OutRun conventions inspected and adapted

Read-only reference: OutRun `upstream` checkout at `2a017f61689d3c888d8018fd7e6a764308cecade`. Its existing local ahead-one state was preserved.

| Inspected source | Convention carried over | Initial D adaptation |
|---|---|---|
| `docs/TELEMETRY.md`, `docs/telemetry_probe.md`, `docs/TELEMETRY_TEST_PROTOCOL.md` | Exact build/scenario metadata, controlled observations, visible capture status, flush on stop | Standalone identity purpose; no game hooks, force parameters or driving; JSONL with immediate flush |
| `docs/HYP36R_FORCE_RESEARCH_II_R2C_UAT.md`, `src/research_scenario_runner.hpp` | Short numbered scenarios, attempt/review state, retained cancellation/retry evidence | Only baseline/restart; separate capture and identity verdicts; no timed driving countdown |
| `src/overlay/debug_ui.cpp`, `src/telemetry_probe.cpp` | In-app collection controls, current filename/status, start/stop, append review outcome and session metadata | Existing Unity probe UI; run/session manifest plus append-preserved result attempts |
| `src/research_capture_path.hpp` and unique capture-path helper in `src/telemetry_probe.cpp` | Dedicated scenario folders, safe path components, collision avoidance | Fixed milestone, UTC/GUID run IDs, exclusive writer lock; LocalAppData root separate from game/source/OutRun |
| `docs/DEVELOPMENT_HISTORY.md`, `research/README.md` | Retain failed interpretations, distinguish software observations from hardware, explicit evidence handoff | Diagnostic developer history and allowlisted shareable ZIP; archive extraction tested |

The relevant collector is the existing in-app telemetry/session code; no standalone collection/ZIP tool was found in the inspected OutRun tree. The new privacy-reviewed ZIP packaging is an Initial D adaptation, not a claim that OutRun already had this exporter. No OutRun file was modified.
