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

    private bool menuBindings;
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
            Setup.OpenChoice();
        if (Page == 4)
            services.Feedback?.RefreshDevices();
    }

    internal void Navigate(int direction)
    {
        if (actions.Count > 0)
            focus = (focus + Math.Sign(direction) + actions.Count) % actions.Count;
        if (Page == 2 && !menuBindings && focus >= 8 && focus < 28)
            scroll.y = Mathf.Clamp(((focus - 8) / 2) * 36 - 90, 0, 133);
        if (Page == 2 && menuBindings && focus >= 7 && focus < 15)
            scroll.y = Mathf.Clamp((focus - 7) * 36 - 90, 0, 68);
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
            fontSize = 13
        };
        button = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
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

    private void Button(float x, float y, float width, string label, Action action, bool enabled = true, bool selected = false)
    {
        int index = actions.Count;
        if (enabled)
            actions.Add(action);
        var rect = new Rect(x, y, width, 32);
        Fill(rect, selected || enabled && focus == index ? new Color32(222, 35, 49, 255) : new Color32(61, 64, 73, 255));
        Fill(new Rect(x + 1, y + 1, width - 2, 30), new Color32(32, 35, 42, 255));
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
        if (state == Idas3SetupSession.Stage.Choose)
        {
            Label(300, 220, 660, 45, "What are you setting up?", heading);
            Button(300, 300, 310, "WHEEL + PEDALS", () => Setup.Begin(false, Time.realtimeSinceStartupAsDouble), services.Bindings.CanCaptureExperimental);
            Button(630, 300, 310, "GAMEPAD", () => Setup.Begin(true, Time.realtimeSinceStartupAsDouble), services.Bindings.CanCaptureExperimental);
            Label(300, 350, 660, 60, services.Bindings.CanCaptureExperimental ? "Rest the controls before selecting. Your saved setup is kept until you save." : "Connect a supported controller. Keyboard bindings are available in Bindings.");
        }
        else if (state == Idas3SetupSession.Stage.Review)
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
            for (int i = 0; i < ids.Length; ++i)
                Label(300, 270 + i * 40, 680, 36, names[i] + ": " + services.Bindings.CompactBindingName(ids[i], Idas3ControlBindings.Slot.Controller));
            Button(300, 495, 300, "SAVE", services.Save);
        }
        else
        {
            Label(300, 215, 660, 30, (Setup.Step + 1) + " / 5", secondary);
            Label(300, 260, 660, 65, Setup.Instruction, heading);
            if (state == Idas3SetupSession.Stage.Capturing)
                Label(300, 340, 660, 60, Math.Max(0, Math.Ceiling(Setup.Deadline - Time.realtimeSinceStartupAsDouble)) + " seconds remaining\n" + services.Bindings.CaptureError);
            else if (state == Idas3SetupSession.Stage.ReviewInput)
            {
                Label(300, 340, 660, 65, Setup.Detected);
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
                Label(300, 340, 660, 65, Setup.Error);
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
        Button(282, 230, 345, "DRIVING", () =>
        {
            menuBindings = false;
            focus = 5;
            scroll = Vector2.zero;
        });
        Button(641, 230, 345, "MENU", () =>
        {
            menuBindings = true;
            focus = 6;
            scroll = Vector2.zero;
        });
        var bindings = services.Bindings;
        if (menuBindings)
        {
            scroll = GUI.BeginScrollView(new Rect(280, 269, 714, 220), scroll, new Rect(0, 0, 688, 288));
            for (int i = 0; i < Idas3ControlBindings.MenuActionCount; ++i)
            {
                var action = (Idas3ControlBindings.MenuActionId)i;
                Label(4, 4 + i * 36, 205, 29, Idas3ControlBindings.MenuActionNames[i], secondary);
                Button(211, i * 36, 474, bindings.CompactMenuBindingName(action), () => services.RebindMenu(action));
            }

            GUI.EndScrollView();
            Button(282, 501, 704, "AXIS ACTIVATION: " + Mathf.RoundToInt(bindings.MenuActivationPoint * 100) + "%", () => bindings.SetMenuActivation(bindings.MenuActivationPoint >= .95f ? .45f : bindings.MenuActivationPoint + .05f));
        }
        else
        {
            Button(282, 268, 340, "KEYBOARD SLOT " + (keyboardSlot + 1) + "  ›", () => keyboardSlot = (keyboardSlot + 1) % 3);
            Label(640, 273, 345, 28, "Controller input", secondary);
            scroll = GUI.BeginScrollView(new Rect(280, 307, 714, 227), scroll, new Rect(0, 0, 688, 360));
            for (int i = 0; i < 10; ++i)
            {
                var action = (Idas3ControlBindings.ActionId)i;
                Label(4, i * 36 + 4, 155, 30, Idas3ControlBindings.ActionName(action), secondary);
                Button(160, i * 36, 130, bindings.CompactBindingName(action, (Idas3ControlBindings.Slot)keyboardSlot), () => services.Rebind(action, (Idas3ControlBindings.Slot)keyboardSlot));
                Button(298, i * 36, 385, ControllerAssignment(action), () => services.Rebind(action, Idas3ControlBindings.Slot.Controller));
            }

            GUI.EndScrollView();
        }
    }

    private string ControllerAssignment(Idas3ControlBindings.ActionId action)
    {
        string assignment = services.Bindings.CompactBindingName(action, Idas3ControlBindings.Slot.Controller);
        return services.Bindings.ExperimentalDraftEnabled ? assignment : assignment + " · " + Idas3ControlBindings.ShortControlText(services.Devices.ActiveName, 14);
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
        Label(286, 510, 695, 28, "Assigned input before native response. Use the pointer or keyboard Escape to leave.", secondary);
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
