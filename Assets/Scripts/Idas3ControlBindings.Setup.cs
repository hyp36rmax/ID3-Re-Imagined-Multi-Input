using System;

public sealed partial class Idas3ControlBindings
{
    // Capture rightward movement once, then derive the other half of the same
    // centered axis. Buttons and end-rest pedals cannot become steering pairs.
    internal bool DeriveSetupSteering()
    {
        var right = experimentalDraft.actions[(int)ActionId.SteerRight];
        var binding = right.binding;
        float span = binding.controlMax - binding.controlMin;
        if (!right.assigned || binding.controlButton || span <= 0 || binding.controlRest <= binding.controlMin + span * .1f || binding.controlRest >= binding.controlMax - span * .1f)
        {
            LastError = "Use a centered steering axis. Rest it at center before Retry.";
            return false;
        }

        var left = binding.Clone();
        left.controlPath = right.runtimePath;
        left.controlDirection = -binding.controlDirection;
        return SetExperimentalControl(ActionId.SteerLeft, left);
    }

    internal string CompactBindingName(ActionId action, Slot slot)
    {
        if (slot != Slot.Controller || !ExperimentalDraftEnabled)
            return ShortControlText(BindingName(action, slot), 42);
        return CompactAssignment(experimentalDraft.actions[(int)action]);
    }

    internal string CompactMenuBindingName(MenuActionId action) => CompactAssignment(experimentalDraft.menuActions[(int)action]);
    private string CompactAssignment(ExperimentalAssignment assignment)
    {
        if (string.IsNullOrEmpty(assignment.binding.controlPath))
            return "Not assigned";
        int number = 0;
        if (experimentalFrame != null)
            foreach (var device in experimentalFrame.Endpoints)
            {
                ++number;
                if (device.Token.Equals(assignment.endpoint))
                    break;
            }

        string source = ShortControlText(assignment.deviceName, 14);
        string control = assignment.binding.controlLabel ?? assignment.binding.controlPath;
        string status = !assignment.assigned || assignment.runtimePath == null || !experimentalControls.ContainsKey(assignment.runtimePath) ? " (reassign)" : "";
        return ShortControlText(control, 18) + " · " + source + (assignment.assigned ? " #" + number : "") + status;
    }

    internal static string ShortControlText(string text, int limit)
    {
        if (string.IsNullOrEmpty(text))
            return "Not assigned";
        return text.Length <= limit ? text : text.Substring(0, limit - 1) + "…";
    }
}
