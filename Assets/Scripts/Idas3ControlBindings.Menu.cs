using System;
using UnityEngine;

public sealed partial class Idas3ControlBindings
{
    // Start is the native frontend button; Confirm is Enter/A in managed menus.
    public enum MenuActionId
    {
        Up,
        Down,
        Left,
        Right,
        Confirm,
        Back,
        Pause,
        Start
    }

    internal static readonly string[] MenuActionNames =
    {
        "Up",
        "Down",
        "Left",
        "Right",
        "Confirm",
        "Back / Cancel",
        "Open Options / Pause",
        "Start (frontend)"
    };
    internal const int MenuActionCount = 8;
    internal const float MenuReleasePoint = .40f;
    private static readonly int[] MenuOutputKeys =
    {
        38,
        40,
        37,
        39,
        13,
        8
    };
    private readonly Idas3MenuExcursion legacyMenuExcursion = new Idas3MenuExcursion();
    private int menuCaptureAction = -1;
    private int menuContext = -1;
    private bool menuWasFocused, menuWasPreview;
    private readonly bool[] menuArmed = new bool[MenuActionCount];
    private readonly bool[] menuAvailable = new bool[MenuActionCount];
    private readonly float[] menuAmounts = new float[MenuActionCount];
    internal int MenuEvents { get; private set; }
    internal string LastMenuEvent { get; private set; } = "none";
    internal string MenuCaptureName => menuCaptureAction < 0 ? null : MenuActionNames[menuCaptureAction];

    internal void ClearCapturedMenuAssignment()
    {
        if (menuCaptureAction >= 0)
            ClearMenuAssignment((MenuActionId)menuCaptureAction);
    }

    internal bool ExplicitMenuOwnsSample => ExperimentalEnabled || ExplicitMenuControlHeld;
    internal bool ExplicitMenuControlHeld
    {
        get
        {
            if (MenuEvents != 0) return true;
            for (int i = 0; i < menuAmounts.Length; ++i)
                if (menuAvailable[i] && menuAmounts[i] >= MenuReleasePoint) return true;
            return false;
        }
    }

    internal float MenuActivationPoint => experimentalDraft.menuActivation;

    internal bool MenuAvailable(int action) => menuAvailable[action];
    internal float MenuAmount(int action) => menuAmounts[action];
    internal bool MenuArmed(int action) => menuArmed[action];
    internal bool MenuEvent(MenuActionId action) => (MenuEvents & (1 << (int)action)) != 0;
    private static ExperimentalAssignment[] EmptyMenuAssignments()
    {
        var assignments = new ExperimentalAssignment[MenuActionCount];
        for (int i = 0; i < assignments.Length; ++i)
            assignments[i] = new ExperimentalAssignment();
        return assignments;
    }

    internal void DisarmMenuNavigation() => ResetMenuNavigation();

    private void ResetMenuNavigation()
    {
        legacyMenuExcursion.Reset();
        Array.Clear(menuArmed, 0, menuArmed.Length);
        Array.Clear(menuAmounts, 0, menuAmounts.Length);
        Array.Clear(menuAvailable, 0, menuAvailable.Length);
        MenuEvents = 0;
        LastMenuEvent = "none";
    }

    internal void SetMenuActivation(float value)
    {
        if (!Finite(value))
            throw new ArgumentException("Activation must be finite.");
        // Keep release below activation. These are development settings, not hardware-approved values.
        experimentalDraft.menuActivation = Math.Max(.45f, Math.Min(.95f, value));
        experimentalDirty = true;
        ResetMenuNavigation();
    }

    internal void BeginMenuCapture(MenuActionId action, double now)
    {
        if (!CanCaptureExperimental)
        {
            LastError = "No supported input sample is available. Connect a device before capturing.";
            return;
        }
        using (var scope = new ExperimentalScope(this))
            BeginCaptureCore(ActionId.Accelerate, Slot.Controller, now);
        menuCaptureAction = (int)action;
        CapturePrompt = "Rest the control, then move to a comfortable navigation extent (not a mechanical stop). Escape cancels.";
    }

