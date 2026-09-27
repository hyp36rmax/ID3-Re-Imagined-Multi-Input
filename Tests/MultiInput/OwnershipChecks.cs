using System;
using System.IO;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Bindings = Idas3ControlBindings;

internal static class OwnershipChecks
{
    internal static int Run(string root)
    {
        int checks = 0;
        void Check(bool condition, string message)
        {
            ++checks;
            if (!condition) throw new Exception(message);
        }

        var source = new Joystick { deviceId = 5000, path = "/ownership", description = new Description { interfaceName = "HID", product = "Non-FFB input" } };
        var axis = new AxisControl { path = "/ownership/axis", value = 0 };
        source.allControls.Add(axis);
        InputSystem.Add(source);
        double now = 0;
        using var devices = new Idas3ControllerDevices(() => now,
            (uint slot, out Idas3Native.PadState state) => { state = default; return 1167; }, device => device == source);
        devices.Initialize(root);
        Check(devices.Select("automatic"), "ownership fixture saves device preference");
        var bindings = new Bindings();
        bindings.Initialize(root);
        bindings.SelectControllerProfile("original-one", "Original controller");
        Check(bindings.TrySetDraftPad(Bindings.ActionId.Accelerate, Bindings.PadInput.A) && bindings.ApplyDraft(), "first original profile fixture");
        bindings.SelectControllerProfile("original-two", "Other controller");
        Check(bindings.TrySetDraftPad(Bindings.ActionId.Accelerate, Bindings.PadInput.RightShoulder) && bindings.ApplyDraft(), "second original profile fixture");
        bindings.SelectControllerProfile("original-one", "Original controller");
        string original = File.ReadAllText(bindings.FilePath);
        string deviceFile = Path.Combine(root, "controller-device.json");
        string originalDevice = File.ReadAllText(deviceFile);
        devices.BeginSelectionEdit();
        bindings.BeginEdit();
        var checkpoint = bindings.BeginWheelSetup();
        Check(bindings.ExperimentalDraftEnabled && !bindings.ExperimentalEnabled, "new wheel setup changes only draft mode");
        bindings.RestoreDraftCheckpoint(checkpoint);
        bindings.CancelEdit(false);
        devices.CancelSelectionEdit();
        Check(!bindings.ExperimentalDraftEnabled && File.ReadAllText(bindings.FilePath) == original && File.ReadAllText(deviceFile) == originalDevice && !File.Exists(bindings.ExperimentalFilePath), "open/new setup/cancel preserves original files and does not create new settings");

        devices.BeginSelectionEdit();
        bindings.BeginEdit();
        bindings.BeginWheelSetup();
        void Poll(ushort buttons = 0)
        {
            now += .02;
            devices.Tick(false);
            bindings.Poll(key => false, new Bindings.PadState { connected = true, buttons = buttons }, now, devices.Controls, devices.Snapshot);
        }
        Poll();
        var endpoint = devices.Snapshot.Endpoints.Single(item => item.Status == Idas3EndpointStatus.Ready);
        Check(bindings.TrySetExperimentalControl(Bindings.ActionId.Accelerate, endpoint.Token, endpoint.ConnectionGeneration, "axis", 1, 0), "new wheel assignment uses separate production source references");
        var result = Idas3ControllerSave.Save(
            () => !bindings.HasUnsavedChanges || bindings.ApplyDraft(), () => bindings.LastError,
            devices.ApplySelectionEdit, () => devices.LastError,
            () => true, () => null, bindings.ApplyExperimentalDraft, () => bindings.ExperimentalError);
        Check(result.Complete && File.ReadAllText(bindings.FilePath) == original && File.ReadAllText(deviceFile) == originalDevice, "combined menu save preserves both original files byte for byte");
        Poll();
        axis.value = .5f;
        Poll(0x1000);
        var frame = default(Idas3Native.FrameInput);
        bindings.ApplyDriving(ref frame);
        Check(frame.rightTrigger == 128, "separate wheel source contributes without adding original held full-throttle button");
        bindings.SetExperimentalDraftEnabled(false);
        Check(bindings.ApplyExperimentalDraft(), "explicit switch back saved");
        Poll();
        Poll(0x1000);
        frame = default;
        bindings.ApplyDriving(ref frame);
        Check(frame.rightTrigger == 255, "return to existing controller restores original A throttle mapping");
        Check(File.ReadAllText(bindings.FilePath) == original, "switching back does not rewrite original profiles");
        var reloaded = new Bindings();
        reloaded.Initialize(root);
        reloaded.SelectControllerProfile("original-one", "Original controller");
        Check(reloaded.Current.actions[0].pad == Bindings.PadInput.A, "original first profile survives save and restart");
        reloaded.SelectControllerProfile("original-two", "Other controller");
        Check(reloaded.Current.actions[0].pad == Bindings.PadInput.RightShoulder, "original second profile survives save and restart");
        Check(!reloaded.ExperimentalEnabled, "restart respects explicitly saved existing mode");
        InputSystem.Remove(source);
        return checks;
    }
}
