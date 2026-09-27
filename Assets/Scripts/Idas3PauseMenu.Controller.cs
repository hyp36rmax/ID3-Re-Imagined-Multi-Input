using System;
using UnityEngine;

public sealed partial class Idas3PauseMenu
{
    private static readonly string[] WheelPages =
    {
        "QUICK SETUP",
        "OVERVIEW",
        "BIND CONTROLS",
        "TEST CONTROLS",
        "FORCE FEEDBACK"
    };
    private static readonly Idas3ControlBindings.ActionId[] SetupActions =
    {
        Idas3ControlBindings.ActionId.SteerLeft,
        Idas3ControlBindings.ActionId.SteerRight,
        Idas3ControlBindings.ActionId.Accelerate,
        Idas3ControlBindings.ActionId.Brake,
        Idas3ControlBindings.ActionId.ShiftUp,
        Idas3ControlBindings.ActionId.ShiftDown
    };
    private int wheelPage = 1, quickStep;
    private bool wheelMenuBindings;
    private Vector2 menuTestScroll;
    private bool controllerTesting, overviewSaved, overviewDevices, testFocused, testSuppressed;
    private Vector2 controllerDeviceScroll, selectedNameScroll, activeNameScroll, outputNameScroll, controllerNoticeScroll, assignmentScroll, setupReviewScroll;
    private bool controllerSaveIncomplete;
    private string controllerSaveMessage;
    private Idas3Native.FrameInput controllerTestFrame;
    private readonly bool[] controllerTestButtons = new bool[10];
    private Idas3ControlBindings.DraftCheckpoint quickCheckpoint;
    internal bool TestingControls => IsOpen && showOptions && tab == 4 && wheelPage == 3 && controllerTesting;
    internal int ControllerPage => wheelPage;
    private int WheelPageRows => wheelPage == 2 ? (wheelMenuBindings ? 12 : 13) : wheelPage == 0 ? 9 : wheelPage == 4 ? 6 : wheelPage == 1 ? 4 : 3;

    internal void SetControllerTestSample(Idas3Native.FrameInput frame, bool[] buttons, bool focused, bool suppressed)
    {
        controllerTestFrame = frame;
        testFocused = focused;
        testSuppressed = suppressed;
        Array.Copy(buttons, controllerTestButtons, 10);
    }

    internal void SelectControllerPage(int page)
    {
        if (bindings != null && bindings.IsCapturing)
            return;
        if (tab != 4 || !showOptions)
            SelectTab(4);
        if (!controllerSaveIncomplete && quickStep < 6)
            CancelQuickSetup();
        controllerTesting = false;
        bindings?.DisarmMenuNavigation();
        wheelPage = Wrap(page, 5);
        wheelEditing = false;
        selection = 1;
        notice = "";
        if (wheelPage == 4)
            wheelFeedback?.RefreshDevices();
    }

    private void CancelQuickSetup()
    {
        if (quickCheckpoint == null)
            return;
        bindings.RestoreDraftCheckpoint(quickCheckpoint);
        quickCheckpoint = null;
        quickStep = 0;
    }

    private void NavigateWheelPage(int delta)
    {
        if (selection == 1)
        {
            SelectControllerPage(wheelPage + Math.Sign(delta));
            return;
        }

        if (selection == 2)
        {
            if (!bindings.ExperimentalDraftEnabled)
                ChangeControllerDevice(Math.Sign(delta));
            return;
        }

        if (wheelPage == 2 && selection <= Rows)
        {
            if (selection == 3)
            {
                wheelMenuBindings = !wheelMenuBindings;
                return;
            }

            if (wheelMenuBindings)
            {
                if (selection == 12)
                    bindings.SetMenuActivation(bindings.MenuActivationPoint + Math.Sign(delta) * .05f);
                return;
            }

            bindingColumn = Wrap(bindingColumn + Math.Sign(delta), 4);
            return;
        }

        if (wheelPage == 4 && selection <= Rows)
            AdjustWheel(selection - 3, Math.Sign(delta));
    }