    internal void ClearMenuAssignment(MenuActionId action)
    {
        experimentalDraft.menuActions[(int)action] = new ExperimentalAssignment();
        LastError = null;
        LastNotice = "Menu assignment cleared. Save Changes to save.";
        experimentalDirty = true;
        ResetMenuNavigation();
    }

    private bool SetMenuControl(MenuActionId action, Binding binding, bool useCapturedExtent)
    {
        if (!experimentalSources.TryGetValue(binding.controlPath, out var source) || !experimentalControls.TryGetValue(binding.controlPath, out var control))
        {
            LastError = "Menu source unavailable; capture it again.";
            return false;
        }

        for (int i = 0; i < MenuActionCount; ++i)
        {
            var other = experimentalDraft.menuActions[i];
            if (i != (int)action && other.assigned && other.runtimePath == binding.controlPath && other.binding.controlDirection == binding.controlDirection)
            {
                LastError = "Menu control already assigned to " + MenuActionNames[i] + ". Clear it first.";
                return false;
            }
        }

        var calibrated = binding.Clone();
        if (useCapturedExtent && !calibrated.controlButton)
        {
            // Only menu calibration changes. Driving calibration remains untouched.
            if (calibrated.controlDirection > 0)
                calibrated.controlMax = control.value;
            else
                calibrated.controlMin = control.value;
        }

        if (!ValidExperimentalBinding(calibrated))
        {
            LastError = "Invalid menu calibration.";
            return false;
        }

        calibrated.controlPath = source.localPath;
        experimentalDraft.menuActions[(int)action] = new ExperimentalAssignment
        {
            deviceName = source.name,
            binding = calibrated,
            endpoint = source.endpoint,
            generation = source.generation,
            runtimePath = binding.controlPath,
            assigned = true
        };
        experimentalDirty = true;
        LastError = null;
        ResetMenuNavigation();
        return true;
    }

    internal bool TrySetMenuControl(MenuActionId action, Idas3EndpointToken endpoint, long generation, string path, int direction, float rest)
    {
        if (!ExperimentalDraftEnabled || experimentalFrame == null || !experimentalFrame.TryGetEndpoint(endpoint, out var device) || device.ConnectionGeneration != generation)
            return false;
        string runtimePath = ExperimentalPath(device, path);
        if (!experimentalControls.TryGetValue(runtimePath, out var control))
            return false;
        return SetMenuControl(action, new Binding { controlPath = runtimePath, controlLabel = control.label, controlDirection = direction, controlRest = rest, controlMin = control.minimum, controlMax = control.maximum, controlButton = control.button }, false);
    }

    internal string MenuBindingName(MenuActionId action, bool preview = true) => ExperimentalAssignmentName((preview ? experimentalDraft : experimentalCurrent).menuActions[(int)action]);
    internal bool ProposeArcadeNavigation()
    {
        // Explicit proposal only, shown in the draft review. Never called by driving capture.
        var proposal = experimentalDraft.Clone();
        int[] driving =
        {
            4,
            5,
            2,
            3,
            0,
            1,
            -1,
            -1
        };
        for (int i = 0; i < driving.Length; ++i)
            if (driving[i] >= 0)
                proposal.menuActions[i] = experimentalDraft.actions[driving[i]].Clone();
        for (int i = 0; i < MenuActionCount; ++i)
            for (int j = i + 1; j < MenuActionCount; ++j)
            {
                var left = proposal.menuActions[i];
                var right = proposal.menuActions[j];
                if (left.assigned && right.assigned && left.runtimePath == right.runtimePath && left.binding.controlDirection == right.binding.controlDirection)
                {
                    LastError = "Arcade proposal conflicts: " + MenuActionNames[i] + " / " + MenuActionNames[j] + ". Existing navigation kept.";
                    return false;
                }
            }

        experimentalDraft.menuActions = proposal.menuActions;
        experimentalDirty = true;
        ResetMenuNavigation();
        LastError = null;
        return true;
    }

