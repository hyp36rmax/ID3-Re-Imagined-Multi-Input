using System;
using UnityEngine;

// One entry point for the replacement settings UI. Input data comes from the
// host's evaluated frame, never from a second device read or raw-axis preview.
internal sealed class Idas3ControlsSetupView
{
    internal static readonly string[] Pages =
    {
        "Quick Setup",
        "Devices",
        "Bindings",
        "Test Inputs",
        "Force Feedback"
    };
    private readonly IIdas3ControlsServices services;
    internal readonly Idas3SetupSession Setup;
    internal int Page { get; private set; } = 1;

    private int bindingGroup;
    private int keyboardSlot, focus = 1;
    private readonly System.Collections.Generic.List<Action> actions = new System.Collections.Generic.List<Action>();
    private GUIStyle text, heading, button, secondary;
    private Vector2 scroll;
    private long loggedInventory = -1;
    private Idas3Native.FrameInput sample;
    private bool sampleAvailable;
    private readonly bool[] held = new bool[10];
    internal bool Testing => Page == 3 && !Setup.Open;

    internal Idas3ControlsSetupView(IIdas3ControlsServices services)
    {
        this.services = services;
        Setup = new Idas3SetupSession(services.Bindings);
    }

    internal void Select(int page)
    {
        if (Setup.Open || services.Bindings.IsCapturing || page == 0 && services.SaveIncomplete)
            return;
        Page = (page + Pages.Length) % Pages.Length;
        focus = Page;
        scroll = Vector2.zero;
        services.Bindings.DisarmMenuNavigation();
        if (Page == 0)
            Setup.Begin(Time.realtimeSinceStartupAsDouble);
        if (Page == 4)
            services.Feedback?.RefreshDevices();
    }

    internal void Navigate(int direction)
    {
        if (actions.Count > 0)
            focus = (focus + Math.Sign(direction) + actions.Count) % actions.Count;
    }

    internal void Activate()
    {
        if (focus < actions.Count)
            actions[focus]();
    }

    internal void Back()
    {
        if (Setup.Open)
        {
            Setup.Cancel();
            Page = 1;
        }
        else
            services.Leave();
        services.Bindings.DisarmMenuNavigation();
    }

    internal void Tick(double now)
    {
        Setup.Tick(now);
    }

    internal void Sample(Idas3Native.FrameInput frame, bool[] buttons, bool focused, bool suppressed)
    {
        sample = frame;
        Array.Copy(buttons, held, held.Length);
        sampleAvailable = focused && !suppressed;
    }

