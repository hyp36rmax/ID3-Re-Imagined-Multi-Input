using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using B = Idas3ControlBindings;

internal static class CaptureFollowupChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string why) { ++checks; if (!ok) throw new Exception(why); }
        var device = new Joystick { deviceId = 9100, path = "/capture", description = new Description { product = "Driver wheel", interfaceName = "HID" } };
        AxisControl Add(string path, bool button = false)
        {
            AxisControl control = button ? new ButtonControl() : new AxisControl();
            control.path = device.path + "/" + path;
            control.name = path.Substring(path.LastIndexOf('/') + 1);
            device.allControls.Add(control);
            return control;
        }
        var axis = Add("steering");
        var pedal = Add("pedal"); pedal.value = 1;
        var hat = new DpadControl();
        var directions = new[] { "up", "down", "left", "right" };
        var hats = directions.Select(d => Add("hat/" + d, true)).ToArray();
        foreach (var control in hats) control.parent = hat;
        InputSystem.Add(device);
        double now = 0;
        KeyCode key = KeyCode.None;
        using var provider = new Idas3ControllerDevices(() => now, (uint slot, out Idas3Native.PadState value) => { value = default; return 1167; }, d => d == device);
        try
        {
            provider.Initialize(root);
            var b = new B(); b.Initialize(root); b.ApplyDraft();
            string original = File.ReadAllText(b.FilePath);
            void Poll() { now += .02; provider.Tick(false); b.Poll(k => k == key, default, now, provider.Controls, provider.Snapshot); }
            Poll();
            b.BeginCapture(B.ActionId.Camera, B.Slot.Primary, now); Poll(); key = KeyCode.E; Poll();
            var before = b.Draft.Clone();
            Check(b.ConflictPending && b.CaptureConflictMessage == "Already assigned to Shift up. Replace this assignment?", "keyboard duplicate proposes explicit replacement");
            b.ResolveCaptureConflict(false);
            Check(B.Equivalent(before, b.Draft), "Cancel keeps both keyboard assignments");
            key = KeyCode.None; Poll(); b.BeginCapture(B.ActionId.Camera, B.Slot.Primary, now); Poll(); key = KeyCode.E; Poll();
            b.ResolveCaptureConflict(true);
            Check(b.Draft.actions[6].key1 == KeyCode.E && b.Draft.actions[4].key1 == KeyCode.None, "Replace clears only conflicting keyboard slot");
            Check(File.ReadAllText(b.FilePath) == original, "replacement remains unsaved");
            b.CancelEdit(false); key = KeyCode.None; Poll();
            b.BeginCapture(B.ActionId.Camera, B.Slot.Controller, now);
            Check(!b.TrySetDraftPad(B.ActionId.Camera, B.PadInput.B) && b.ConflictPending, "legacy pad conflict uses the same confirmation contract");
            b.ResolveCaptureConflict(false);
            Check(b.Draft.actions[4].pad == B.PadInput.B && b.Draft.actions[6].pad == B.PadInput.Y, "legacy pad Cancel leaves both assignments");
            Poll(); b.BeginCapture(B.ActionId.Camera, B.Slot.Controller, now);
            b.TrySetDraftPad(B.ActionId.Camera, B.PadInput.B); b.ResolveCaptureConflict(true);
            Check(b.Draft.actions[4].pad == B.PadInput.None && b.Draft.actions[6].pad == B.PadInput.B, "legacy pad Replace clears only the confirmed conflict");
            b.CancelEdit(false); key = KeyCode.None; Poll(); b.SetExperimentalDraftEnabled(true); Poll();
            var endpoint = provider.Snapshot.Endpoints.Single(e => e.CanRead);
            Check(b.TrySetExperimentalControl(B.ActionId.SteerRight, endpoint.Token, endpoint.ConnectionGeneration, "steering", 1, 0), "driving steering fixture");
            foreach (var direction in new[] { -1, 1 })
            {
                axis.value = 0; Poll();
                var action = direction < 0 ? B.MenuActionId.Left : B.MenuActionId.Right;
                b.BeginMenuCapture(action, now); Poll(); axis.value = .6f * direction; Poll();
                Check(!b.IsCapturing && !b.ConflictPending, "opposite wheel direction captures in menu and reuses driving axis");
            }
            axis.value = 0; Poll(); b.EvaluateMenuNavigation(2, true, true);
            axis.value = .44f; Poll(); b.EvaluateMenuNavigation(2, true, true);
            Check(!b.MenuEvent(B.MenuActionId.Right), "menu remains below 75 percent of captured range");
            axis.value = .46f; Poll(); b.EvaluateMenuNavigation(2, true, true);
            Check(b.MenuEvent(B.MenuActionId.Right), "menu activates at 75 percent calibrated range");
            Poll(); b.EvaluateMenuNavigation(2, true, true); Check(b.MenuEvents == 0, "held wheel never repeats");
            b.BeginMenuCapture(B.MenuActionId.Left, now); Poll(); axis.value = 0; Poll();
            Check(b.IsCapturing, "opening axis release is not captured as opposite movement");
            axis.value = -.6f; Poll(); Check(!b.IsCapturing, "fresh excursion after opening-axis release captures");
            axis.value = 0; Poll();
            b.BeginMenuCapture(B.MenuActionId.Confirm, now); Poll(); pedal.value = -.6f; Poll();
            Check(!b.IsCapturing, "positive-rest pedal captures deliberate menu movement");
            pedal.value = 1; Poll();
            for (int i = 0; i < hats.Length; ++i)
            {
                b.BeginMenuCapture(B.MenuActionId.Up, now); Poll(); hats[i].value = 1; Poll();
                Check(!b.IsCapturing && b.MenuBindingName(B.MenuActionId.Up).Contains("POV " + char.ToUpperInvariant(directions[i][0]) + directions[i].Substring(1)), "hat direction capture/label follows decoded child " + directions[i]);
                hats[i].value = 0; Poll(); b.ClearMenuAssignment(B.MenuActionId.Up);
            }
            b.BeginMenuCapture(B.MenuActionId.Up, now); Poll(); hats[0].value = hats[2].value = 1; Poll();
            Check(b.IsCapturing && b.CaptureError.Contains("ambiguous"), "diagonal cannot select arbitrary enumerated direction");
            hats[2].value = 0; Poll(); Check(b.IsCapturing, "diagonal settling cannot capture a stale remaining direction");
            hats[0].value = 0; Poll(); hats[0].value = 1; Poll();
            Check(!b.IsCapturing && b.MenuBindingName(B.MenuActionId.Up).Contains("POV Up"), "neutral then cardinal captures correct direction");
            hats[0].value = 0; Poll(); b.BeginMenuCapture(B.MenuActionId.Down, now); Poll(); hats[0].value = 1; Poll();
            Check(b.ConflictPending, "same-context POV conflict proposes replacement");
            b.ResolveCaptureConflict(true); hats[0].value = 0; Poll();
            Check(b.MenuBindingName(B.MenuActionId.Up) == "Unassigned" && b.MenuBindingName(B.MenuActionId.Down).Contains("POV Up"), "menu Replace transfers only confirmed source");
            b.BeginCapture(B.ActionId.Brake, B.Slot.Controller, now); Poll(); key = KeyCode.W; Poll();
            Check(b.ConflictPending, "experimental keyboard reports original active-context conflict");
            b.ResolveCaptureConflict(true); key = KeyCode.None; Poll(); key = KeyCode.W; Poll();
            var evaluated = b.EvaluateDraftDriving();
            Check((evaluated.key2 & (1u << (87 & 31))) == 0 && evaluated.leftTrigger == 255, "experimental keyboard ownership suppresses original action without duplicate input");
            Check(File.ReadAllText(b.FilePath) == original, "new keyboard assignment preserves original file");
            key = KeyCode.None; Poll();
            string beforeLostSource = JsonUtility.ToJson(b.SaveDraftCheckpoint().experimental);
            b.BeginMenuCapture(B.MenuActionId.Right, now); Poll(); hats[0].value = 1; Poll();
            Check(b.ConflictPending, "source-loss fixture has a pending confirmed-transfer proposal");
            InputSystem.Remove(device); Poll(); b.ResolveCaptureConflict(true);
            Check(!b.IsCapturing && JsonUtility.ToJson(b.SaveDraftCheckpoint().experimental) == beforeLostSource,
                "failed Replace after disconnect restores both assignments atomically");
            checks += NativeHatChecks(Path.Combine(root, "native"));
            return checks;
        }
        finally { if (device.added) InputSystem.Remove(device); }
    }
    private static int NativeHatChecks(string root)
    {
        int checks = 0;
        void Check(bool ok, string why) { ++checks; if (!ok) throw new Exception(why); }
        ushort buttons = 0;
        double now = 0;
        using var provider = new Idas3ControllerDevices(() => now, (uint slot, out Idas3Native.PadState state) =>
        {
            state = new Idas3Native.PadState { gamepad = new Idas3Native.GamepadState { buttons = buttons } };
            return slot == 0 ? 0u : 1167u;
        }, _ => false);
        provider.Initialize(root);
        var b = new B(); b.Initialize(root); b.SetExperimentalDraftEnabled(true);
        void Poll() { now += .02; provider.Tick(false); b.Poll(_ => false, default, now, provider.Controls, provider.Snapshot); }
        var names = new[] { "up", "down", "left", "right" };
        for (int i = 0; i < 4; ++i)
        {
            buttons = 0; Poll(); b.BeginMenuCapture(B.MenuActionId.Up, now); Poll();
            buttons = (ushort)(1 << i); Poll();
            Check(provider.TryRead(out var raw) && raw.buttons == buttons, "native POV mask reaches active pad unchanged");
            Check(!b.IsCapturing && b.MenuBindingName(B.MenuActionId.Up).Contains("D-pad " + names[i]), "native POV capture uses matching stored direction label");
            buttons = 0; Poll(); b.EvaluateMenuNavigation(2, true, true);
            buttons = (ushort)(1 << i); Poll(); b.EvaluateMenuNavigation(2, true, true);
            Check(b.MenuEvent(B.MenuActionId.Up), "native stored direction evaluates assigned menu action");
            Poll(); b.EvaluateMenuNavigation(2, true, true);
            Check(b.MenuEvents == 0, "native held POV emits once");
            buttons = 0; Poll(); b.ClearMenuAssignment(B.MenuActionId.Up);
        }
        foreach (ushort diagonal in new ushort[] { 5, 9, 6, 10 })
        {
            buttons = 0; Poll(); b.BeginMenuCapture(B.MenuActionId.Up, now); Poll();
            buttons = diagonal; Poll(); Check(b.IsCapturing, "native diagonal cannot silently pick a cardinal");
            b.CancelCapture(); buttons = 0; Poll();
        }
        return checks;
    }

}