    private void ActivateWheelPage(bool pointer = false)
    {
        if (!pointer && wheelNavigation && (selection <= 2 || wheelPage == 4 && selection <= Rows))
        {
            wheelEditing = !wheelEditing;
            return;
        }

        if (selection == 1)
        {
            SelectControllerPage(wheelPage + 1);
            return;
        }

        if (selection == 2)
        {
            if (!bindings.ExperimentalDraftEnabled)
                ChangeControllerDevice(1);
            return;
        }

        if (selection == Rows + 1)
        {
            DiscardControllerChanges();
            return;
        }

        if (selection == Rows + 2)
        {
            SaveControllerChanges();
            return;
        }

        if (selection == Rows + 3)
        {
            ToggleExperimentalInput();
            return;
        }

        if (selection == Rows + 4)
        {
            Back();
            return;
        }

        if (wheelPage == 2)
        {
            if (selection == 3)
            {
                wheelMenuBindings = !wheelMenuBindings;
                return;
            }

            if (wheelMenuBindings)
            {
                if (!bindings.ExperimentalDraftEnabled)
                {
                    notice = "Choose INPUT: MULTI to edit separate navigation assignments.";
                    return;
                }

                if (selection == 12)
                    bindings.SetMenuActivation(bindings.MenuActivationPoint >= .95f ? .45f : bindings.MenuActivationPoint + .05f);
                else
                    OpenMenuBindingChoice((Idas3ControlBindings.MenuActionId)(selection - 4));
            }
            else
                OpenBindingChoice((Idas3ControlBindings.ActionId)(selection - 4), (Idas3ControlBindings.Slot)bindingColumn);
            return;
        }

        if (wheelPage == 1)
        {
            if (selection == 3)
                overviewSaved = !overviewSaved;
            else
                overviewDevices = !overviewDevices;
            return;
        }

        if (wheelPage == 3)
        {
            controllerTesting = true;
            return;
        }

        if (wheelPage == 4)
        {
            AdjustWheel(selection - 3, 1);
            return;
        }

        if (selection >= 7)
        {
            if (quickCheckpoint != null && quickStep < 6)
            {
                notice = "Finish driving Setup before choosing navigation.";
                return;
            }

            if (selection == 9)
            {
                notice = "Current menu assignments kept; no driving assignments were copied.";
                return;
            }

            if (quickCheckpoint == null)
            {
                quickCheckpoint = bindings.BeginWheelSetup();
                quickStep = 6;
            }

            bool arcadeProposal = selection == 8;
            if (arcadeProposal && !bindings.ProposeArcadeNavigation())
            {
                notice = bindings.LastError;
                return;
            }

            wheelMenuBindings = true;
            SelectControllerPage(2);
            notice = arcadeProposal ? "Review proposed menu assignments before Save Changes." : "Choose each Menu action and Rebind a POV direction or button.";
            return;
        }

        if (selection == 3)
        {
            if (!bindings.CanCaptureExperimental)
            {
                notice = "Connect a supported input device to create a wheel setup. Keyboard bindings remain available.";
                return;
            }

            if (quickCheckpoint == null)
            {
                // New wheel assignments must never replace the existing controller profile.
                // Checkpoint before selecting the separate configuration so Cancel restores both.
                quickCheckpoint = bindings.BeginWheelSetup();
                quickStep = 0;
            }

            if (quickStep < SetupActions.Length)
            {
                captureAction = SetupActions[quickStep];
                captureSlot = Idas3ControlBindings.Slot.Controller;
                bindings.BeginCapture(captureAction, captureSlot, Time.realtimeSinceStartupAsDouble);
            }
        }
        else if (selection == 4 && quickCheckpoint != null && quickStep < SetupActions.Length)
        {
            if (quickStep < 4 && !bindings.HasControllerAssignment(SetupActions[quickStep]))
            {
                notice = "Bind this required action before continuing.";
                return;
            }

            ++quickStep;
            notice = quickStep == 6 ? "Review the assignments below, then Save Changes. Cancel restores your prior draft." : "Accepted in draft. Release controls before capturing the next action.";
        }
        else if (selection == 5 && quickCheckpoint != null && quickStep >= 4 && quickStep < 6)
        {
            ++quickStep;
            notice = "Existing shift assignment kept.";
        }
        else if (selection == 6)
        {
            CancelQuickSetup();
            notice = "Quick Setup cancelled; previous draft restored.";
        }
    }

