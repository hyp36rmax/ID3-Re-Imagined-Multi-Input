using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

internal static class OtherInputChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool ok, string why) { ++checks; if (!ok) throw new Exception(why); }
        var device = new Joystick { deviceId = 9200, path = "/observed", description = new Description { product = "A long unchanged driver supplied wheel name", interfaceName = "HID" } };
        var axis = new AxisControl { path = device.path + "/wheel", displayName = "Steering" };
        var pedal = new AxisControl { path = device.path + "/pedal", displayName = "Unassigned pedal", value = 1 };
        var button = new ButtonControl { path = device.path + "/button5", displayName = "Button 5" };
        device.allControls.Add(axis); device.allControls.Add(pedal); device.allControls.Add(button);
        InputSystem.Add(device);
        double now = 0;
        using var provider = new Idas3ControllerDevices(() => now, (uint slot, out Idas3Native.PadState value) => { value = default; return 1167; }, d => d == device);
        try
        {
            provider.Initialize(root);
            var bindings = new Idas3ControlBindings(); bindings.Initialize(root); bindings.ApplyDraft();
            string saved = File.ReadAllText(bindings.FilePath);
            var view = new Idas3OtherInputs();
            KeyCode key = KeyCode.None;
            void Poll(double elapsed = .02)
            {
                now += elapsed; provider.Tick(false);
                bindings.Poll(k => k == key, default, now, provider.Controls, provider.Snapshot);
                view.Update(provider.Snapshot, bindings, now, true);
            }
            Poll();
            Check(view.Visible.Count == 0, "resting centered axis and end-rest pedal are absent");
            axis.value = .02f; pedal.value = .99f; Poll();
            Check(view.Visible.Count == 0, "analog noise does not flood Other Inputs");
            bindings.SetExperimentalDraftEnabled(true);
            var endpoint = provider.Snapshot.Endpoints.Single(e => e.CanRead);
            Check(bindings.TrySetExperimentalControl(Idas3ControlBindings.ActionId.SteerRight, endpoint.Token, endpoint.ConnectionGeneration, "wheel", 1, 0), "assigned input display fixture");
            axis.value = .7f; pedal.value = -.7f; button.value = 1; key = KeyCode.Return; Poll();
            Check(view.Visible.Count == 4 && view.Visible.All(e => e.Active), "assigned and unassigned axes, button and Enter show simultaneously");
            Check(view.Visible.Any(e => e.Name.Contains("unchanged driver supplied") && e.Display.Contains("Button 5")), "full driver name retained and shortened display retains control name");
            Check(view.Visible.Any(e => e.Name == "Keyboard · Enter"), "reserved keyboard input is observable without dispatch");
            axis.value = 0; pedal.value = 1; button.value = 0; key = KeyCode.None; Poll();
            Check(view.Visible.Count == 4 && view.Visible.All(e => !e.Active), "release is briefly visible together");
            Poll(.7); Check(view.Visible.Count == 0, "inactive rows expire instead of accumulating");
            button.value = 1; Poll(); view.Update(provider.Snapshot, bindings, now + 1, true);
            Check(view.Visible.Count == 0, "stale device samples cannot appear active");
            Poll(); view.Update(provider.Snapshot, bindings, now, false);
            Check(view.Visible.Count == 0, "focus loss clears the display");
            Poll(); InputSystem.Remove(device); view.Update(provider.Snapshot, bindings, now, true);
            Check(view.Visible.Count == 0, "disconnect immediately removes activity");
            InputSystem.Add(device); button.value = 0; Poll();
            Check(view.Visible.Count == 0, "reconnect generation does not retain stale activity");
            Check(File.ReadAllText(bindings.FilePath) == saved, "test display does not write bindings");
            return checks;
        }
        finally { if (device.added) InputSystem.Remove(device); }
    }
}
