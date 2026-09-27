# Development history — Initial D multi-input and FFB investigation

This is the persistent engineering record for this branch. Like the OutRun development history, it records what changed our decisions, including failures and unknowns. Milestone reports hold the detailed evidence. Do not rewrite an unperformed check as a pass when summarizing later work.

## Original-game reports and the input boundary

Players reported digital-feeling input, limited support for separate USB controls and excessive force feedback in the original game. Those are attributed reports, not measurements made by this diagnostic. The probe does not fix or tune them.

Source inspection confirmed a narrower architectural fact: `Assets/Scripts/Idas3ControllerDevices.cs` discovers multiple candidates but exposes controls, active profile and wheel identity through one `active` device. Discovery is not the same as simultaneous wheel/pedals/shifter aggregation. The later input design must preserve existing behavior while addressing that boundary explicitly.

## Branch and identity investigation

`ID3-Multi-device-input` was established at `377e4ad9451a34ed4bc0acf735038372d8dec876`, with main reserved for original-project syncs. The M1-A investigation found that Unity runtime device IDs do not establish a public Windows endpoint mapping. Model names, VID/PID, list order and activity cannot prove persistent unit identity. Duplicate or missing serials and composite/container ambiguity must remain unresolved. This stopped us from introducing a resolver based on assumptions.

## Read-only probe and publication

Commit `6bdfb9240ed0d751ac8d7999995d43555f1fcd80` added an isolated Unity 6000.6.0f1 / Input System 1.19.0 probe with Windows metadata inventory. No exclusive acquisition, force effects, property mutation or production routing was added. Campaign-stable pseudonyms preserve equality across sessions while private raw evidence stays separate.

The 34 portable synthetic checks, C++ JSON checks and static device-access audit passed locally. The tests caught an order-dependent container-pair presentation issue, which was corrected. Those checks did not compile the Windows DLL or run Unity. Publication was authorized separately; Git command-line authentication was unavailable, so the existing authenticated GitHub Desktop app pushed the exact reviewed commit. The live branch SHA was verified afterward, and main/default branch remained unchanged.

Windows build validation was blocked by the absence of an accessible configured Windows machine. No Windows compile, Unity JsonUtility self-test, player artifact or physical baseline/restart result has been observed in this task. We did not add a new CI or licensing arrangement to work around that gap.

## 2026-09-27 — Dedicated UAT collection

The next change adapts the inspected OutRun controlled-scenario workflow: build/purpose identification, setup notes, short procedure, automatic collection, reviewable results, retained retries/cancellations and a developer decision record. OutRun's scenario folders, unique CSV/session naming, visible start/stop status and honest separation of software telemetry from hardware behavior carry over as process conventions. Its driving scenarios, force settings, game hooks and beside-game storage do not carry over.

Initial D keeps the existing campaign root and adds `UAT/M1-A-P1/<unique-run-id>`. One run can hold baseline and application-restart process sessions using the same campaign key. Capture and identity verdicts are separate. Reviewed notes are readable in the exported package; raw identifiers and unreviewed logs are excluded. Versioned manifests and flushed JSONL preserve useful partial evidence, and a recovered session is explicitly interrupted. A completed capture is not a production multi-input or FFB success.

46 portable UAT checks pass, covering collisions, campaign/build checks, writer locking, per-step results, retained attempts, interruption recovery, cancellation and export/restore privacy. An early review tightened PASS gating to the latest native/managed identity-matching inventory, rather than any earlier successful inventory. Export-only reopening was added so an owner can package a finished run without recording a new session. The same 34 identity checks continue to pass. Windows UI, locking, filesystem recovery, Unity build/serialization and hardware remain unperformed.

Next decision: review this bounded local change, then authorize its publication and run the isolated Windows build. If that succeeds, collect only baseline and restart evidence first. Review those results before reconnect/duplicate-device/XInput/multiple-FFB testing or any production identity, bindings, aggregation, UI or force-feedback work.

## Future evidence entry format

Append a dated entry with milestone/build, purpose, setup, procedure/run ID, capture verdict, identity verdict, expected versus actual behavior, evidence package location, findings, limitations and the next decision. Retain failed or inconclusive attempts. Never infer a hardware success from a synthetic test or a capture-only PASS.