    private void Styles()
    {
        if (text != null)
            return;
        text = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            wordWrap = true,
            clipping = TextClipping.Clip
        };
        heading = new GUIStyle(text)
        {
            fontSize = 23,
            fontStyle = FontStyle.Bold
        };
        secondary = new GUIStyle(text)
        {
            fontSize = 16
        };
        button = new GUIStyle(GUI.skin.label)
        {
            fontSize = 17,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
            clipping = TextClipping.Clip
        };
        text.normal.textColor = heading.normal.textColor = button.normal.textColor = Color.white;
        secondary.normal.textColor = new Color32(185, 188, 195, 255);
    }

    private void Label(float x, float y, float width, float height, string value, GUIStyle style = null)
    {
        GUI.Label(new Rect(x, y, width, height), value, style ?? text);
    }

    private static void Fill(Rect rect, Color color)
    {
        Color old = GUI.color;
        GUI.color = color;
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = old;
    }

    private void Button(float x, float y, float width, string label, Action action, bool enabled = true, bool selected = false, float height = 32)
    {
        int index = actions.Count;
        if (enabled)
            actions.Add(action);
        var rect = new Rect(x, y, width, height);
        Fill(rect, selected || enabled && focus == index ? new Color32(222, 35, 49, 255) : new Color32(61, 64, 73, 255));
        Fill(new Rect(x + 1, y + 1, width - 2, height - 2), new Color32(32, 35, 42, 255));
        bool old = GUI.enabled;
        GUI.enabled = old && enabled;
        if (GUI.Button(rect, label, button))
        {
            focus = index;
            action();
        }

        GUI.enabled = old;
    }

    internal void Draw()
    {
        Styles();
        actions.Clear();
        Fill(new Rect(262, 130, 748, 413), new Color32(24, 26, 31, 255));
        Label(282, 142, 690, 35, "CONTROLS", heading);
        if (Setup.Open)
        {
            DrawSetup();
            return;
        }

        for (int i = 0; i < Pages.Length; ++i)
        {
            int page = i;
            Button(276 + i * 144, 184, 140, Pages[i], () => Select(page), page != 0 || !services.SaveIncomplete, Page == page);
        }

        if (Page == 1)
            DrawDevices();
        else if (Page == 2)
            DrawBindings();
        else if (Page == 3)
            DrawTest();
        else if (Page == 4)
            DrawFeedback();
        Label(282, 546, 710, 36, services.Message, secondary);
        Button(280, 590, 224, "DISCARD CHANGES", services.Discard);
        Button(516, 590, 224, "SAVE CHANGES", services.Save);
        Button(752, 590, 238, "BACK", services.Leave);
    }

    private void DrawSetup()
    {
        var state = Setup.State;
        if (state == Idas3SetupSession.Stage.Review)
        {
            Label(300, 218, 660, 35, "Review your setup", heading);
            var ids = new[]
            {
                Idas3ControlBindings.ActionId.SteerRight,
                Idas3ControlBindings.ActionId.Accelerate,
                Idas3ControlBindings.ActionId.Brake,
                Idas3ControlBindings.ActionId.ShiftUp,
                Idas3ControlBindings.ActionId.ShiftDown
            };
            string[] names =
            {
                "Steering",
                "Gas",
                "Brake",
                "Shift Up",
                "Shift Down"
            };
            if (Setup.DigitalSteering)
            {
                Label(300, 265, 680, 28, "Steering Left: " + services.Bindings.CompactBindingName(Idas3ControlBindings.ActionId.SteerLeft, Idas3ControlBindings.Slot.Controller));
                Label(300, 294, 680, 28, "Steering Right: " + services.Bindings.CompactBindingName(Idas3ControlBindings.ActionId.SteerRight, Idas3ControlBindings.Slot.Controller));
            }
            else
                Label(300, 270, 680, 36, "Steering: " + services.Bindings.CompactBindingName(ids[0], Idas3ControlBindings.Slot.Controller));
            for (int i = 1; i < ids.Length; ++i)
                Label(300, 290 + i * 38, 680, 36, names[i] + ": " + services.Bindings.CompactBindingName(ids[i], Idas3ControlBindings.Slot.Controller));
            Button(300, 495, 300, "SAVE", services.Save);
        }
        else
        {
            Label(300, 215, 660, 30, (Setup.Step + 1) + " / 5 · " + new[] { "Steering", "Gas", "Brake", "Shift Up", "Shift Down" }[Setup.Step], secondary);
            if (Setup.Step == 0)
            {
                Button(300, 252, 310, "Axis", () => Setup.SelectSteeringType(false, Time.realtimeSinceStartupAsDouble), true, !Setup.DigitalSteering);
                Button(630, 252, 310, "Buttons / Keys", () => Setup.SelectSteeringType(true, Time.realtimeSinceStartupAsDouble), true, Setup.DigitalSteering);
            }

            Label(300, 290, 660, 42, Setup.Instruction, heading);
            if (Setup.Step == 0 && !Setup.DigitalSteering)
                Label(300, 330, 660, 28, "Rest at center first, then move right.", secondary);
            if (state == Idas3SetupSession.Stage.Capturing)
                Label(300, 366, 660, 60, Math.Max(0, Math.Ceiling(Setup.Deadline - Time.realtimeSinceStartupAsDouble)) + " seconds remaining\n" + services.Bindings.CaptureError);
            else if (state == Idas3SetupSession.Stage.ReviewInput)
            {
                Label(300, 366, 660, 60, Setup.Detected);
                // Pointer confirmation is deliberate; hardware capture is not a click.
                Button(300, 430, 310, "CONFIRM", () =>
                {
                    Setup.Confirm(Time.realtimeSinceStartupAsDouble);
                    focus = 0;
                });
                Button(630, 430, 310, "RETRY", () => Setup.Retry(Time.realtimeSinceStartupAsDouble));
            }
            else
            {
                Label(300, 366, 660, 60, Setup.Error);
                Button(300, 430, 640, "RETRY", () => Setup.Retry(Time.realtimeSinceStartupAsDouble));
            }

            if (Setup.Step >= 3)
                Button(300, 490, 640, "KEEP EXISTING SHIFT ASSIGNMENT", () => Setup.KeepShift(Time.realtimeSinceStartupAsDouble));
        }

        Button(700, 590, 290, "CANCEL SETUP", Back);
    }

    private void DrawDevices()
    {
        Label(285, 230, 690, 30, "Connected devices", heading);
        var frame = services.Devices.Snapshot;
        if (frame != null && frame.InventoryGeneration != loggedInventory)
        {
            services.LogDevices();
            loggedInventory = frame.InventoryGeneration;
        }

        var endpoints = frame?.Endpoints;
        float total = 45;
        if (endpoints != null)
            foreach (var endpoint in endpoints)
                if (ShowDevice(endpoint))
                    total += Math.Max(50, text.CalcHeight(new GUIContent(endpoint.Name), 510) + 16);
        scroll = GUI.BeginScrollView(new Rect(280, 270, 710, 220), scroll, new Rect(0, 0, 680, total));
        Label(8, 0, 500, 35, "Keyboard");
        Label(548, 0, 120, 35, "Connected", secondary);
        float y = 42;
        if (endpoints != null)
            foreach (var endpoint in endpoints)
            {
                if (!ShowDevice(endpoint))
                    continue;
                float height = Math.Max(50, text.CalcHeight(new GUIContent(endpoint.Name), 510) + 16);
                Label(8, y, 510, height, endpoint.Name);
                Label(548, y, 120, height, endpoint.Status == Idas3EndpointStatus.Ready || endpoint.Status == Idas3EndpointStatus.PartialSample ? "Connected" : "Unavailable", secondary);
                Fill(new Rect(8, y + height - 8, 658, 1), new Color32(51, 54, 63, 255));
                y += height;
            }

        GUI.EndScrollView();
        Button(282, 500, 704, "USE SAVED CONTROLLER SETUP", services.SelectSavedController);
    }

    private static bool ShowDevice(Idas3EndpointSnapshot endpoint) => endpoint.Status != Idas3EndpointStatus.Disconnected && endpoint.Status != Idas3EndpointStatus.Disposed && endpoint.Status != Idas3EndpointStatus.SuppressedMirror;
    private void DrawBindings()
    {
        string[] groups =
        {
            "DRIVING",
            "MENU",
            "KEYBOARD"
        };
        for (int i = 0; i < groups.Length; ++i)
        {
            int group = i;
            Button(282 + i * 238, 230, 228, groups[i], () =>
            {
                bindingGroup = group;
                focus = 7;
            }, true, bindingGroup == group);
        }

        var bindings = services.Bindings;
        // All rows live in the fixed content panel. No binding group scrolls.
        if (bindingGroup == 1)
        {
            for (int i = 0; i < Idas3ControlBindings.MenuActionCount; ++i)
            {
                var action = (Idas3ControlBindings.MenuActionId)i;
                float y = 274 + i * 28;
                Label(286, y + 2, 240, 26, Idas3ControlBindings.MenuActionNames[i], secondary);
                Button(532, y, 450, bindings.CompactMenuBindingName(action), () => services.RebindMenu(action), height: 26);
            }

            Button(282, 505, 704, "AXIS ACTIVATION: " + Mathf.RoundToInt(bindings.MenuActivationPoint * 100) + "%", () => bindings.SetMenuActivation(bindings.MenuActivationPoint >= .95f ? .45f : bindings.MenuActivationPoint + .05f));
            return;
        }

        if (bindingGroup == 2)
        {
            Button(282, 270, 345, "KEYBOARD SLOT " + (keyboardSlot + 1) + "  ›", () => keyboardSlot = (keyboardSlot + 1) % 3, height: 26);
            Label(641, 270, 345, 26, "Menu / recovery keys", secondary);
        }

        for (int i = 0; i < 10; ++i)
        {
            var action = (Idas3ControlBindings.ActionId)i;
            float y = (bindingGroup == 2 ? 304 : 272) + i * 23;
            Label(286, y, bindingGroup == 2 ? 140 : 240, 23, DrivingName(action), secondary);
            if (bindingGroup == 2)
                Button(432, y, 195, bindings.CompactBindingName(action, (Idas3ControlBindings.Slot)keyboardSlot), () => services.Rebind(action, (Idas3ControlBindings.Slot)keyboardSlot), height: 22);
            else
                Button(532, y, 450, ControllerAssignment(action), () => services.Rebind(action, Idas3ControlBindings.Slot.Controller), height: 22);
        }

        if (bindingGroup == 2)
            for (int i = 0; i < Idas3ControlBindings.MenuActionCount; ++i)
            {
                var action = (Idas3ControlBindings.MenuActionId)i;
                float y = 304 + i * 28;
                Label(641, y, 145, 28, (action == Idas3ControlBindings.MenuActionId.Pause ? "Open Options" : Idas3ControlBindings.MenuActionNames[i]), secondary);
                Button(788, y, 194, bindings.CompactMenuKeyboardName(action), () => services.RebindMenu(action), height: 26);
            }
    }

    private static string DrivingName(Idas3ControlBindings.ActionId action)
    {
        if (action == Idas3ControlBindings.ActionId.Accelerate)
            return "Gas";
        if (action == Idas3ControlBindings.ActionId.Headlights)
            return "Headlights";
        return Idas3ControlBindings.ActionName(action);
    }

    private string ControllerAssignment(Idas3ControlBindings.ActionId action)
    {
        string assignment = services.Bindings.CompactBindingName(action, Idas3ControlBindings.Slot.Controller);
        return Idas3ControlBindings.ShortControlText(services.Bindings.ExperimentalDraftEnabled ? assignment : assignment + " · " + Idas3ControlBindings.ShortControlText(services.Devices.ActiveName, 14), 48);
    }

    private static bool Key(Idas3Native.FrameInput frame, int key)
    {
        uint word = key < 32 ? frame.key0 : key < 64 ? frame.key1 : key < 96 ? frame.key2 : frame.key3;
        return (word & (1u << (key & 31))) != 0;
    }

    private void DrawTest()
    {
        Label(286, 233, 695, 32, sampleAvailable ? "Live input" : "Release controls / return focus", heading);
        float steer = sample.thumbLX / (sample.thumbLX < 0 ? 32768f : 32767f);
        if (Key(sample, 65) || Key(sample, 68))
            steer = (Key(sample, 68) ? 1 : 0) - (Key(sample, 65) ? 1 : 0);
        Bar(290, "Wheel / Stick", sampleAvailable ? steer : 0, true);
        Bar(345, "Gas", sampleAvailable ? (Key(sample, 87) ? 1 : sample.rightTrigger / 255f) : 0, false);
        Bar(400, "Brake", sampleAvailable ? (Key(sample, 83) ? 1 : sample.leftTrigger / 255f) : 0, false);
        Label(286, 464, 340, 35, "Shift Up: " + (sampleAvailable && held[4] ? "ON" : "OFF"));
        Label(640, 464, 340, 35, "Shift Down: " + (sampleAvailable && held[5] ? "ON" : "OFF"));
        Label(286, 503, 695, 39, "Assigned input before native response. Pointer / Escape to leave.", secondary);
    }

    private void Bar(float y, string name, float amount, bool centered)
    {
        Label(286, y, 180, 30, name);
        GUI.Box(new Rect(480, y, 500, 28), GUIContent.none);
        float position = centered ? (amount + 1) * .5f : amount;
        Color old = GUI.color;
        GUI.color = new Color(.87f, .14f, .19f);
        GUI.DrawTexture(centered ? new Rect(480 + 496 * position, y, 4, 28) : new Rect(480, y, 500 * position, 28), Texture2D.whiteTexture);
        GUI.color = old;
        if (centered)
            GUI.DrawTexture(new Rect(730, y, 1, 28), Texture2D.whiteTexture);
    }

    private void DrawFeedback()
    {
        var values = services.Options.Draft;
        string output = "Unavailable";
        if (services.Feedback != null)
            foreach (var device in services.Feedback.Choices)
                if (device.id == values.wheelFeedbackDevice)
                    output = device.name;
        string[] labels =
        {
            "Enable",
            "Force-output device",
            "Strength",
            "Invert"
        };
        string[] valuesText =
        {
            values.wheelForceFeedback ? "ON" : "OFF",
            output,
            Mathf.RoundToInt(values.wheelFeedbackStrength * 100) + "%",
            values.wheelFeedbackInvert ? "ON" : "OFF"
        };
        for (int i = 0; i < 4; ++i)
        {
            int row = i;
            Label(285, 243 + i * 50, 210, 42, labels[i]);
            Button(500, 240 + i * 50, 35, "‹", () => services.AdjustFeedback(row, -1));
            Label(543, 241 + i * 50, 394, 44, valuesText[i], secondary);
            Button(949, 240 + i * 50, 35, "›", () => services.AdjustFeedback(row, 1));
        }

        Label(285, 454, 695, 80, FeedbackStatus(), secondary);
    }

    private string FeedbackStatus()
    {
        bool pending = Idas3ControllerStatus.FeedbackPending(services.Options.Current, services.Options.Draft);
        string output = services.Bindings.ExperimentalBlocksFeedback ? "Force output is unavailable for this wheel setup. Your feedback settings are kept." : services.Feedback?.StatusText ?? "Force output is unavailable.";
        return (pending ? "Unsaved changes. " : "") + output + " Feedback is paused while settings are open.";
    }
}
