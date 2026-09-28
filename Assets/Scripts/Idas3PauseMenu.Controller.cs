using System;
using UnityEngine;

// Game adapter for the replaceable ControlsSetup module. Persistence, capture,
// evaluated input and motor ownership remain with the existing game services.
public sealed partial class Idas3PauseMenu : IIdas3ControlsServices
{
    private Idas3ControlsSetupView controlsView;
    private bool controllerSaveIncomplete;
    private string controllerSaveMessage;
    private Idas3ControlsSetupView Controls => controlsView ?? (controlsView = new Idas3ControlsSetupView(this));
    internal bool TestingControls => IsOpen && showOptions && tab == 3 && Controls.Testing;
    internal bool SettingUpControls => IsOpen && showOptions && tab == 3 && Controls.Setup.Open;
    internal int ControllerPage => Controls.Page;

    private void CancelQuickSetup()
    {
        if (controlsView != null)
            controlsView.Setup.Cancel();
    }

    internal void SetControllerTestSample(Idas3Native.FrameInput frame, bool[] buttons, bool focused, bool suppressed) => Controls.Sample(frame, buttons, focused, suppressed);
    internal void SelectControllerPage(int page)
    {
        if (!showOptions || tab != 3)
            SelectTab(3);
        Controls.Select(page);
    }

    private void NavigateControlsSetup(int delta) => Controls.Navigate(delta);
    private void ActivateControlsSetup(bool pointer = false) => Controls.Activate();
    private void ControlsSetupView() => Controls.Draw();
    private void SaveControllerChanges()
    {
        if (SettingUpControls && !Controls.Setup.Complete)
        {
            notice = "Complete or cancel setup before saving.";
            return;
        }

        if (options.DisplayConfirmationPending)
        {
            notice = "Confirm or revert the display change first.";
            return;
        }

        var result = Idas3ControllerSave.Save(() => !bindings.HasUnsavedChanges || bindings.ApplyDraft(), () => bindings.LastError, controllerDevices.ApplySelectionEdit, () => controllerDevices.LastError, options.ApplyWheelSettings, () => options.LastError, bindings.ApplyExperimentalDraft, () => bindings.ExperimentalError);
        if (result.BindingsSaved)
            Controls.Setup.RetireCheckpoint();
        controllerSaveIncomplete = !result.Complete;
        controllerSaveMessage = result.Message;
        notice = result.Complete ? "Changes saved." : result.Message;
        Controls.Select(result.Complete ? 3 : 2);
    }

    private void DiscardControllerChanges()
    {
        controllerSaveIncomplete = false;
        controllerSaveMessage = null;
        CancelQuickSetup();
        controllerDevices.CancelSelectionEdit();
        controllerDevices.BeginSelectionEdit();
        bindings.CancelEdit(false);
        Idas3GameOptions.CopyWheelSettings(options.Current, options.Draft);
        notice = "Unsaved changes discarded.";
    }

    private void AdjustWheel(int row, int direction)
    {
        int previous = tab;
        try
        {
            tab = 4;
            Adjust(row, direction);
        }
        finally
        {
            tab = previous;
        }
    }

    Idas3ControlBindings IIdas3ControlsServices.Bindings => bindings;

    Idas3ControllerDevices IIdas3ControlsServices.Devices => controllerDevices;

    Idas3GameOptions IIdas3ControlsServices.Options => options;

    Idas3WheelFeedback IIdas3ControlsServices.Feedback => wheelFeedback;

    bool IIdas3ControlsServices.SaveIncomplete => controllerSaveIncomplete;

    string IIdas3ControlsServices.Message => controllerSaveIncomplete ? controllerSaveMessage : bindings.CaptureError ?? bindings.LastError ?? (!string.IsNullOrEmpty(notice) ? notice : bindings.HasUnsavedChanges || bindings.HasExperimentalChanges || controllerDevices.SelectionHasChanges || Idas3ControllerStatus.FeedbackPending(options.Current, options.Draft) ? "Unsaved changes." : "");

    void IIdas3ControlsServices.LogDevices()
    {
        var frame = controllerDevices.Snapshot;
        foreach (var endpoint in frame.Endpoints)
        {
            var report = new System.Text.StringBuilder("Controls device: ");
            report.Append(endpoint.Name).Append(" | session endpoint ").Append(endpoint.Token).Append(" | connection ").Append(endpoint.ConnectionGeneration).Append(" | ").Append(endpoint.Backend).Append(" | ").Append(endpoint.Status).Append(" | ").Append(endpoint.StatusDetail).Append(" | identity: ").Append(endpoint.Identity.ResolutionStatus).Append(" | aliases: ").Append(endpoint.AliasStatus);
            foreach (var field in endpoint.Identity.Fields)
                report.Append("\n  ").Append(field.Name).Append(" = ").Append(field.Value).Append(" (").Append(field.Source).Append(", ").Append(field.Status).Append(")");
            Debug.Log(report.ToString());
        }
    }

    void IIdas3ControlsServices.Save() => SaveControllerChanges();
    void IIdas3ControlsServices.Discard() => DiscardControllerChanges();
    void IIdas3ControlsServices.Leave()
    {
        if (controllerSaveIncomplete)
        {
            notice = controllerSaveMessage;
            return;
        }

        // Back leaves the panel; reopening a page keeps edits until Save/Discard
        // or closing Options. The host's existing close path cancels unsaved edits.
        selection = 0;
        Controls.Select(1);
        bindings.DisarmMenuNavigation();
    }

    void IIdas3ControlsServices.Rebind(Idas3ControlBindings.ActionId action, Idas3ControlBindings.Slot slot) => BeginBindingCapture(action, slot);
    void IIdas3ControlsServices.RebindMenu(Idas3ControlBindings.MenuActionId action) => BeginMenuBindingCapture(action);
    void IIdas3ControlsServices.AdjustFeedback(int row, int direction) => AdjustWheel(row, direction);
    void IIdas3ControlsServices.SelectSavedController()
    {
        bindings.SetExperimentalDraftEnabled(false);
        notice = "Saved controller setup selected. Save Changes to use it; wheel assignments are kept.";
    }
}