    private void SaveControllerChanges()
    {
        if (bindings == null || options == null || controllerDevices == null)
        {
            notice = "Controller services unavailable; nothing saved.";
            return;
        }

        if (quickCheckpoint != null && quickStep < 6)
        {
            notice = "Finish and review Quick Setup before saving, or cancel it.";
            return;
        }

        if (options.DisplayConfirmationPending)
        {
            notice = "Confirm or revert the pending display change before saving Controller settings.";
            return;
        }

        var result = Idas3ControllerSave.Save(() => !bindings.HasUnsavedChanges || bindings.ApplyDraft(), () => bindings.LastError, controllerDevices.ApplySelectionEdit, () => controllerDevices.LastError, options.ApplyWheelSettings, () => options.LastError, bindings.ApplyExperimentalDraft, () => bindings.ExperimentalError);
        // Never restore a pre-Setup checkpoint over bindings that have reached disk.
        if (result.BindingsSaved)
            quickCheckpoint = null;
        controllerSaveIncomplete = !result.Complete;
        controllerSaveMessage = result.Message;
        notice = result.Message;
    }

    private void ToggleExperimentalInput()
    {
        CancelQuickSetup();
        controllerTesting = false;
        bindings.SetExperimentalDraftEnabled(!bindings.ExperimentalDraftEnabled);
        notice = bindings.ExperimentalDraftEnabled ? "Multi-input preview. Set up each action using its own device. FFB disabled; Save Changes to use in game." : "Existing controls preview. Save Changes to return; multi-input preferences are kept.";
    }

    private void DiscardControllerChanges()
    {
        controllerSaveIncomplete = false;
        controllerSaveMessage = null;
        CancelQuickSetup();
        controllerTesting = false;
        controllerDevices?.CancelSelectionEdit();
        controllerDevices?.BeginSelectionEdit();
        bindings?.CancelEdit(false);
        Idas3GameOptions.CopyWheelSettings(options.Current, options.Draft);
        notice = "Shared CONTROLS + WHEEL draft discarded. Saved configuration restored.";
    }

    private void AdjustWheel(int row, int direction)
    {
        // Reuse the existing option adjustment path; no hardware output here.
        int previousTab = tab;
        try
        {
            tab = 4;
            Adjust(row, direction);
        }
        finally
        {
            tab = previousTab;
        }
    }

    private bool ControllerButton(Rect rect, string text, int row, bool enabled = true)
    {
        if (!Button(rect, text, selection == row, enabled))
            return false;
        selection = row;
        ActivateWheelPage(true);
        return true;
    }

