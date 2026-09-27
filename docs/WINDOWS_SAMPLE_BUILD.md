> Owner update: baseline `73e87d617a467776803c9ff59d6754ff78fa829d` built and launched on the owner’s Windows PC; the owner reports native tests and ZIP integrity passed. The local Hub route worked independently of unavailable hosted activation. The unified Controls follow-up still needs its own Windows build and hardware retest. See [current handoff](UNIFIED_CONTROLS.md).

# Windows sample build and Actions handoff

## Current review update — 2026-09-27

[Published ad3a18f run](https://github.com/hyp36rmax/ID3-Re-Imagined-Multi-Input/actions/runs/36343935859): portable and Windows native passed. Both native DLLs built and all three native tests passed. This confirms the Native/tools checkout correction. Unity activation inputs were absent; Unity jobs skipped and readiness failed. Native DLL/report artifacts exist, but no complete player ZIP. This local navigation follow-up has not run in CI. See [navigation and build evidence](WHEEL_MENU_NAVIGATION.md).

## Historical status when the build workflow was introduced

No Actions run, Windows DLL build, Unity semantic build, player artifact or hardware acceptance has occurred for this change. The local host is an ARM Mac without Unity or an accessible configured Windows rig. A GitHub-hosted Windows route is prepared; local OS absence does not make that route unavailable. Repository secret/activation status has not been established. No purchase, new license arrangement, paid runner, release or repository setting change has been performed.

Starting HEAD and live working branch were both `d821b83f02e4f0832dc688fcb92e1ecde45402e2` with no unpublished commits. The handoff's report that this commit was unpublished was stale. Fork main/default remains `377e4ad9451a34ed4bc0acf735038372d8dec876`.

Upstream initially matched that main, then advanced during implementation to `80923197cb739fecc3e91da20992e19ebcc75882` (release .38). Read-only review found new `RigSamples`/`PollRig` generic-profile aggregation, per-control reconnect guards, capture diagnostics and source-lock changes. These overlap our provider/bindings/host. Nothing was merged. Our session-scoped, explicit per-action assignments, immutable snapshots and separate preferences remain the reviewed fork approach. Review reconciliation separately; neither unpublished future upstream work nor compatibility with .38 is assumed.

## Required environment

- Windows x64. Actions uses the existing GitHub-hosted `windows-2022` image, not a newly provisioned machine.
- Unity **6000.6.0f1**, changeset **f7f8ed4d1e24**, with Windows Standalone **Mono** support. Input System **1.19.0** is pinned by source. IL2CPP is not used. `Install-Unity.ps1` downloads the pinned official Windows editor installer, checks its Authenticode publisher/signature, installs it silently and checks standalone support. This path has not yet been executed on Windows.
- A valid activation route. Hosted jobs support an **already eligible paid serial/account** route after owner opt-in. Set repository variable `ID3_UNITY_CI=pro-serial` and secrets `UNITY_SERIAL`, `UNITY_EMAIL`, `UNITY_PASSWORD` in GitHub settings. Do not paste them into chat, source, logs or artifacts. Activation can still fail due to account/seat restrictions; readiness only checks presence. The jobs serialize activations and attempt license return in finally. If cancellation/runner failure prevents return, the owner must check the seat before retry.
- Unity Personal activation uses Unity Hub. If no eligible existing serial route is available, the shortest handoff is a Windows owner machine with Unity activated through Hub. A licensing-server or self-hosted route needs separate configuration review; none is silently introduced here.
- Visual Studio C++ x64 build tools, Windows SDK, CMake 3.24+, Python 3.11+ (CI pins 3.12), .NET 8 for portable checks. Existing native CMake builds both `Idas3Unity.dll` and `Idas3WheelFeedback.dll`; Windows system libraries remain unchanged. Toolchain/SDK output is retained in the native log.
- Complete game checkout: `Assets`, `Native/data`, `RuntimeAssets`, project settings, packages and the checked-in Steamworks package/runtime. Restore Unity packages on first import; no private package feed is assumed. Probe and portable/native jobs use smaller source checkouts. The current local sparse checkout does not supply all game assets.
- Use only a clean disposable checkout of the approved source. Scripts reject source edits rather than label them as the commit. Generated native DLL replacements are checked separately against the native report. Keep ROMs, captures, user settings, personal music, credentials and unrelated files out of build staging. The existing game packager rejects private data/ROMs and uses its original allowlist; no external asset downloads are added.

Official references: [pinned editor release/installers](https://unity.com/releases/editor/whats-new/6000.6.0f1), [Unity activation methods](https://docs.unity.com/en-us/engine/6000.0/manual/get-started/install-and-upgrade/licenses-and-activation/license-activation-methods), [editor command-line activation](https://docs.unity.com/en-us/engine/6000.0/manual/unity-editor/command-line-arguments/editor).

## Actions

`.github/workflows/multi-input-sample.yml` declares manual dispatch and path-filtered push/PR triggers for `ID3-Multi-device-input`. Read-only repository permissions; pinned action commits; 14-day portable/native and 7-day Unity artifact retention. No releases or writes to branches/settings. PRs run portable/native checks but never receive licensed builds. The Unity matrix expands into separate probe/game jobs, runs serially, and does not stop the probe just because the game native job failed. The game requires matching native DLLs and their exact SHA/hash report.

1. **Portable:** 34 diagnostic + 46 UAT, 57 Controller, 101 snapshot/provider and current multi-input checks; probe access audit, portable native JSON and packaging boundaries. The old diagnostic-only whole-repository diff check remains available by default; CI explicitly selects `--access-only` because later production work was authorized.
2. **Windows native:** actual MSVC DLL compilation plus steering smoothing, original FFB calculation and owner lifecycle unit checks. No GPU/ROM tests or device output.
3. **Unity probe:** existing isolated `Build-Probe.ps1`, actual JsonUtility round trips, native/managed identity agreement and a synthetic UAT baseline/restart/export using actual Unity serialization. Full probe player and reviewed docs are zipped. Rendered UAT UI and physical baseline/restart remain pending.
4. **Unity game:** actual import/compilation, snapshot/Controller/multi-input Unity synthetic checks, separate sample product, existing full-game staging and verified packaging. A standalone probe PASS cannot replace this job.
5. **Readiness/result:** explicitly reports READY-to-attempt, BLOCKED prerequisites, SKIPPED PR trust policy, FAILED jobs, or PASSED build/package checks. A blocked branch run fails overall readiness even if portable/native passed. A PR pass explicitly excludes Unity. Hardware and rendered UI remain NOT TESTED in build reports.

Artifact names include the exact Git commit; result JSON includes SHA, OS and version requirements, tests and limitations. Native logs include compiler/SDK evidence. Player ZIPs include executable/data/runtime dependencies, not merely the EXE. Game READ ME identifies SHA/version; probe self-test identifies SHA plus source digest. Useful failure logs are uploaded when generated. License activation/return logs and account state are never uploaded; explicit secrets are redacted from report files. No package is represented as a pass when its build or completeness checks fail.

**Publication is not yet authorized.** Once the local commits are reviewed and approved, a normal push to the working branch triggers validation. Inspect the exact commit's run, logs and complete ZIPs before a hardware claim. GitHub's manual-dispatch UI normally requires the workflow on the default branch; this change intentionally does not modify main. The branch push trigger provides the first run and that run can be manually rerun. Do not change main/default just to expose the dispatch button; review any bootstrap separately. [GitHub manual-run requirements](https://docs.github.com/en/actions/managing-workflow-runs-and-deployments/managing-workflow-runs/manually-running-a-workflow)

## Owner-machine alternative

Clone a full disposable checkout and check out the approved SHA (not a moving branch), verify it is clean, activate the required Unity version through the supported route, then from its root run PowerShell:

```powershell
$env:IDAS3_UNITY_EDITOR='C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor\Unity.exe'
.\Tools\CI\Build-Windows.ps1 -Kind game
.\Tools\CI\Build-Windows.ps1 -Kind probe
```

Run each from a fresh disposable checkout if Unity changed tracked settings during a prior attempt. The game script builds native code unless `-ReuseNative` was explicitly supplied with a matching successful report. Python must be on PATH. Keep generated `Verification/ci/native`, `Verification/ci/game` and `Verification/ci/probe` reports. Share the produced complete ZIP plus its result/checksum reports after reviewing logs. A failed import or compile is returned with its log and smallest proposed correction in a separate local review commit. Do not call a native-only build a playable sample.
