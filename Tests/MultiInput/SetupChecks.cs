using System;
using System.IO;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using B = Idas3ControlBindings;

internal static class SetupChecks
{
    internal static int Run(string root)
    {
        int count = 0;
        void Check(bool value, string why)
        {
            ++count;
            if (!value)
                throw new Exception(why);
        }

        var device = new Joystick
        {
            deviceId = 7200,
            path = "/setup",
            description = new Description
            {
                interfaceName = "HID",
                product = "A very long driver supplied controller name which must remain unchanged"
            }
        };
        AxisControl Add(string name, bool button = false)
        {
            AxisControl control = button ? new ButtonControl() : new AxisControl();
            control.path = device.path + "/" + name;
            device.allControls.Add(control);
            return control;
        }

        var wheel = Add("wheel");
        var gas = Add("gas");
        gas.value = 1;
        var brake = Add("brake");
        brake.value = -1;
        var up = Add("up", true);
        var down = Add("down", true);
        InputSystem.Add(device);
        double now = 0;
        using var provider = new Idas3ControllerDevices(() => now, (uint slot, out Idas3Native.PadState state) =>
        {
            state = default;
            return 1167;
        }, d => d == device);
        try
        {
            provider.Initialize(root);
            var bindings = new B();
            bindings.Initialize(root);
            bindings.ApplyDraft();
            string saved = File.ReadAllText(bindings.FilePath);
            var setup = new Idas3SetupSession(bindings);
            void Poll(double advance = .02)
            {
                now += advance;
                provider.Tick(false);
                bindings.Poll(_ => false, default, now, provider.Controls, provider.Snapshot);
                setup.Tick(now);
            }

            Poll();
            setup.OpenChoice();
            setup.Begin(false, now);
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.Capturing && setup.Deadline == now - .02 + 6, "wizard starts six-second capture immediately after type choice");
            wheel.value = .7f;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.ReviewInput && setup.Step == 0, "capture does not advance or confirm itself");
            Check(bindings.HasControllerAssignment(B.ActionId.SteerLeft) && bindings.HasControllerAssignment(B.ActionId.SteerRight), "one centered axis derives both steering directions");
            Check(!bindings.ExperimentalEnabled && File.ReadAllText(bindings.FilePath) == saved, "review never activates or saves tentative setup");
            wheel.value = 0;
            Poll();
            setup.Retry(now);
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.Capturing && !bindings.HasControllerAssignment(B.ActionId.SteerRight), "retry restores pre-step state before fresh capture");
            Poll(6.01);
            Check(setup.State == Idas3SetupSession.Stage.TimedOut && !bindings.HasControllerAssignment(B.ActionId.SteerRight), "timeout restores prior assignment");
            setup.Retry(now);
            Poll();
            wheel.value = -.8f;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.ReviewInput, "inverted axis accepted from explicit rightward prompt");
            wheel.value = 0;
            Poll();
            setup.Confirm(now);
            Poll();
            gas.value = -1;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.ReviewInput && setup.Step == 1, "positive-rest pedal captured without requiring center");
            gas.value = 1;
            Poll();
            setup.Confirm(now);
            Poll();
            brake.value = 1;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.ReviewInput && setup.Step == 2, "negative-rest pedal captured");
            brake.value = -1;
            Poll();
            setup.Confirm(now);
            Poll();
            up.value = 1;
            Poll();
            Check(setup.Step == 3 && setup.State == Idas3SetupSession.Stage.ReviewInput, "shift press awaits explicit confirmation");
            up.value = 0;
            Poll();
            setup.Confirm(now);
            Poll();
            down.value = 1;
            Poll();
            down.value = 0;
            Poll();
            setup.Confirm(now);
            Check(setup.Complete && !bindings.ExperimentalEnabled, "all five confirmed steps reach review without save");
            setup.Cancel();
            Check(!bindings.ExperimentalDraftEnabled && File.ReadAllText(bindings.FilePath) == saved && !bindings.HasExperimentalChanges, "cancel from final review restores prior configuration and dirty state");
            setup.OpenChoice();
            setup.Begin(true, now);
            Poll();
            Check(setup.Gamepad && setup.Instruction.Contains("stick"), "gamepad path uses stick instruction");
            setup.Cancel();
            Poll();
            // A button cannot be converted to both steering directions.
            setup.OpenChoice();
            setup.Begin(false, now);
            Poll();
            up.value = 1;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.TimedOut && !bindings.HasControllerAssignment(B.ActionId.SteerRight), "steering button rejected and tentative capture rolled back");
            up.value = 0;
            setup.Cancel();
            Poll();
            // Original profile can gain explicit menu assignments without switching driving.
            bindings.BeginMenuCapture(B.MenuActionId.Pause, now);
            Poll();
            down.value = 1;
            Poll();
            Check(!bindings.IsCapturing && !bindings.ExperimentalDraftEnabled && bindings.MenuBindingName(B.MenuActionId.Pause).Contains("driver supplied"), "menu capture works with original driving profile");
            Check(bindings.ApplyExperimentalDraft() && !bindings.ExperimentalEnabled, "menu-only save preserves original driving ownership");
            down.value = 0;
            Poll();
            bindings.EvaluateMenuNavigation(100, true, false);
            down.value = 1;
            Poll();
            bindings.EvaluateMenuNavigation(100, true, false);
            Check(bindings.MenuEvent(B.MenuActionId.Pause) && bindings.RawPauseHeld, "Open Options event available on original driving setup");
            var menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 0x1000,
                padConnected = 1
            };
            bindings.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.padButtons == 0 && menuFrame.key0 == 0, "explicit Options suppresses overlapping legacy Confirm packet");
            down.value = 0;
            Poll();
            bindings.EvaluateMenuNavigation(100, true, false);
            menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 0x1000,
                padConnected = 1
            };
            bindings.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.padButtons == 0x1000, "original gamepad Confirm remains available outside explicit excursions");
            bindings.BeginMenuCapture(B.MenuActionId.Start, now);
            Poll();
            up.value = 1;
            Poll();
            Check(bindings.ApplyExperimentalDraft(), "Start saves independently from Options");
            up.value = 0;
            Poll();
            bindings.EvaluateMenuNavigation(100, true, false);
            up.value = 1;
            Poll();
            bindings.EvaluateMenuNavigation(100, true, false);
            menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 0x1000,
                padConnected = 1
            };
            bindings.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.key0 == (1u << 13) && menuFrame.padButtons == 0 && !bindings.RawPauseHeld, "Start delivers one frontend confirm with no legacy alias or Pause");
            bindings.Poll(_ => false, new B.PadState { connected = true, buttons = 0x8020 }, now, provider.Controls, provider.Snapshot);
            Check(!bindings.ViewChangeHeld && !bindings.RawOnlineHeld, "explicit Start suppresses legacy View Change/Online shortcut aliases");
            up.value = 0;
            Poll();
            bindings.EvaluateMenuNavigation(2, true, false);
            up.value = 1;
            Poll();
            bindings.EvaluateMenuNavigation(2, true, false);
            menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 0x1000,
                padConnected = 1
            };
            bindings.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.key0 == 0 && menuFrame.padButtons == 0 && !bindings.RawPauseHeld, "Start is not settings Confirm or Pause");
            var legacy = new B();
            legacy.Initialize(Path.Combine(root, "legacy-navigation"));
            legacy.EvaluateMenuNavigation(2, true, false);
            menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 2
            };
            legacy.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.padButtons == 0 && menuFrame.key1 == 0, "legacy POV held at entry is consumed");
            menuFrame = default;
            legacy.ApplyMenu(ref menuFrame, false);
            menuFrame.padButtons = 2;
            legacy.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.padButtons == 0 && menuFrame.key1 == (1u << 8), "legacy POV emits one canonical Down event without a second pad alias");
            menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 2
            };
            legacy.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.key1 == 0, "legacy held POV cannot repeat through the host handler");
            legacy.EvaluateMenuNavigation(2, false, false);
            legacy.EvaluateMenuNavigation(2, true, false);
            menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 2
            };
            legacy.ApplyMenu(ref menuFrame, false);
            Check(menuFrame.key1 == 0, "legacy focus regain still waits for physical release");
            var gate = new Idas3MenuExcursion();
            Check(gate.Evaluate(1) == 0 && gate.Evaluate(1) == 0, "held POV at entry blocked");
            gate.Evaluate(0);
            Check(gate.Evaluate(1) == 1 && gate.Evaluate(1) == 0 && gate.Evaluate(2) == 0, "POV press emits once; hold and direction change cannot repeat");
            gate.Evaluate(0);
            Check(gate.Evaluate(8) == 8, "neutral release permits next direction");
            gate.Reset();
            Check(gate.Evaluate(8) == 0, "focus/reconnect reset requires neutral");
            gate.Evaluate(0);
            Check(gate.Evaluate(9) == 1 && gate.Evaluate(8) == 0, "diagonal consumes one excursion without duplicate horizontal event");
            Check(B.ShortControlText(device.description.product, 22).Length == 22 && device.description.product == "A very long driver supplied controller name which must remain unchanged", "compact labels do not mutate driver names");
            count += ControlsViewChecks.Run(bindings, provider, Path.Combine(root, "view-options"));
            Check(File.ReadAllText(bindings.FilePath) == saved, "all setup and menu operations preserve original file bytes");
        }
        finally
        {
            InputSystem.Remove(device);
        }

        return count;
    }
}
