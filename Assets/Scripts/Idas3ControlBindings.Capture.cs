using System;
using System.Collections.Generic;

public sealed partial class Idas3ControlBindings
{
    private Func<bool> replacement;
    private bool applyingReplacement;
    private DraftCheckpoint replacementCheckpoint;
    private bool replacementKeyArmed;
    internal bool ConflictPending => replacement != null;
    internal string CaptureConflictMessage { get; private set; }
    private readonly Dictionary<string, Binding> openingAxes = new Dictionary<string, Binding>();

    private bool OfferReplacement(string action, Func<bool> apply)
    {
        LastError = "Already assigned to " + action + ". Replace this assignment?";
        if (IsCapturing)
        {
            replacementCheckpoint = SaveDraftCheckpoint();
            replacement = apply;
            replacementKeyArmed = false;
            CaptureConflictMessage = LastError;
        }
        return false;
    }

    private void ClearReplacement()
    {
        replacement = null;
        replacementCheckpoint = null;
        CaptureConflictMessage = null;
        replacementKeyArmed = false;
    }

    internal void ResolveCaptureConflict(bool replace)
    {
        if (!ConflictPending) return;
        var apply = replacement;
        var checkpoint = replacementCheckpoint;
        ClearReplacement();
        if (!replace) { CancelCapture(); return; }
        // Apply both edits together; failed validation/source loss restores the
        // entire draft. Nothing reaches saved files until Save Changes.
        bool accepted;
        applyingReplacement = true;
        try { accepted = apply(); }
        finally { applyingReplacement = false; }
        string error = LastError;
        if (!accepted) RestoreDraftCheckpoint(checkpoint);
        CancelCapture();
        if (accepted) ++CaptureRevision;
        else CaptureError = error ?? "Source unavailable. Capture again.";
    }

    private static bool IsHat(Idas3ControllerControl control) => control.button &&
        (control.label != null && control.label.StartsWith("POV ", StringComparison.Ordinal) ||
         control.path.Contains("dpad/") || control.path.Contains("hatswitch/"));

    private void RememberOpeningAxes()
    {
        openingAxes.Clear();
        // An assigned axis used to open capture must return to its calibrated
        // rest. Otherwise its return movement could be captured backwards.
        foreach (var assignment in experimentalDraft.menuActions)
        {
            if (!assignment.assigned || assignment.binding.controlButton || assignment.runtimePath == null) continue;
            if (!controls.TryGetValue(assignment.runtimePath, out var control)) continue;
            if (CalibratedAmount(assignment.binding, control.value) < MenuReleasePoint) continue;
            openingAxes[assignment.runtimePath] = assignment.binding.Clone();
        }
    }

    private bool OpeningInputsReleased()
    {
        foreach (var pair in openingAxes)
            if (controls.TryGetValue(pair.Key, out var control) && CalibratedAmount(pair.Value, control.value) >= MenuReleasePoint)
                return false;
        return true;
    }
}