    private void WheelView()
    {
        Fill(new Rect(262, 130, 748, 413), Panel);
        Text(new Rect(282, 142, 680, 34), (bindings?.ExperimentalDraftEnabled == true ? "WHEEL — MULTI-INPUT SAMPLE" : "WHEEL — EXISTING CONTROLS"), heading);
        for (int i = 0; i < 5; ++i)
            if (Button(new Rect(276 + i * 144, 181, 140, 32), WheelPages[i], wheelPage == i, true, false, bindingButton))
                SelectControllerPage(i);
        if (selection == 1)
            Frame(new Rect(274, 179, 720, 36), Red);
        string active = controllerDevices?.ActiveName ?? bindings?.ActiveControllerProfileLabel ?? "No controller";
        string selected = active;
        if (controllerDevices != null)
            foreach (var choice in controllerDevices.Choices)
                if (choice.key == controllerDevices.SelectedKey)
                    selected = choice.label;
        ScrollControllerName(new Rect(278, 217, 555, 31), bindings?.ExperimentalDraftEnabled == true ? "Capture each action from its own device" : "Selected: " + selected + " | " + (controllerDevices != null && controllerDevices.UsingFallback ? "Active fallback: " : "Active: ") + active, ref selectedNameScroll);
        ControllerButton(new Rect(841, 219, 149, 29), "CHANGE DEVICE", 2, controllerDevices != null && controllerDevices.Choices.Count > 0 && !bindings.ExperimentalDraftEnabled);
        ScrollControllerName(new Rect(278, 250, 712, 30), SharedConfigurationStatus, ref activeNameScroll);
        if (bindings == null)
        {
            Text(new Rect(285, 293, 695, 40), "Binding service failed to initialize. Controller setup cannot run.", wrapped);
            return;
        }

        if (wheelPage == 0)
            DrawWheelQuickSetup();
        else if (wheelPage == 1)
            DrawWheelOverview();
        else if (wheelPage == 2)
            DrawWheelBindings();
        else if (wheelPage == 3)
            DrawWheelTest();
        else if (wheelPage == 4)
            DrawWheelFeedback();
        ControllerStatusView();
        if (Button(new Rect(262, 583, 177, 35), "DISCARD CHANGES", selection == Rows + 1))
            DiscardControllerChanges();
        if (Button(new Rect(447, 583, 180, 35), "SAVE CHANGES", selection == Rows + 2, true, true))
            SaveControllerChanges();
        if (Button(new Rect(635, 583, 185, 35), bindings.ExperimentalDraftEnabled ? "INPUT: MULTI" : "INPUT: EXISTING", selection == Rows + 3))
            ToggleExperimentalInput();
        if (Button(new Rect(828, 583, 182, 35), "BACK", selection == Rows + 4))
            Back();
    }

    private void DrawWheelQuickSetup()
    {
        string prompt = quickCheckpoint == null ? (bindings.ExperimentalDraftEnabled ? "Start setup; move only the device for each action." : "Separate wheel setup; original mappings kept. FFB disabled.") : quickStep < 6 ? (quickStep < 2 ? "1 / 5 — Steering: " + (quickStep == 0 ? "turn left" : "turn right") : (quickStep) + " / 5 — " + Idas3ControlBindings.ActionName(SetupActions[quickStep])) + ". Capture, then accept." : "REVIEW — check assignments, then Save Changes.";
        Text(new Rect(285, 282, 700, 30), prompt, label);
        ControllerButton(new Rect(280, 318, 225, 32), quickCheckpoint == null ? "NEW WHEEL SETUP" : "CAPTURE AGAIN", 3, quickCheckpoint == null || quickStep < 6);
        ControllerButton(new Rect(512, 318, 232, 32), "ACCEPT / NEXT", 4, quickCheckpoint != null && quickStep < 6);
        ControllerButton(new Rect(751, 318, 239, 32), "KEEP OPTIONAL SHIFT", 5, quickCheckpoint != null && quickStep >= 4 && quickStep < 6);
        string review = "Steering — Left: " + bindings.BindingName(SetupActions[0], Idas3ControlBindings.Slot.Controller) + "\nRight: " + bindings.BindingName(SetupActions[1], Idas3ControlBindings.Slot.Controller);
        for (int i = 2; i < 6; ++i)
            review += "\n" + Idas3ControlBindings.ActionName(SetupActions[i]) + ": " + bindings.BindingName(SetupActions[i], Idas3ControlBindings.Slot.Controller);
        float reviewHeight = Math.Max(140, wrapped.CalcHeight(new GUIContent(review), 677) + 8);
        setupReviewScroll = GUI.BeginScrollView(new Rect(280, 355, 712, 104), setupReviewScroll, new Rect(0, 0, 690, reviewHeight));
        Text(new Rect(6, 0, 677, reviewHeight), review, wrapped);
        GUI.EndScrollView();
        ControllerButton(new Rect(280, 466, 225, 29), "POV / BUTTONS", 7, quickCheckpoint == null || quickStep >= 6);
        ControllerButton(new Rect(512, 466, 232, 29), "ARCADE NAVIGATION", 8, quickCheckpoint == null || quickStep >= 6);
        ControllerButton(new Rect(751, 466, 239, 29), "KEEP CURRENT NAV", 9, quickCheckpoint == null || quickStep >= 6);
        ControllerButton(new Rect(730, 501, 260, 29), "CANCEL SETUP", 6, quickCheckpoint != null);
    }

