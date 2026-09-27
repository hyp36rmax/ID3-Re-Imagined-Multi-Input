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

## 2026-09-27 — UAT publication verified; Windows readiness still blocked

The publication check found `112711edfc775464b87b79d36db781931c67e177` already on the live `origin/ID3-Multi-device-input` branch. Local HEAD and the tracking ref matched, and the working tree was clean. Its parent is the previously published probe commit. No additional push or history rewrite was needed. This differs from the earlier report that the UAT commit was local-only; the current live remote is the evidence for its publication. Main and the default branch remain unchanged at `377e4ad9451a34ed4bc0acf735038372d8dec876`.

We checked readiness again. The accessible host is still macOS 26.6.2 on ARM64. Configured project inventory returned no remote projects, no SSH host configuration was present, and the inspected applications/commands provided no Windows VM environment. The repository still has no GitHub Actions workflow. No new evidence establishes an accessible Windows build machine or rig. Windows Unity installation/license status remains unknown, not a diagnosed license failure.

The missing prerequisite is access to a Windows x64 environment with Git/PowerShell, MSVC C++ x64 tools and Windows SDK, CMake 3.24+, and licensed Unity 6000.6.0f1 with Windows Mono support. The isolated project resolves Input System 1.19.0. The shortest next step is to use an existing configured owner machine at the published UAT SHA and run `Diagnostics/DeviceIdentityProbe/Build-Probe.ps1`, retaining its console transcript and Unity build log.

No Windows native/player compilation, serialization self-test, packaged-documentation verification, UAT UI/folder/restart/export check, CI run, player artifact or hardware result was produced. The earlier 46 UAT and 34 identity checks remain portable local results; they were not rerun for this documentation-only update. After a successful Windows build, verify the UAT controls and complete package, then perform only baseline/restart if the rig is accessible. Return evidence before expanding the campaign.

The agreed later player-facing direction remains one Controller menu containing Quick Setup, Overview, Bind Controls, Test Controls and Force Feedback, preserving existing configurations and keeping normal setup simple. This publication/readiness task implements none of that menu or any production input/FFB work. This outcome record is a separate local commit for review and is not included in the already published UAT commit.

## 2026-09-27 — Functional Controller menu baseline (local review)

The scope was expanded from a future menu direction to five working pages using existing single-controller capabilities. We inspected the pause menu, binding capture/evaluation, device preferences and FFB option/lifecycle paths. Two existing behaviors needed adjustment: selecting an input device immediately saved it, and capturing an occupied controller control automatically swapped an unrelated action. Selection is now a menu draft, and conflicts are rejected with the affected action named so clearing is explicit.

Quick Setup guides steering/pedals and optional shifting, retains a checkpoint for Cancel, and shows assignments before Apply. Overview reads saved/draft mappings and real device status. Bind Controls reuses capture and clear. Test Controls calls the same driving binding evaluator on the normal physical sample, before menu packet neutralization; it shows binding demand rather than claiming post-physics steering response. Tested inputs cannot operate gameplay or menus, and a held-input exit guard keeps them from becoming a fresh confirmation. Force Feedback exposes existing settings through a scoped apply that preserves unrelated drafts. Native force calculations and lifecycle guards are unchanged.

39 portable checks pass against the actual binding/game-option classes with platform adapters. They cover conflicts, cancellation, saved-format compatibility, mapped preview parity, reversed pedals, reconnect/keyboard recovery and FFB-only save/rollback. An adapter initially lacked JsonUtility.FromJsonOverwrite; the test adapter was completed without changing production serialization. C# syntax checks passed. Explicit Unity flow checks were added and existing menu/device smoke expectations adjusted, but Unity/player compilation, UI rendering, Windows persistence and physical hardware checks have not run here. This is an implemented local baseline awaiting Windows acceptance, not a claim that the original input/FFB reports have been fixed.

The change stays on ID3-Multi-device-input in a separate local commit after the earlier unpushed log-only commit. Next is code review, publication approval, a full-game Windows build and the documented Controller menu acceptance checks. The isolated identity-probe build cannot validate these production menu files. Simultaneous multi-device input, production physical-identity resolution and FFB tuning remain pending.

## 2026-09-27 — Controller usability follow-up (separate local review)

The five Controller pages now share Save Changes for bindings, selected input device and existing FFB settings. Unrelated option drafts stay pending. Saving stops at the first failure, reports earlier successful writes and retains the remaining edits for retry or explicit Discard Changes. An incomplete save prevents accidental exit. Setup's checkpoint retires when bindings reach disk, so a later failure cannot restore an older draft over saved bindings. This uses the existing persistence services, with no format or force-calculation change.

Quick Setup presents Steering as one stage with sequential left/right captures and grouped review. Test Controls labels its values as evaluated assigned input before native response processing. Full driver names remain unchanged and are scrollable/wrapped in the menu. Fanatec model, accessories, mode and compatibility require owner-rig evidence; those cases remain pending.

50 portable checks passed, including combined save, failures at each step, retry, cancellation, Setup checkpoint restoration and unrelated-option preservation. The device writer is simulated in the portable coordinator checks; Unity integration checks were extended for all five save actions and the partial-save exit/discard flow but could not run here. C# syntax parsing passed for 107 files. Unity semantic compilation, Windows player build, actual JsonUtility, rendered layout and hardware acceptance remain unperformed. There is no new CI run or player package. Reports of digital-feeling input, limited multi-USB support and excessive FFB concern the original game; this follow-up does not establish fixes for those reports.

The reviewed implementation remains intact. This follow-up is a separate local commit with no push. Next is review, then an approved full-game build on a configured Windows machine with licensed Unity 6000.6.0f1 and the documented acceptance checks. Multi-device aggregation, physical identity resolution and force tuning remain pending.

## Future evidence entry format

Append a dated entry with milestone/build, purpose, setup, procedure/run ID, capture verdict, identity verdict, expected versus actual behavior, evidence package location, findings, limitations and the next decision. Retain failed or inconclusive attempts. Never infer a hardware success from a synthetic test or a capture-only PASS.