    // Called once after the normal physical Poll. The context changes at menu/test/focus boundaries.
    // Unavailable endpoints disarm only their own actions; returning samples must release first.
    internal void EvaluateMenuNavigation(int context, bool focused, bool preview)
    {
        if (context != menuContext || focused != menuWasFocused || preview != menuWasPreview)
            ResetMenuNavigation();
        menuContext = context;
        menuWasFocused = focused;
        menuWasPreview = preview;
        MenuEvents = 0;
        var settings = preview ? experimentalDraft : experimentalCurrent;
        if (!focused || SuppressInput)
        {
            ResetMenuNavigation();
            return;
        }

        using (var scope = new ExperimentalScope(this))
        {
            for (int i = 0; i < MenuActionCount; ++i)
            {
                var assignment = settings.menuActions[i];
                bool available = assignment.assigned && assignment.runtimePath != null && experimentalControls.ContainsKey(assignment.runtimePath);
                menuAvailable[i] = available;
                if (!available)
                {
                    menuArmed[i] = false;
                    menuAmounts[i] = 0;
                    continue;
                }

                float amount = CalibratedAmount(assignment.binding, experimentalControls[assignment.runtimePath].value);
                menuAmounts[i] = amount;
                if (amount < MenuReleasePoint && MenuAxisAtRest(settings, assignment))
                    menuArmed[i] = true;
                else if (amount >= settings.menuActivation && menuArmed[i])
                {
                    menuArmed[i] = false;
                    MenuEvents |= 1 << i;
                }
            }
        }

        // Opposing directions cancel. Diagonals consume both excursions, with vertical priority.
        if (menuAmounts[0] >= settings.menuActivation && menuAmounts[1] >= settings.menuActivation)
            MenuEvents &= ~3;
        if (menuAmounts[2] >= settings.menuActivation && menuAmounts[3] >= settings.menuActivation)
            MenuEvents &= ~12;
        if (menuAmounts[0] >= settings.menuActivation || menuAmounts[1] >= settings.menuActivation)
            MenuEvents &= ~12;
        // One command per sample; Back wins over Confirm/Start, Pause wins over other actions.
        if (MenuEvent(MenuActionId.Pause))
            MenuEvents = 1 << (int)MenuActionId.Pause;
        else if (MenuEvent(MenuActionId.Back))
            MenuEvents = 1 << (int)MenuActionId.Back;
        else if (MenuEvent(MenuActionId.Confirm))
            MenuEvents = 1 << (int)MenuActionId.Confirm;
        else if (MenuEvent(MenuActionId.Start))
            MenuEvents = 1 << (int)MenuActionId.Start;
        for (int i = 0; i < MenuActionCount; ++i)
            if ((MenuEvents & (1 << i)) != 0)
                LastMenuEvent = MenuActionNames[i];
    }

    private bool MenuAxisAtRest(ExperimentalValues settings, ExperimentalAssignment assignment)
    {
        // At entry, the opposite direction of an already-deflected wheel is not neutral.
        // Re-arm a shared axis only when all of its assigned directions are below release.
        if (assignment.binding.controlButton) return true;
        foreach (var other in settings.menuActions)
            if (other.assigned && other.runtimePath == assignment.runtimePath &&
                CalibratedAmount(other.binding, experimentalControls[other.runtimePath].value) >= MenuReleasePoint)
                return false;
        return true;
    }

    private void ApplyExplicitMenu(ref Idas3Native.FrameInput frame)
    {
        frame.padConnected = experimentalControls.Count > 0 ? 1u : 0u;
        frame.padButtons = frame.leftTrigger = frame.rightTrigger = 0;
        frame.thumbLX = frame.thumbLY = frame.thumbRX = frame.thumbRY = 0;
        if (SuppressInput || menuWasPreview)
            return;
        var keys = MenuOutputKeys;
        for (int i = 0; i < keys.Length; ++i)
            if ((MenuEvents & (1 << i)) != 0)
                frame.SetKey(keys[i]);
        if (MenuEvent(MenuActionId.Back))
            frame.padButtons |= 0x2000;
        // The native frontend consumes Enter in every selection stage. Start is
        // deliberately not Confirm in host settings, nor Pause during a race.
        if (MenuEvent(MenuActionId.Start) && menuContext >= 100)
            frame.SetKey(13);
    }
}