    private void DrawWheelOverview()
    {
        ControllerButton(new Rect(280, 280, 455, 28), overviewSaved ? "SAVED — SWITCH TO DRAFT" : "DRAFT — SWITCH TO SAVED", 3);
        ControllerButton(new Rect(742, 280, 248, 28), overviewDevices ? "SHOW ASSIGNMENTS" : "AVAILABLE DEVICES", 4);
        if (overviewDevices)
        {
            var choices = controllerDevices?.Choices;
            Text(new Rect(285, 314, 695, 22), "Keyboard mappings stay available in Bind Controls (three slots per action).", small);
            int count = choices?.Count ?? 0;
            var names = new string[count];
            var heights = new float[count];
            float total = 0;
            for (int i = 0; i < count; ++i)
            {
                names[i] = choices[i].label + " — " + (choices[i].connected ? "available" : "disconnected") + (choices[i].key == controllerDevices.SelectedKey ? " / selected" : "");
                heights[i] = Math.Max(30, wrapped.CalcHeight(new GUIContent(names[i]), 665) + 8);
                total += heights[i];
            }

            controllerDeviceScroll = GUI.BeginScrollView(new Rect(280, 344, 712, 183), controllerDeviceScroll, new Rect(0, 0, 687, Math.Max(183, total)));
            float nameY = 0;
            for (int i = 0; i < count; ++i)
            {
                Text(new Rect(8, nameY, 665, heights[i]), names[i], wrapped);
                nameY += heights[i];
            }

            GUI.EndScrollView();
        }
        else
        {
            // Device names and reassignment status must remain readable together.
            float total = 0;
            var lines = new string[10];
            var heights = new float[10];
            for (int i = 0; i < 10; ++i)
            {
                var action = (Idas3ControlBindings.ActionId)i;
                string keys = "";
                for (int k = 0; k < 3; ++k)
                    keys += (k > 0 ? " / " : "") + bindings.BindingName(action, (Idas3ControlBindings.Slot)k, !overviewSaved);
                lines[i] = Idas3ControlBindings.ActionName(action) + ": " + bindings.BindingName(action, Idas3ControlBindings.Slot.Controller, !overviewSaved) + "\nKeyboard: " + keys;
                heights[i] = Math.Max(46, wrapped.CalcHeight(new GUIContent(lines[i]), 675) + 8);
                total += heights[i];
            }

            assignmentScroll = GUI.BeginScrollView(new Rect(280, 311, 712, 222), assignmentScroll, new Rect(0, 0, 689, total));
            float y = 0;
            for (int i = 0; i < 10; ++i)
            {
                Text(new Rect(5, y, 675, heights[i]), lines[i], wrapped);
                y += heights[i];
            }

            GUI.EndScrollView();
        }
    }

    private void DrawWheelBindings()
    {
        ControllerButton(new Rect(280, 278, 710, 28), wheelMenuBindings ? "MENU — SWITCH TO DRIVING" : "DRIVING — SWITCH TO MENU", 3);
        if (wheelMenuBindings)
        {
            for (int i = 0; i < Idas3ControlBindings.MenuActionCount; ++i)
            {
                var action = (Idas3ControlBindings.MenuActionId)i;
                float y = 313 + i * 22;
                Text(new Rect(285, y + 2, 205, 21), Idas3ControlBindings.MenuActionNames[i], small);
                if (Button(new Rect(491, y, 494, 21), bindings.MenuBindingName(action), selection == i + 4, true, false, bindingButton))
                {
                    selection = i + 4;
                    OpenMenuBindingChoice(action);
                }
            }

            ControllerButton(new Rect(280, 497, 710, 30), "MENU AXIS ACTIVATION POINT: " + Mathf.RoundToInt(bindings.MenuActivationPoint * 100) + "%  (‹ / ›)", 12);
            Text(new Rect(285, 527, 700, 16), "Release below 40%. Repeat off. Development values; verify on your rig.", small);
            return;
        }

        string[] columns =
        {
            "KEYBOARD 1",
            "KEYBOARD 2",
            "KEYBOARD 3",
            "CONTROLLER"
        };
        for (int col = 0; col < 4; ++col)
            if (Button(new Rect(col == 3 ? 804 : 462 + col * 114, 309, col == 3 ? 180 : 110, 22), columns[col], bindingColumn == col, true, false, bindingButton))
                SelectBindingColumn(col);
        for (int row = 0; row < 10; ++row)
        {
            var action = (Idas3ControlBindings.ActionId)row;
            float y = 335 + row * 20;
            Text(new Rect(285, y + 2, 171, 20), Idas3ControlBindings.ActionName(action), small);
            for (int col = 0; col < 4; ++col)
            {
                var slot = (Idas3ControlBindings.Slot)col;
                if (Button(new Rect(col == 3 ? 804 : 462 + col * 114, y, col == 3 ? 180 : 110, 19), bindings.BindingName(action, slot), selection == row + 4 && bindingColumn == col, true, false, bindingButton))
                {
                    selection = row + 4;
                    bindingColumn = col;
                    OpenBindingChoice(action, slot);
                }
            }
        }
    }

