using System;
using System.Collections.Generic;

public sealed partial class Idas3ControlBindings
{
    // Adapted from upstream .38's per-control guards, without its PollRig aggregation.
    // A held pedal cannot disable a fresh paddle on the same original controller.
    private int reconnectHeldAxes;
    private Predicate<string> releasedLegacyControl;
    private readonly HashSet<string> reconnectHeldControls = new HashSet<string>(StringComparer.Ordinal);
    private void ResetLegacyReconnect()
    {
        reconnectHeldAxes = 0;
        reconnectHeldControls.Clear();
    }

    private void PollLegacyReconnect()
    {
        if (awaitingProfileSample)
        {
            SnapshotRest(true);
            reconnectHeldButtons = pad.buttons;
            reconnectHeldAxes = HeldAxes();
            foreach (var binding in current.actions)
                if (!string.IsNullOrEmpty(binding.controlPath) && CustomAmountRaw(binding) > .15f)
                    reconnectHeldControls.Add(binding.controlPath);
            awaitingProfileSample = false;
            controllerReleaseBlocked = false;
        }

        reconnectHeldButtons &= pad.buttons;
        reconnectHeldAxes &= HeldAxes();
        reconnectHeldControls.RemoveWhere(releasedLegacyControl ?? (releasedLegacyControl = LegacyControlReleased));
    }

    private bool LegacyControlReleased(string path) => !CustomControlHeld(path);
    private bool CustomControlHeld(string path)
    {
        foreach (var binding in current.actions)
            if (binding.controlPath == path && CustomAmountRaw(binding) > .15f)
                return true;
        return false;
    }

    private int HeldAxes() => !pad.connected ? 0 : (pad.leftTrigger > 30 ? 1 : 0) | (pad.rightTrigger > 30 ? 2 : 0) | (Math.Abs((int)pad.thumbLX) > 8000 ? 4 : 0) | (Math.Abs((int)pad.thumbLY) > 8000 ? 8 : 0) | (Math.Abs((int)pad.thumbRX) > 8000 ? 16 : 0) | (Math.Abs((int)pad.thumbRY) > 8000 ? 32 : 0);
    private static int AxisMask(PadInput input)
    {
        switch (input)
        {
            case PadInput.LeftTrigger:
                return 1;
            case PadInput.RightTrigger:
                return 2;
            case PadInput.LeftStickLeft:
            case PadInput.LeftStickRight:
                return 4;
            case PadInput.LeftStickUp:
            case PadInput.LeftStickDown:
                return 8;
            case PadInput.RightStickLeft:
            case PadInput.RightStickRight:
                return 16;
            case PadInput.RightStickUp:
            case PadInput.RightStickDown:
                return 32;
            default:
                return 0;
        }
    }

    private void GuardLegacyAxes(ref Idas3Native.FrameInput frame)
    {
        if ((reconnectHeldAxes & 1) != 0)
            frame.leftTrigger = 0;
        if ((reconnectHeldAxes & 2) != 0)
            frame.rightTrigger = 0;
        if ((reconnectHeldAxes & 4) != 0)
            frame.thumbLX = 0;
        if ((reconnectHeldAxes & 8) != 0)
            frame.thumbLY = 0;
        if ((reconnectHeldAxes & 16) != 0)
            frame.thumbRX = 0;
        if ((reconnectHeldAxes & 32) != 0)
            frame.thumbRY = 0;
    }
}
