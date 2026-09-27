using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using UnityEngine.InputSystem;

public sealed partial class Idas3ControllerDevices
{
    private readonly List<Device> snapshotDevices = new List<Device>();
    private Guid snapshotSession;
    private long snapshotSequence, inventoryGeneration, nextEndpoint;
    private Idas3DeviceFrame latestSnapshot = new Idas3DeviceFrame(Guid.Empty, 0, 0, 0, 0, Idas3SnapshotKind.Initial, Array.Empty<Idas3EndpointSnapshot>());
    // Read this property once per consumer operation. Historical frames stay immutable;
    // only the latest frame represents current availability. All writes are Unity-thread only.
    public Idas3DeviceFrame Snapshot => Volatile.Read(ref latestSnapshot);

    public bool IsCurrent(Idas3DeviceFrame frame) => ReferenceEquals(Snapshot, frame);
    public bool TryReadCurrent(Idas3EndpointToken token, long connectionGeneration, string path, out Idas3ControlSample sample)
    {
        var frame = Snapshot;
        sample = default;
        return frame.TryGetEndpoint(token, out var endpoint) && endpoint.ConnectionGeneration == connectionGeneration && endpoint.TryGetControl(path, out sample);
    }

    private void ResetSnapshots()
    {
        snapshotDevices.Clear();
        snapshotSession = Guid.NewGuid();
        snapshotSequence = inventoryGeneration = nextEndpoint = 0;
        Volatile.Write(ref latestSnapshot, new Idas3DeviceFrame(snapshotSession, 0, 0, now(), now(), Idas3SnapshotKind.Initial, Array.Empty<Idas3EndpointSnapshot>()));
    }

    private void RegisterSnapshotDevice(Device device)
    {
        snapshotDevices.Add(device);
        device.token = new Idas3EndpointToken(snapshotSession, ++nextEndpoint);
        device.validity = new Idas3SampleValidity[device.controls.Count];
        device.identity = ReadIdentityEvidence(device);
        ++inventoryGeneration;
    }

    private static Idas3IdentityField Reported(string name, string value, string source) => new Idas3IdentityField(name, value, source, string.IsNullOrEmpty(value) ? Idas3EvidenceStatus.Missing : Idas3EvidenceStatus.Reported);
    private static Idas3EndpointIdentity ReadIdentityEvidence(Device device)
    {
        var fields = new List<Idas3IdentityField>();
        if (device.unity != null)
        {
            var input = device.unity;
            var description = input.description;
            fields.Add(Reported("runtimeId", input.deviceId.ToString(CultureInfo.InvariantCulture), "Unity InputDevice.deviceId (session only)"));
            fields.Add(Reported("layout", input.layout, "Unity InputDevice.layout"));
            fields.Add(Reported("interface", description.interfaceName, "Unity InputDevice.description"));
            fields.Add(Reported("deviceClass", description.deviceClass, "Unity InputDevice.description"));
            fields.Add(Reported("version", description.version, "Unity InputDevice.description"));
            fields.Add(Reported("vendorId", device.vendorId == 0 ? null : device.vendorId.ToString(CultureInfo.InvariantCulture), "Unity description.capabilities parsed by existing provider"));
            fields.Add(Reported("productId", device.productId == 0 ? null : device.productId.ToString(CultureInfo.InvariantCulture), "Unity description.capabilities parsed by existing provider"));
            fields.Add(Reported("manufacturer", description.manufacturer, "Unity InputDevice.description"));
            fields.Add(Reported("product", description.product, "Unity InputDevice.description"));
            fields.Add(Reported("serial", description.serial, "Unity InputDevice.description; unvalidated driver report"));
            fields.Add(Reported("capabilities", description.capabilities, "Unity InputDevice.description; unvalidated driver report"));
        }
        else
        {
            fields.Add(Reported("slot", device.slot.ToString(CultureInfo.InvariantCulture), "Native XInputGetState slot (session only)"));
            fields.Add(new Idas3IdentityField("serial", null, "XInputGetState", Idas3EvidenceStatus.Unsupported));
        }

        foreach (string field in new[]
        {
            "windowsInterfacePath",
            "pnpInstanceId",
            "containerId",
            "physicalAssociation"
        }

        )
            fields.Add(new Idas3IdentityField(field, null, "Existing input provider has no validated association", Idas3EvidenceStatus.Unsupported));
        return new Idas3EndpointIdentity(fields.ToArray());
    }

