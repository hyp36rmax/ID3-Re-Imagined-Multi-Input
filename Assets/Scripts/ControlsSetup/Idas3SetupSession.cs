using System;

// Capture is still owned by the game's binding service. This class only owns
// wizard checkpoints, confirmation, and its six-second interaction window.
internal sealed class Idas3SetupSession
{
    internal enum Stage
    {
        Choose,
        Capturing,
        ReviewInput,
        TimedOut,
        Review,
        Closed
    }

    private static readonly Idas3ControlBindings.ActionId[] Actions =
    {
        Idas3ControlBindings.ActionId.SteerRight,
        Idas3ControlBindings.ActionId.Accelerate,
        Idas3ControlBindings.ActionId.Brake,
        Idas3ControlBindings.ActionId.ShiftUp,
        Idas3ControlBindings.ActionId.ShiftDown
    };
    private readonly Idas3ControlBindings bindings;
    private Idas3ControlBindings.DraftCheckpoint checkpoint, stepCheckpoint;
    private int revision;
    internal Stage State { get; private set; } = Stage.Closed;
    internal int Step { get; private set; }
    internal bool Gamepad { get; private set; }
    internal double Deadline { get; private set; }
    internal string Error { get; private set; }
    internal bool Open => State != Stage.Closed;
    internal bool Complete => State == Stage.Review;
    internal string Instruction => Step == 0 ? (Gamepad ? "Move the steering stick right" : "Center the wheel, then turn it right") : Step == 1 ? "Press the gas control" : Step == 2 ? "Press the brake control" : Step == 3 ? "Press Shift Up" : "Press Shift Down";

    internal Idas3SetupSession(Idas3ControlBindings bindings)
    {
        this.bindings = bindings;
    }

    internal void OpenChoice()
    {
        State = Stage.Choose;
    }

    internal void Begin(bool gamepad, double now)
    {
        Gamepad = gamepad;
        checkpoint = bindings.BeginWheelSetup();
        Step = 0;
        StartCapture(now);
    }

    private void StartCapture(double now)
    {
        stepCheckpoint = bindings.SaveDraftCheckpoint();
        if (Step == 0)
        {
            bindings.ClearDraft(Idas3ControlBindings.ActionId.SteerLeft, Idas3ControlBindings.Slot.Controller);
            bindings.ClearDraft(Idas3ControlBindings.ActionId.SteerRight, Idas3ControlBindings.Slot.Controller);
        }

        revision = bindings.CaptureRevision;
        Error = null;
        Deadline = now + 6;
        bindings.BeginCapture(Actions[Step], Idas3ControlBindings.Slot.Controller, now);
        bindings.LimitCapture(Deadline);
        State = Stage.Capturing;
    }

    internal void Tick(double now)
    {
        if (State != Stage.Capturing)
            return;
        if (bindings.CaptureRevision != revision)
        {
            bindings.CancelCapture();
            if (Step == 0 && !bindings.DeriveSetupSteering())
            {
                Error = bindings.LastError;
                bindings.RestoreDraftCheckpoint(stepCheckpoint);
                State = Stage.TimedOut;
            }
            else
                State = Stage.ReviewInput;
        }
        else if (now >= Deadline || !bindings.IsCapturing)
        {
            Error = bindings.CaptureError ?? "No input confirmed. Rest the controls and retry.";
            bindings.RestoreDraftCheckpoint(stepCheckpoint);
            State = Stage.TimedOut;
        }
        else
            bindings.LimitCapture(Deadline); // A rejected conflict cannot extend this window.
    }

    internal void Retry(double now)
    {
        if (State != Stage.ReviewInput && State != Stage.TimedOut)
            return;
        bindings.RestoreDraftCheckpoint(stepCheckpoint);
        StartCapture(now);
    }

    internal void Confirm(double now)
    {
        if (State != Stage.ReviewInput)
            return;
        if (++Step == Actions.Length)
            State = Stage.Review;
        else
            StartCapture(now);
    }

    internal void KeepShift(double now)
    {
        if (Step < 3 || State == Stage.Review)
            return;
        bindings.RestoreDraftCheckpoint(stepCheckpoint);
        State = Stage.ReviewInput;
        Confirm(now);
    }

    internal void Cancel()
    {
        if (checkpoint != null)
            bindings.RestoreDraftCheckpoint(checkpoint);
        checkpoint = stepCheckpoint = null;
        State = Stage.Closed;
    }

    // Once any binding file has been persisted, cancellation must not restore
    // a checkpoint predating that write. The adapter retains remaining drafts.
    internal void RetireCheckpoint()
    {
        checkpoint = stepCheckpoint = null;
        State = Stage.Closed;
    }

    internal string Detected => bindings.CompactBindingName(Actions[Math.Min(Step, 4)], Idas3ControlBindings.Slot.Controller);
}