    private void DrawWheelTest()
    {
        if (!controllerTesting)
            ControllerButton(new Rect(282, 284, 704, 36), "START LIVE TEST (ESC TO STOP)", 3);
        else
            Text(new Rect(285, 284, 700, 30), !testFocused ? "UNFOCUSED — test unavailable" : testSuppressed ? "RELEASE CONTROLS — capture/reconnect guard active" : "LIVE — evaluated draft bindings; gameplay blocked", label);
        if (controllerTesting && testFocused && !testSuppressed)
        {
            float steer = controllerTestFrame.thumbLX / (controllerTestFrame.thumbLX < 0 ? 32768f : 32767f);
            if (controllerTestButtons[2] || controllerTestButtons[3])
            {
                // Keyboard steering is a digital demand; analog normalization remains the mapper's.
                if (TestKey(controllerTestFrame, 65) || TestKey(controllerTestFrame, 68))
                    steer = (TestKey(controllerTestFrame, 68) ? 1 : 0) - (TestKey(controllerTestFrame, 65) ? 1 : 0);
            }

            Text(new Rect(285, 326, 700, 28), "Steering " + steer.ToString("0.000") + "   Accelerator " + (TestKey(controllerTestFrame, 87) ? 1 : controllerTestFrame.rightTrigger / 255f).ToString("0.000") + "   Brake " + (TestKey(controllerTestFrame, 83) ? 1 : controllerTestFrame.leftTrigger / 255f).ToString("0.000"), label);
            for (int i = 0; i < 10; ++i)
                Text(new Rect(286 + (i % 2) * 350, 356 + (i / 2) * 18, 342, 25), Idas3ControlBindings.ActionName((Idas3ControlBindings.ActionId)i) + ": " + (controllerTestButtons[i] ? "ON" : "OFF"), small);
        }

        if (controllerTesting)
        {
            string monitor = "Last event: " + bindings.LastMenuEvent + " | Menu activation " + Mathf.RoundToInt(bindings.MenuActivationPoint * 100) + "% / re-arm below 40%; no repeat.\n";
            for (int i = 0; i < Idas3ControlBindings.MenuActionCount; ++i)
            {
                float amount = bindings.MenuAmount(i);
                string region = amount < Idas3ControlBindings.MenuReleasePoint ? "re-arm region" :
                    amount >= bindings.MenuActivationPoint ? "activation region" : "between thresholds";
                string state = bindings.MenuAvailable(i) ? Mathf.RoundToInt(amount * 100) + "% " + region + " / " +
                    (bindings.MenuArmed(i) ? "armed" : "release to arm") : "unavailable — connect / reassign";
                monitor += Idas3ControlBindings.MenuActionNames[i] + ": " + state +
                    (bindings.MenuEvent((Idas3ControlBindings.MenuActionId)i) ? " — EVENT" : "") + "\n";
            }
            menuTestScroll = GUI.BeginScrollView(new Rect(280, 453, 712, 57), menuTestScroll, new Rect(0, 0, 686, 180));
            Text(new Rect(0, 0, 686, 180), monitor, small);
            GUI.EndScrollView();
        }

        Text(new Rect(285, 514, 700, 22), "Evaluated input before native response. Menu events test only; Escape exits.", small);
    }