    private void TrackSnapshotConnection(Device device, bool connected)
    {
        if (device.snapshotConnected == connected)
            return;
        device.snapshotConnected = connected;
        ++inventoryGeneration;
        if (connected)
            ++device.connectionGeneration;
    }

    private Idas3EndpointSnapshot CopyEndpoint(Device device, bool connected, double sampleTime = 0)
    {
        TrackSnapshotConnection(device, connected);
        var old = device.snapshot;
        if (!connected && old != null && old.Status == device.sampleStatus && old.StatusDetail == device.sampleDetail)
            return old;
        // Disabled/suppressed/backend status changes are inventory changes even if
        // both sides are non-readable. Value validity alone does not change inventory.
        if (!connected && old != null && old.Status != device.sampleStatus)
            ++inventoryGeneration;
        var controls = new Idas3ControlSample[device.controls.Count];
        for (int i = 0; i < controls.Length; ++i)
            controls[i] = new Idas3ControlSample(device.controls[i], connected ? device.validity[i] : Idas3SampleValidity.Unavailable);
        device.snapshot = new Idas3EndpointSnapshot(device.token, device.connectionGeneration, connected ? snapshotSequence + 1 : old?.SampleSequence ?? 0, connected ? (double? )sampleTime : old?.SampledAt, device.unity == null ? "NativeXInput" : "UnityInputSystem", device.choice.label, device.sampleStatus, device.sampleDetail, device.identity, controls);
        return device.snapshot;
    }

    private void PublishSnapshots(double start, Idas3SnapshotKind kind)
    {
        var endpoints = new Idas3EndpointSnapshot[snapshotDevices.Count];
        for (int i = 0; i < endpoints.Length; ++i)
            endpoints[i] = CopyEndpoint(snapshotDevices[i], snapshotDevices[i].choice.connected, start);
        Volatile.Write(ref latestSnapshot, new Idas3DeviceFrame(snapshotSession, ++snapshotSequence, inventoryGeneration, start, now(), kind, endpoints));
    }

    private void InvalidateSnapshotDevice(InputDevice input, Idas3EndpointStatus status)
    {
        if (!unityDevices.TryGetValue(input.deviceId, out var device) || !ReferenceEquals(input, device.unity))
            return;
        device.sampleStatus = status;
        device.sampleDetail = "Unity removal/disable event; awaiting next provider tick";
        var invalid = CopyEndpoint(device, false);
        // Copy the prior publication, not the mutable in-progress samples of other devices.
        var prior = Snapshot;
        var endpoints = new Idas3EndpointSnapshot[prior.Endpoints.Count];
        for (int i = 0; i < endpoints.Length; ++i)
            endpoints[i] = prior.Endpoints[i].Token.Equals(device.token) ? invalid : prior.Endpoints[i];
        double time = now();
        Volatile.Write(ref latestSnapshot, new Idas3DeviceFrame(snapshotSession, ++snapshotSequence, inventoryGeneration, time, time, Idas3SnapshotKind.Invalidation, endpoints));
    }

    private void StopSnapshots()
    {
        foreach (var device in snapshotDevices)
        {
            device.choice.connected = false;
            device.sampleStatus = Idas3EndpointStatus.Disposed;
            device.sampleDetail = "Provider stopped";
        }

        PublishSnapshots(now(), Idas3SnapshotKind.Stopped);
    }
}
