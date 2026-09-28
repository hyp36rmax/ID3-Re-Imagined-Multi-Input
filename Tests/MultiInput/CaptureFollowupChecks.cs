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
            return checks;
        }
        finally { InputSystem.Remove(device); }
    }
}