    private void DrawWheelFeedback()
    {
        string[] names =
        {
            "ENABLE (DRAFT)",
            "OUTPUT",
            "STRENGTH",
            "INVERT"
        };
        var v = options.Draft;
        string[] values =
        {
            v.wheelForceFeedback ? "ON" : "OFF",
            WheelDeviceName(v.wheelFeedbackDevice),
            Mathf.RoundToInt(v.wheelFeedbackStrength * 100) + "%",
            v.wheelFeedbackInvert ? "ON" : "OFF"
        };
        for (int i = 0; i < 4; ++i)
        {
            float y = 284 + i * 46;
            Text(new Rect(285, y + 5, 185, 28), names[i], label);
            if (Button(new Rect(479, y, 36, 32), "‹", selection == i + 3))
            {
                selection = i + 3;
                AdjustWheel(i, -1);
            }

            if (i == 1)
                ScrollControllerName(new Rect(521, y, 416, 32), values[i], ref outputNameScroll);
            else
                ControllerButton(new Rect(521, y, 416, 32), values[i], i + 3);
            if (Button(new Rect(943, y, 40, 32), "›", selection == i + 3))
            {
                selection = i + 3;
                AdjustWheel(i, 1);
            }
        }

        string feedback = Idas3ControllerStatus.Feedback(options.Current, options.Draft, bindings.ExperimentalBlocksFeedback, wheelFeedback?.StatusText);
        float feedbackHeight = Math.Max(62, wrapped.CalcHeight(new GUIContent(feedback), 680));
        feedbackStatusScroll = GUI.BeginScrollView(new Rect(280, 468, 712, 65), feedbackStatusScroll, new Rect(0, 0, 680, feedbackHeight));
        Text(new Rect(0, 0, 680, feedbackHeight), feedback, wrapped);
        GUI.EndScrollView();
    }

    private Vector2 feedbackStatusScroll;
    private string SharedConfigurationStatus => Idas3ControllerStatus.Configuration(bindings?.ExperimentalEnabled == true, bindings?.ExperimentalDraftEnabled == true);

    private void ControllerStatusView()
    {
        string error = bindings?.CaptureError ?? bindings?.LastError;
        bool dirty = bindings?.HasUnsavedChanges == true || bindings?.HasExperimentalChanges == true || controllerDevices?.SelectionHasChanges == true || Idas3ControllerStatus.FeedbackPending(options.Current, options.Draft);
        string message = controllerSaveIncomplete ? controllerSaveMessage : !string.IsNullOrEmpty(error) ? error : !string.IsNullOrEmpty(notice) ? notice : (dirty ? "UNSAVED CHANGES. " : "") + (bindings?.ExperimentalDraftEnabled == true ? "Separate multi-input assignments; keyboard and FFB settings are shared." : "Editing existing controller profile. Save replaces its assignments; use New Wheel Setup to keep them.");
        float messageHeight = Math.Max(33, wrapped.CalcHeight(new GUIContent(message), 710));
        controllerNoticeScroll = GUI.BeginScrollView(new Rect(274, 544, 730, 38), controllerNoticeScroll, new Rect(0, 0, 710, messageHeight));
        Text(new Rect(0, 0, 710, messageHeight), message, wrapped);
        GUI.EndScrollView();
    }

    // Preserve the complete driver name; horizontal scrolling handles arbitrary lengths.
    private void ScrollControllerName(Rect rect, string name, ref Vector2 scroll)
    {
        float width = Math.Max(rect.width - 18, small.CalcSize(new GUIContent(name)).x + 8);
        scroll = GUI.BeginScrollView(rect, scroll, new Rect(0, 0, width, 17));
        Text(new Rect(0, 0, width, 17), name, small);
        GUI.EndScrollView();
    }

    private static bool TestKey(Idas3Native.FrameInput f, int key)
    {
        uint word = key < 32 ? f.key0 : key < 64 ? f.key1 : key < 96 ? f.key2 : f.key3;
        return (word & (1u << (key & 31))) != 0;
    }
}
