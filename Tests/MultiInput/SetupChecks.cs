using System;
using System.IO;
using System.Linq;
using UnityEngine;
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
        var shifter = new Joystick
        {
            deviceId = 7201,
            path = "/setup-shifter",
            description = new Description
            {
                interfaceName = "HID",
                product = "Setup shifter"
            }
        };
        var up = new ButtonControl
        {
            path = shifter.path + "/up"
        };
        var down = new ButtonControl
        {
            path = shifter.path + "/down"
        };
        shifter.allControls.Add(up);
        shifter.allControls.Add(down);
        InputSystem.Add(shifter);
        InputSystem.Add(device);
        double now = 0;
        using var provider = new Idas3ControllerDevices(() => now, (uint slot, out Idas3Native.PadState state) =>
        {
            state = default;
            return 1167;
        }, d => d == device || d == shifter);
        try
        {
            provider.Initialize(root);
            var bindings = new B();
            bindings.Initialize(root);
            bindings.ApplyDraft();
            string saved = File.ReadAllText(bindings.FilePath);
            var setup = new Idas3SetupSession(bindings);
            KeyCode pressedKey = KeyCode.None;
            void Poll(double advance = .02)
            {
                now += advance;
                provider.Tick(false);
                bindings.Poll(k => k == pressedKey, default, now, provider.Controls, provider.Snapshot);
                setup.Tick(now);
            }

            Poll();
            setup.Begin(now);
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
            setup.Begin(now);
            Poll();
            Check(!setup.DigitalSteering && setup.Instruction.Contains("stick"), "gamepad path uses stick instruction");
            setup.Cancel();
            Poll();
            // A button cannot be converted to both steering directions.
            setup.Begin(now);
            Poll();
            up.value = 1;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.Capturing && !bindings.HasControllerAssignment(B.ActionId.SteerRight), "steering button ignored and tentative capture rolled back");
            up.value = 0;
            setup.Cancel();
            Poll();
            // The same wizard accepts keys and multiple physical devices without a type menu.
            setup.Begin(now);
            setup.SelectSteeringType(true, now);
            Poll();
            Check(setup.Step == 0 && setup.Instruction == "Press Left.", "digital steering starts Left inside the first step");
            pressedKey = KeyCode.A;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.ReviewInput && bindings.SuppressInput, "captured key is release-blocked and cannot confirm itself");
            pressedKey = KeyCode.None;
            Poll();
            setup.Confirm(now);
            Poll();
            Check(setup.Step == 0 && setup.Instruction == "Press Right.", "Left confirmation stays inside Steering");
            down.value = 1;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.ReviewInput, "digital Right accepts a different supported device control");
            down.value = 0;
            Poll();
            setup.Retry(now);
            Poll(6.01);
            Check(setup.State == Idas3SetupSession.Stage.TimedOut && bindings.HasControllerAssignment(B.ActionId.SteerLeft) && !bindings.HasControllerAssignment(B.ActionId.SteerRight), "Right timeout preserves confirmed Left without changing prior Right assignment");
            setup.Retry(now);
            Poll();
            down.value = 1;
            Poll();
            down.value = 0;
            Poll();
            setup.SelectSteeringType(false, now);
            Poll();
            Check(!setup.DigitalSteering && !bindings.HasControllerAssignment(B.ActionId.SteerLeft), "toggle removes pending digital steering and restarts axis capture");
            Check(File.ReadAllText(bindings.FilePath) == saved && bindings.Draft.actions[6].key1 == KeyCode.C, "toggle leaves saved files and unrelated keys unchanged");
            setup.SelectSteeringType(true, now);
            Poll();
            pressedKey = KeyCode.A;
            Poll();
            pressedKey = KeyCode.None;
            Poll();
            setup.Confirm(now);
            Poll();
            pressedKey = KeyCode.D;
            Poll();
            pressedKey = KeyCode.None;
            Poll();
            setup.Confirm(now);
            Poll();
            Check(setup.Step == 1, "two digital confirmations complete one Steering step");
            gas.value = -1;
            Poll();
            gas.value = 1;
            Poll();
            setup.Confirm(now);
            Poll();
            pressedKey = KeyCode.S;
            Poll();
            Check(setup.State == Idas3SetupSession.Stage.ReviewInput, "gas pedal and keyboard brake coexist in the same flow");
            pressedKey = KeyCode.None;
            Poll();
            setup.Confirm(now);
            Poll();
            up.value = 1;
            Poll();
            up.value = 0;
            Poll();
            setup.Confirm(now);
            Poll();
            down.value = 1;
            Poll();
            down.value = 0;
            Poll();
            setup.Confirm(now);
            Check(setup.Complete && bindings.ApplyExperimentalDraft(), "mixed setup saves only the separate assignment configuration");
            setup.RetireCheckpoint();
            Poll();
            pressedKey = KeyCode.A;
            gas.value = -1;
            up.value = 1;
            Poll();
            var evaluated = bindings.EvaluateDraftDriving();
            Check(evaluated.thumbLX < -32000 && evaluated.rightTrigger == 255 && bindings.DraftActionHeld(B.ActionId.ShiftUp), "mixed key/pedal/shift assignments evaluate simultaneously through production mapper");
            Check(File.ReadAllText(bindings.FilePath) == saved, "mixed setup save preserves original configuration bytes");
            pressedKey = KeyCode.None;
            gas.value = 1;
            up.value = 0;
            Poll();
            bindings.SetExperimentalDraftEnabled(false);
            Check(bindings.ApplyExperimentalDraft(), "return to original setup after mixed fixture");
            Poll();
            // Original profile can gain explicit menu assignments without switching driving.
            bindings.BeginMenuCapture(B.MenuActionId.Pause, now);
            Poll();
            down.value = 1;
            Poll();
            Check(!bindings.IsCapturing && !bindings.ExperimentalDraftEnabled && bindings.MenuBindingName(B.MenuActionId.Pause).Contains("Setup shifter"), "menu capture works with original driving profile");
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
            Check(menuFrame.padButtons == 0, "raw gamepad Confirm cannot bypass explicit menu ownership");
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
            Check(menuFrame.padButtons == 0 && menuFrame.key1 == 0, "unassigned raw POV cannot navigate");
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
            legacy.Poll(_ => false, default, now);
            legacy.EvaluateMenuNavigation(2, true, false);
            legacy.Poll(key => key == KeyCode.UpArrow, default, now);
            legacy.EvaluateMenuNavigation(2, true, false);
            menuFrame = new Idas3Native.FrameInput
            {
                padButtons = 0xffff,
                thumbLX = 32767,
                rightTrigger = 255,
                key3 = 0xffff
            };
            legacy.ApplyMenu(ref menuFrame, true);
            Check(menuFrame.key1 == (1u << 6) && menuFrame.padButtons == 0 && menuFrame.thumbLX == 0 && menuFrame.rightTrigger == 0 && menuFrame.key3 == 0, "keyboard recovery emits one canonical Up while all raw menu aliases are removed");
            legacy.EvaluateMenuNavigation(2, true, false);
            menuFrame = default;
            legacy.ApplyMenu(ref menuFrame, true);
            Check(menuFrame.key1 == 0, "holding keyboard recovery never repeats");
            legacy.EvaluateMenuNavigation(2, false, false);
            legacy.EvaluateMenuNavigation(2, true, false);
            Check(legacy.MenuEvents == 0, "held recovery key requires release after focus regain");
            legacy.Poll(key => key == KeyCode.W || key == KeyCode.S || key == KeyCode.A || key == KeyCode.E, default, now);
            legacy.EvaluateMenuNavigation(2, true, false);
            Check(legacy.MenuEvents == 0, "driving keyboard assignments do not navigate menus");
            legacy.Poll(_ => false, default, now);
            legacy.BeginMenuCapture(B.MenuActionId.Up, now);
            legacy.Poll(_ => false, default, now + .02);
            legacy.Poll(key => key == KeyCode.R, default, now + .04);
            Check(!legacy.IsCapturing && legacy.MenuBindingName(B.MenuActionId.Up).Contains("Keyboard"), "explicit menu keyboard capture uses the shared source service without hardware");
            Check(legacy.ApplyExperimentalDraft(), "keyboard menu assignment saves without enabling wheel driving");
            legacy.Poll(_ => false, default, now + .06);
            legacy.EvaluateMenuNavigation(2, true, false);
            legacy.Poll(key => key == KeyCode.R || key == KeyCode.UpArrow, default, now + .08);
            legacy.EvaluateMenuNavigation(2, true, false);
            Check(legacy.MenuEvents == 1, "assigned keyboard and recovery combine into one action");
            legacy.EvaluateMenuNavigation(2, true, false);
            Check(legacy.MenuEvents == 0, "combined keyboard sources do not repeat while held");
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
            InputSystem.Remove(shifter);
        }

        return count;
    }
}
