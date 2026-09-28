using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using B = Idas3ControlBindings;
using M = Idas3ControlBindings.MenuActionId;

internal static class MenuNavigationChecks
{
    internal static int Run(string root)
    {
        int count = 0;
        void Check(bool ok, string message)
        {
            ++count;
            if (!ok)
                throw new Exception(message);
        }

        var device = new Joystick
        {
            deviceId = 6000,
            path = "/menu",
            description = new Description
            {
                interfaceName = "HID",
                product = "Navigation fixture"
            }
        };
        var other = new Joystick
        {
            deviceId = 6001,
            path = "/other",
            description = new Description
            {
                interfaceName = "HID",
                product = "Pedal fixture"
            }
        };
        AxisControl Add(string path, bool button = false)
        {
            AxisControl control = button ? new ButtonControl() : new AxisControl();
            control.path = device.path + "/" + path;
            device.allControls.Add(control);
            return control;
        }

        var steering = Add("steering");
        var pedal = Add("pedal");
        pedal.value = 1;
        var up = Add("dpad/up", true);
        var down = Add("dpad/down", true);
        var confirm = Add("confirm", true);
        var back = Add("back", true);
        var pause = Add("pause", true);
        var start = Add("start", true);
        var otherAxis = new AxisControl
        {
            path = "/other/axis"
        };
        other.allControls.Add(otherAxis);
        InputSystem.Add(device);
        InputSystem.Add(other);
        double now = 0;
        using var provider = new Idas3ControllerDevices(() => now, (uint slot, out Idas3Native.PadState state) =>
        {
            state = default;
            return 1167;
        }, d => d == device || d == other);
        provider.Initialize(root);
        string active = provider.ActiveName;
        otherAxis.value = 1;
        provider.Tick(true);
        Check(provider.ActiveName == active, "upstream stable generic selection: another component cannot steal active source");
        var bindings = new B();
        bindings.Initialize(root);
        bindings.ApplyDraft();
        string original = File.ReadAllText(bindings.FilePath);
        bindings.BeginWheelSetup();
        void Poll(int context = 2, bool focused = true, bool preview = false)
        {
            now += .02;
            provider.Tick(false);
            bindings.Poll(_ => false, default, now, provider.Controls, provider.Snapshot);
            bindings.EvaluateMenuNavigation(context, focused, preview);
        }

        Idas3EndpointSnapshot Endpoint() => provider.Snapshot.Endpoints.First(e => e.Identity.Fields.Any(f => f.Name == "runtimeId" && f.Value == "6000"));
        bool Assign(M action, string path, int direction = 1, float rest = 0)
        {
            var e = Endpoint();
            return bindings.TrySetMenuControl(action, e.Token, e.ConnectionGeneration, path, direction, rest);
        }

        Poll();
        var endpoint = Endpoint();
        Check(bindings.TrySetExperimentalControl(B.ActionId.SteerRight, endpoint.Token, endpoint.ConnectionGeneration, "steering", 1, 0), "driving assignment fixture");
        Check(Assign(M.Right, "steering") && Assign(M.Left, "steering", -1), "same axis independent directions and intentional driving/menu reuse");
        Check(Assign(M.Up, "dpad/up") && Assign(M.Down, "dpad/down"), "individual POV directions");
        Check(Assign(M.Confirm, "confirm") && Assign(M.Back, "back") && Assign(M.Pause, "pause") && Assign(M.Start, "start"), "four distinct command bindings");
        Check(!Assign(M.Back, "confirm") && bindings.LastError.Contains("Confirm"), "conflicts checked within menu group only");
        Check(bindings.ApplyExperimentalDraft(), "version 2 save");
        Poll();
        steering.value = .02f;
        Poll();
        Check(bindings.MenuEvents == 0, "center jitter does not navigate");
        steering.value = .74f;
        Poll();
        Check(bindings.MenuEvents == 0, "below activation does not navigate");
        steering.value = .76f;
        Poll();
        Check(bindings.MenuEvent(M.Right), "threshold crossing gives one event");
        Poll();
        Poll();
        Check(bindings.MenuEvents == 0, "holding has no repeat");
        steering.value = .45f;
        Poll();
        steering.value = .9f;
        Poll();
        Check(bindings.MenuEvents == 0, "return above release threshold does not rearm");
        steering.value = .39f;
        Poll();
        Check(bindings.MenuArmed((int)M.Right), "below release point rearms");
        steering.value = .8f;
        Poll();
        Check(bindings.MenuEvent(M.Right), "fresh excursion allowed");
        steering.value = -.8f;
        Poll();
        Check(!bindings.MenuEvent(M.Left), "opposite direction requires neutral before another excursion");
        Poll(3);
        Check(bindings.MenuEvents == 0, "context entry while deflected blocks navigation");
        steering.value = .8f; Poll(3); Check(bindings.MenuEvents == 0, "entry guard also covers the opposite direction before observed neutral");
        steering.value = 0;
        Poll(3);
        steering.value = -.8f;
        Poll(3);
        Check(bindings.MenuEvent(M.Left), "neutral rearms after context entry");
        Poll(3, false);
        Poll(3);
        Check(bindings.MenuEvents == 0, "focus regain while held cannot navigate");
        steering.value = 0;
        Poll();
        up.value = 1;
        steering.value = .9f;
        Poll();
        Check(bindings.MenuEvent(M.Up) && !bindings.MenuEvent(M.Right), "POV diagonal uses vertical priority and one event");
        up.value = 0;
        Poll();
        Check(bindings.MenuEvents == 0, "diagonal's consumed horizontal component cannot fire while held");
        steering.value = 0;
        Poll();
        up.value = down.value = 1;
        Poll();
        Check(bindings.MenuEvents == 0, "opposing directions cancel");
        up.value = down.value = 0;
        Poll();
        up.value = 1; Poll();
        steering.value = .9f; Poll();
        Check(bindings.MenuEvents == 0, "adding horizontal while POV vertical is held cannot double-navigate");
        up.value = 0; Poll(); Check(bindings.MenuEvents == 0, "consumed diagonal component stays latched until release");
        steering.value = 0; Poll();
        confirm.value = 1;
        Poll();
        Check(bindings.MenuEvent(M.Confirm), "Confirm fresh press");
        Poll();
        Check(bindings.MenuEvents == 0, "Confirm never repeats");
        back.value = 1;
        Poll();
        Check(bindings.MenuEvent(M.Back), "Back fresh press independent from held Confirm");
        Poll();
        Check(bindings.MenuEvents == 0, "Back never repeats");
        confirm.value = back.value = 0;
        Poll();
        confirm.value = back.value = 1;
        Poll();
        Check(bindings.MenuEvent(M.Back) && !bindings.MenuEvent(M.Confirm), "simultaneous Back wins Confirm");
        confirm.value = back.value = 0;
        Poll();
        Poll(100);
        start.value = 1;
        Poll(100);
        var frame = default(Idas3Native.FrameInput);
        bindings.ApplyMenu(ref frame, true);
        Check((frame.key0 & (1u << 13)) != 0 && frame.padButtons == 0 && !bindings.RawPauseHeld, "Start translates to native frontend confirmation without Pause");
        start.value = 0;
        Poll();
        pause.value = 1;
        Poll(0);
        Check(!bindings.RawPauseHeld, "enter driving while Pause held requires release");
        pause.value = 0;
        Poll(0);
        pause.value = 1;
        Poll(0);
        Check(bindings.RawPauseHeld && !bindings.MenuEvent(M.Start), "Pause fresh press separate from Start");
        pause.value = 0;
        Poll();
        bindings.ClearMenuAssignment(M.Confirm);
        Check(Assign(M.Confirm, "pedal", -1, 1), "reverse pedal menu assignment");
        bindings.ApplyExperimentalDraft();
        Poll();
        pedal.value = -.6f;
        Poll();
        Check(bindings.MenuEvent(M.Confirm), "pedal resting at positive end uses directional extent");
        pedal.value = 1;
        Poll();
        bindings.ClearMenuAssignment(M.Confirm);
        Check(Assign(M.Confirm, "pedal", 1, -1), "opposite pedal rest");
        bindings.ApplyExperimentalDraft();
        pedal.value = -1;
        Poll();
        pedal.value = .6f;
        Poll();
        Check(bindings.MenuEvent(M.Confirm), "pedal resting at negative end");
        bindings.ClearMenuAssignment(M.Left);
        bindings.ClearMenuAssignment(M.Right);
        Check(Assign(M.Right, "steering", 1, -.5f), "asymmetric calibrated rest");
        bindings.ApplyExperimentalDraft();
        steering.value = -.5f;
        Poll();
        steering.value = .6f;
        Poll();
        Check(!bindings.MenuEvent(M.Right), "asymmetric travel below threshold");
        steering.value = .7f;
        Poll();
        Check(bindings.MenuEvent(M.Right), "asymmetric travel above threshold");
        bindings.SetMenuActivation(.90f);
        bindings.ApplyExperimentalDraft();
        Poll();
        Check(bindings.MenuEvents == 0, "assignment/activation save disarms held controls");
        steering.value = -.5f;
        Poll();
        steering.value = .8f;
        Poll();
        Check(!bindings.MenuEvent(M.Right), "Activation Point affects navigation independently");
        frame = default;
        bindings.ApplyDriving(ref frame);
        Check(frame.thumbLX > 26000, "menu adjustment does not change driving calibration");
        InputSystem.Remove(device);
        Poll();
        Check(bindings.MenuEvents == 0 && !bindings.MenuArmed((int)M.Right), "disconnect invalidates and disarms menu action");
        InputSystem.Add(device);
        Poll();
        Check(bindings.MenuEvents == 0 && bindings.MenuBindingName(M.Right).Contains("reassign"), "reconnect never restores session assignment implicitly");
        Check(File.ReadAllText(bindings.FilePath) == original, "all menu saves leave original configuration bytes unchanged");
        var loaded = new B();
        loaded.Initialize(root);
        Check(loaded.MenuBindingName(M.Right).Contains("reassign"), "restart keeps menu preference without physical association");
        var checkpoint = bindings.SaveDraftCheckpoint();
        bindings.ClearMenuAssignment(M.Right);
        bindings.RestoreDraftCheckpoint(checkpoint);
        Check(bindings.MenuBindingName(M.Right).Contains("Navigation fixture"), "checkpoint includes menu assignments/settings");
        bindings.BeginEdit();
        bindings.SetMenuActivation(.55f);
        bindings.CancelEdit(false);
        Check(bindings.MenuActivationPoint == .90f, "Discard restores saved Activation Point");
        // Production capture service must support menu bindings and measured menu-only extent.
        steering.value = 0;
        pedal.value = -1;
        Poll();
        bindings.BeginMenuCapture(M.Right, now);
        Poll();
        steering.value = .5f;
        Poll();
        Check(!bindings.IsCapturing && bindings.MenuBindingName(M.Right).Contains("Navigation fixture"), "shared capture assigns menu axis");
        bindings.ApplyExperimentalDraft();
        steering.value = 0;
        Poll();
        steering.value = .46f;
        Poll();
        Check(bindings.MenuEvent(M.Right), "menu capture calibrates comfortable travel, not mechanical stop");
        // A draft test may observe events, but no navigation packet or Pause leaks to the surrounding UI.
        steering.value = 0;
        Poll(3, true, true);
        steering.value = .46f;
        Poll(3, true, true);
        Check(bindings.MenuEvent(M.Right), "Test Controls exposes draft event");
        frame = default;
        bindings.ApplyMenu(ref frame, true);
        Check(frame.key1 == 0 && frame.padButtons == 0 && !bindings.RawPauseHeld, "Test Controls draft events never enter surrounding menu packet");
        bindings.Poll(key => key == KeyCode.Escape || key == KeyCode.F1, default, now, provider.Controls, provider.Snapshot);
        bindings.EvaluateMenuNavigation(3, true, true);
        Check(!bindings.RawPauseHeld && !bindings.RawOnlineHeld, "test ownership consumes recovery and gameplay shortcuts");
        bindings.Poll(_ => false, default, now, provider.Controls, provider.Snapshot);
        bindings.EvaluateMenuNavigation(0, true, false);
        bindings.Poll(key => key == KeyCode.Escape || key == KeyCode.F1, default, now, provider.Controls, provider.Snapshot);
        bindings.EvaluateMenuNavigation(0, true, false);
        Check(bindings.RawPauseHeld, "fresh Escape uses the canonical pause dispatcher in gameplay");
        bindings.EvaluateMenuNavigation(0, true, false);
        Check(!bindings.RawPauseHeld, "held Escape does not repeat");
        steering.value = 0;
        Poll();
        Check(Assign(M.Up, "dpad/up") && Assign(M.Back, "back") && bindings.ApplyExperimentalDraft(), "explicit button/POV reassignment after reconnect");
        back.value = 1;
        up.value = 1;
        Poll(7);
        Check(bindings.MenuEvents == 0, "menu entry with held button and POV requires release");
        Poll(7, false);
        Poll(7);
        Check(bindings.MenuEvents == 0, "held POV remains guarded across focus regain");
        back.value = up.value = 0;
        Poll(7);
        up.value = 1;
        Poll(7);
        Check(bindings.MenuEvent(M.Up), "POV fresh press after entry guard");
        up.value = 0;
        Poll();
        var savedNavigation = bindings.SaveDraftCheckpoint();
        Check(bindings.ProposeArcadeNavigation(), "arcade proposal explicitly created");
        Check(bindings.MenuBindingName(M.Right).Contains("Navigation fixture") && bindings.MenuBindingName(M.Pause).Contains("Navigation fixture"), "proposal shows driving source and retains separately assigned Pause");
        bindings.RestoreDraftCheckpoint(savedNavigation);
        Check(bindings.MenuActivationPoint == .90f, "proposal cancellation restores navigation settings");
        var newEndpoint = Endpoint();
        Check(bindings.TrySetExperimentalControl(B.ActionId.Accelerate, newEndpoint.Token, newEndpoint.ConnectionGeneration, "pedal", 1, -1), "proposal conflict fixture");
        bindings.ClearMenuAssignment(M.Pause);
        Check(Assign(M.Pause, "pedal", 1, -1), "menu pause shares driving pedal intentionally");
        Check(!bindings.ProposeArcadeNavigation() && bindings.LastError.Contains("conflicts"), "proposal cannot silently duplicate Confirm and Pause");
        bindings.RestoreDraftCheckpoint(savedNavigation);
        bindings.SetMenuActivation(.55f);
        string priorMenuFile = File.ReadAllText(bindings.ExperimentalFilePath);
        File.Delete(bindings.ExperimentalFilePath);
        Directory.CreateDirectory(bindings.ExperimentalFilePath);
        Check(!bindings.ApplyExperimentalDraft() && bindings.HasExperimentalChanges && bindings.MenuActivationPoint == .55f, "menu persistence failure retains draft");
        Directory.Delete(bindings.ExperimentalFilePath);
        File.WriteAllText(bindings.ExperimentalFilePath, priorMenuFile);
        bindings.CancelEdit(false);
        Check(bindings.MenuActivationPoint == .90f, "discard after menu failure restores saved settings");
        // Migrating an existing experimental v1 file creates no automatic navigation bindings or file write.
        string versionOne = "{\"version\":1,\"enabled\":true,\"actions\":[" + string.Join(",", Enumerable.Repeat("{\"binding\":{}}", 10)) + "]}";
        File.WriteAllText(bindings.ExperimentalFilePath, versionOne);
        loaded.Initialize(root);
        Check(loaded.MenuBindingName(M.Right) == "Unassigned" && File.ReadAllText(bindings.ExperimentalFilePath) == versionOne, "v1 migration is in-memory and never derives menu assignments");
        var legacy = new B();
        legacy.Initialize(Path.Combine(root, "legacy"));
        legacy.ControllerDeviceChanged();
        legacy.Poll(_ => false, new B.PadState { connected = true, rightTrigger = 255 }, 0);
        legacy.Poll(_ => false, new B.PadState { connected = true, rightTrigger = 255, buttons = 0x2000 }, 1);
        frame = default;
        legacy.ApplyDriving(ref frame);
        Check(frame.rightTrigger == 0 && (frame.padButtons & 0x2000) != 0, "upstream per-control guard: held reconnect pedal does not block fresh paddle");
        legacy.Poll(_ => false, new B.PadState { connected = true }, 2);
        legacy.Poll(_ => false, new B.PadState { connected = true, rightTrigger = 255 }, 3);
        frame = default;
        legacy.ApplyDriving(ref frame);
        Check(frame.rightTrigger == 255, "original pedal re-arms after release");
        var custom = new B();
        custom.Initialize(Path.Combine(root, "custom-legacy"));
        custom.SelectControllerProfile("generic", "Generic", true);
        var legacyPedal = new Idas3ControllerControl
        {
            path = "pedal",
            label = "Pedal",
            minimum = -1,
            maximum = 1,
            value = 1
        };
        var legacyPaddle = new Idas3ControllerControl
        {
            path = "paddle",
            label = "Paddle",
            minimum = 0,
            maximum = 1,
            button = true
        };
        Check(custom.TrySetDraftControl(B.ActionId.Accelerate, legacyPedal, -1, 1) && custom.TrySetDraftControl(B.ActionId.ShiftUp, legacyPaddle, 1, 0) && custom.ApplyDraft(), "generic reconnect fixture");
        legacyPedal.value = -1;
        custom.ControllerDeviceChanged();
        custom.Poll(_ => false, default, 0, new[] { legacyPedal, legacyPaddle });
        legacyPaddle.value = 1;
        custom.Poll(_ => false, default, 1, new[] { legacyPedal, legacyPaddle });
        frame = default;
        custom.ApplyDriving(ref frame);
        Check(frame.rightTrigger == 0 && (frame.padButtons & 0x2000) != 0, "generic held pedal guards only its own path, not fresh paddle");
        legacyPedal.value = 1;
        custom.Poll(_ => false, default, 2, new[] { legacyPedal, legacyPaddle });
        legacyPedal.value = -1;
        custom.Poll(_ => false, default, 3, new[] { legacyPedal, legacyPaddle });
        frame = default;
        custom.ApplyDriving(ref frame);
        Check(frame.rightTrigger == 255, "generic calibrated rest rearms original pedal");
        bindings.EvaluateMenuNavigation(2, true, false);
        var timing = System.Diagnostics.Stopwatch.StartNew();
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        for (int i = 0; i < 10000; ++i) bindings.EvaluateMenuNavigation(2, true, false);
        timing.Stop();
        Console.WriteLine("PERF menu latches (8 actions, fixed snapshot): " +
            ((GC.GetAllocatedBytesForCurrentThread() - allocated) / 10000.0).ToString("F1") + " bytes/evaluation, " +
            (timing.Elapsed.TotalMilliseconds * 1000 / 10000).ToString("F2") + " us; excludes polling/rendering, not hardware timing.");
        InputSystem.Remove(device);
        InputSystem.Remove(other);
        return count;
    }
}
