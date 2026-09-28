# FFB audit after the Controls hardware follow-up

## Delivered boundary

Input/capture corrections and Other Inputs are independent of force output.
Multi-input FFB remains disabled. No new left/right pulse controls, automatic
force selection or claimed DD2 compatibility are shipped in this follow-up.
Existing FFB settings/backend remain available for the original supported path.
The status text now distinguishes a queued/accepted request from physical output;
it no longer calls a mailbox submission "Feedback active".

## Existing backend and concrete missing contract

`Idas3WheelFeedback.IBackend` already separates the menu/owner from a driver.
The native DirectInput DLL enumerates attached force-feedback devices and records
their instance GUIDs. Enumeration queries capabilities without acquiring devices
or creating/starting effects. It requires an actuator and constant-force support
before output. Device open preserves/restores autocenter; foreground, disconnect,
invalid/stale telemetry, stop and shutdown guards already exist. Native constant
forces are finite 100 ms leases; the managed mailbox drops stale requests and
prioritizes Stop. The cabinet force calculations are unchanged.

The current managed owner is unsuitable for enabling the requested multi-input
path unchanged:

- Its `InputIdentity` comes from the active input controller. Switching input
  keys stops and changes ownership; `input.connected` is also an output gate.
  Pedals/shifters must not control the explicitly selected force endpoint.
- Its automatic fallback associates VID/PID. Even a unique match does not prove
  physical input/output identity. The multi-input path must require the selected
  DirectInput endpoint, independently of input matching and enumeration order.
- `IBackend.Send` on the queued implementation acknowledges enqueueing. It has
  no separate initialized-endpoint result or acknowledged command generation.
- Native `Idas3WheelSetForce` already supports bounded signed constant force,
  but a first zero command deliberately does not acquire the wheel. There is no
  exported output-free initialize/ready operation. Calling nonzero force merely
  to discover whether initialization succeeded would violate the requested UI.
- A test controller needs a monotonic pulse deadline, low amplitude cap, selected
  endpoint generation, inversion handling, immediate cancellation mailbox and
  confirmed driver status. Native 100 ms leases help, but do not by themselves
  establish that UI test contract or physical direction.

The smallest next implementation should adapt this backend: add explicit
initialize/ready/stop acknowledgement and a bounded test command to the small
interface, keep the original cabinet telemetry path unchanged, and route the
multi-input owner solely to the selected native endpoint. Re-detect may refresh
candidates without touching assignments; it must not initialize or output force.
A sole compatible candidate may be proposed, but multiple candidates require
selection. Keep saved preference, discovery, initialization, queued command,
driver acceptance, paused/disabled and observed physical output distinct.
No backend replacement, installation or licensing change was made.

## Evidence needed before enabling on the owner's DD2

This session cannot reach the Windows rig. The actual current mode, driver and
firmware, DirectInput candidate GUIDs, supported effects/actuator axes and
Unity/HID paths have not been captured for the owner's wiring. Return the mode
shown by the base/driver, driver versions, Devices log and force-device inventory
for (a) base-connected accessories and (b) independently connected USB devices.
Use the owner's actual supported wiring, not simultaneous alternative connections
invented by this guide. Capture cardinal/diagonal POV and rest reports with exact
mode and device paths. Names and VID/PID remain evidence with unresolved physical
identity, not enrollment. Re-detection will not restore session bindings.

After the initialization/acknowledgement extension is reviewed and compiled,
validate output-free discovery/init, explicit wheel selection among duplicates,
low-strength left/right direction and inversion, finite expiry, Stop, focus/page
close/disconnect cleanup. No physical pass follows from a successful API return.
This is pending work, not a completed force test.

## Alternatives and primary references

Retaining DirectInput is the smallest supported choice: Microsoft documents
separate acquisition and effect creation on
[IDirectInputDevice8](https://learn.microsoft.com/en-us/previous-versions/windows/desktop/ee417816(v=vs.85)).
The existing backend already uses that boundary and finite effects; a different
backend would still need the same explicit owner, acknowledgement and pulse tests.
No HYP36Rforce source/specification is present in this repository or supplied in
this handoff, so its capabilities were not evaluated or substituted.

Fanatec documents the DD2 base USB connection and peripheral/mode behavior in its
[DD2 manual](https://fanatec.com/media/pdf/8c/c0/0a/P-WB-DD2-Manual-EN_10.pdf).
Its [DD2 support page](https://www.fanatec.com/eu/en/explorer/products/racing-wheels-wheel-bases/podium-wheel-base-dd2-qr2/)
describes an operation-mode display and PC compatibility mode. These sources
explain why mode and connection topology must be recorded; they do not establish
how this owner's current Windows configuration enumerates. No mode or firmware
change is instructed by this follow-up.

## Validation

`Tests/FeedbackOwner` compiles the actual managed owner and its existing fake-driver
checks against minimal portable value adapters. It adds a status assertion and
runs 671 checks: telemetry validity, explicit/missing/ambiguous selection, stale
simulation, disabled/menu/disconnect stops, cleanup, queued latest-only requests,
stale-request expiry, Stop precedence and shutdown. No native DLL is loaded or
motor initialized. This is not Windows DirectInput or hardware evidence. The
portable CI runner now includes this suite. New bounded test-pulse checks are
pending because that interface has not been implemented or enabled.
