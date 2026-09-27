using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.HID;

namespace Id3.IdentityProbe
{
    public sealed class ProbePlayer : MonoBehaviour
    {
        [Serializable] public sealed class BuildIdentity { public string identity = "unidentified", expectedUnity = "6000.6.0f1", expectedInput = "1.19.0"; }
        [DllImport("Id3IdentityInventory", CallingConvention = CallingConvention.Cdecl)]
        static extern int ProbeCapture(out IntPtr data, out int length);
        static readonly string[] Steps = { "baseline", "restart", "same-port", "different-port", "reversed-order", "unrelated-controller", "identical-devices", "xinput-slots", "multiple-ffb", "custom-observation" };
        static readonly string[] Verdicts = { "observation", "pass", "unresolved", "fail", "untested" };
        readonly Generations generations = new Generations();
        readonly Dictionary<string, float> observedValues = new Dictionary<string, float>();
        Pseudonyms pseudo;
        BuildIdentity build;
        string session, campaign, directory, status = "Starting", label = "", collectionStart, captureStep, captureObservation, capturePrivateObservation;
        StreamWriter log, privateLog;
        Task<string> pending;
        long sequence;
        int generation, stepIndex, verdictIndex;
        double nextScan, nextSample;
        bool autoCapture = true, subscribed;
        Endpoint[] lastUnity = Array.Empty<Endpoint>();
        Vector2 scroll;
        string summary = "Waiting for first inventory.";

        void Start()
        {
            try {
                if (Application.platform != RuntimePlatform.WindowsPlayer) throw new PlatformNotSupportedException();
                var asset = Resources.Load<TextAsset>("probe-build");
                build = asset == null ? new BuildIdentity() : JsonUtility.FromJson<BuildIdentity>(asset.text);
                if (Application.unityVersion != build.expectedUnity || InputSystem.version.ToString() != build.expectedInput) throw new InvalidOperationException();
                string campaignName = Argument("--campaign") ?? "default";
                if (campaignName.Length > 64 || campaignName.Length == 0 || campaignName.Any(c => !char.IsLetterOrDigit(c) && c != '-' && c != '_'))
                    throw new ArgumentException("Invalid campaign name");
                // Dedicated storage, never Application.persistentDataPath or the game's save root.
                string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ID3IdentityProbe", "Campaigns", campaignName);
                Directory.CreateDirectory(root);
                string keyFile = Path.Combine(root, "campaign-key.secret");
                if (!File.Exists(keyFile)) {
                    byte[] fresh = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(fresh);
                    try { using (var f = new FileStream(keyFile, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { f.Write(fresh, 0, fresh.Length); f.Flush(true); } }
                    catch (IOException) { if (!File.Exists(keyFile)) throw; }
                }
                session = Guid.NewGuid().ToString("N");
                pseudo = new Pseudonyms(File.ReadAllBytes(keyFile), session); campaign = pseudo.Token("campaign", "identity-probe-v1");
                directory = Path.Combine(root, "share", session); Directory.CreateDirectory(directory);
                log = new StreamWriter(new FileStream(Path.Combine(directory, "capture.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
                string privateDirectory = Path.Combine(root, "private", session); Directory.CreateDirectory(privateDirectory);
                privateLog = new StreamWriter(new FileStream(Path.Combine(privateDirectory, "capture.raw.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
                InputSystem.onDeviceChange += DeviceChanged; subscribed = true;
                Write("session-start", Array.Empty<Endpoint>(), Array.Empty<Candidate>(), Array.Empty<CollectionError>());
                status = "Recording. Output: " + directory;
            } catch (Exception e) { Fail(e); }
        }
        static string Argument(string name) {
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i + 1 < args.Length; ++i) if (args[i] == name) return args[i + 1];
            return null;
        }
        void Update()
        {
            if (log == null) return;
            try {
                if (Time.realtimeSinceStartupAsDouble >= nextSample) { ObserveControls(); nextSample = Time.realtimeSinceStartupAsDouble + .1; }
                if (pending != null && pending.IsCompleted) {
                    string json = pending.GetAwaiter().GetResult(); pending = null;
                    Inventory native = JsonUtility.FromJson<Inventory>(json);
                    if (native == null || native.schemaVersion != 1 || native.endpoints == null || native.errors == null) throw new InvalidDataException();
                    if (native.nativeBuild != build.identity) native.errors = native.errors.Concat(new[] { new CollectionError { api = "build-identity", code = "native-managed-mismatch" } }).ToArray();
                    // Resample Unity at completion; start/end times make non-atomic OS/Unity inventory explicit.
                    lastUnity = UnityInventory();
                    Endpoint[] all = lastUnity.Concat(native.endpoints).ToArray(); generations.Apply(all); ++generation;
                    bool complete = native.errors.Length == 0;
                    var candidates = Associations.Examine(all, complete);
                    Write("inventory", all, candidates, native.errors, native.nativeBuild, captureStep, captureObservation);
                    summary = Summarize(all, candidates, native.errors);
                    File.WriteAllText(Path.Combine(directory, "summary.txt"), summary, new UTF8Encoding(false));
                    nextScan = Time.realtimeSinceStartupAsDouble + 2;
                }
                if (autoCapture && pending == null && Time.realtimeSinceStartupAsDouble >= nextScan) Capture();
            } catch (Exception e) { Fail(e); }
        }
        void Capture()
        {
            if (pending != null || log == null) return;
            collectionStart = DateTime.UtcNow.ToString("O"); captureStep = Steps[stepIndex]; captureObservation = pseudo.Token("observation", label);
            capturePrivateObservation = label;
            pending = Task.Run(() => {
                try {
                    if (ProbeCapture(out IntPtr data, out int size) != 1 || data == IntPtr.Zero || size < 2 || size > 16 * 1024 * 1024)
                        return NativeError("ProbeCapture", "invalid-result");
                    byte[] bytes = new byte[size]; Marshal.Copy(data, bytes, 0, size);
                    return new UTF8Encoding(false, true).GetString(bytes);
                } catch (Exception e) { return NativeError("ProbeCapture", e.GetType().Name); }
            });
        }
        static string NativeError(string api, string code) => JsonUtility.ToJson(new Inventory { errors = new[] { new CollectionError { api = api, code = code } } });
        static bool Gaming(InputDevice d) {
            if (d is Keyboard || d is Pointer || d is Sensor) return false;
            if (d is Gamepad || d is Joystick) return true;
            if (d is HID hid) { var h = hid.hidDescriptor; return (int)h.usagePage == 1 && (h.usage == 4 || h.usage == 5 || h.usage == 8); }
            return false;
        }
        void ObserveControls()
        {
            var changed = new List<Endpoint>();
            foreach (var d in InputSystem.devices) {
                if (!d.added || !d.enabled || !Gaming(d)) continue;
                var samples = new List<ControlSample>();
                foreach (var control in d.allControls) {
                    if (!(control is AxisControl axis) || control.synthetic || control.noisy) continue;
                    string path = control.path.Substring(d.path.Length + 1), key = d.deviceId + ":" + path;
                    try {
                        float value = axis.ReadUnprocessedValue();
                        if (float.IsNaN(value) || float.IsInfinity(value)) continue;
                        if (!observedValues.TryGetValue(key, out float previous) || Math.Abs(value - previous) >= .01f) {
                            observedValues[key] = value;
                            samples.Add(new ControlSample { path = path, layout = control.layout, status = "present", value = value.ToString("R", CultureInfo.InvariantCulture) });
                        }
                    } catch { samples.Add(new ControlSample { path = path, layout = control.layout, status = "read-error" }); }
                }
                if (samples.Count > 0) { var e = new Endpoint { id = "unity:" + d.deviceId, backend = "unity", runtimeId = d.deviceId, controls = samples.ToArray() }; generations.Observe(e); changed.Add(e); }
            }
            if (changed.Count > 0) Write("input-sample", changed.ToArray(), Array.Empty<Candidate>(), Array.Empty<CollectionError>());
        }
        Endpoint[] UnityInventory() => InputSystem.devices.Select(Snapshot).ToArray();
        static Endpoint Snapshot(InputDevice d)
        {
            var desc = d.description;
            var fields = new List<Field> {
                Field.Present("interface", desc.interfaceName, "InputDevice.description"), Field.Present("deviceClass", desc.deviceClass, "InputDevice.description"),
                Field.Present("manufacturer", desc.manufacturer, "InputDevice.description"), Field.Present("product", desc.product, "InputDevice.description"),
                Field.Present("serial", desc.serial, "InputDevice.description.serial"), Field.Present("version", desc.version, "InputDevice.description"),
                Field.Present("capabilities", desc.capabilities, "InputDevice.description.capabilities"), Field.Present("layout", d.layout, "InputDevice.layout"),
                new Field { name = "path", status = "unsupported", source = "Unity-1.19-public-description" },
                new Field { name = "instance", status = "unsupported", source = "Unity-1.19-public-description" },
                new Field { name = "container", status = "unsupported", source = "Unity-1.19-public-description" }
            };
            if (desc.interfaceName == "HID" && !string.IsNullOrEmpty(desc.capabilities)) {
                try {
                    var hid = HID.HIDDeviceDescriptor.FromJson(desc.capabilities);
                    fields.Add(Field.Present("vendorId", hid.vendorId > 0 ? hid.vendorId.ToString(CultureInfo.InvariantCulture) : "", "HIDDeviceDescriptor"));
                    fields.Add(Field.Present("productId", hid.productId > 0 ? hid.productId.ToString(CultureInfo.InvariantCulture) : "", "HIDDeviceDescriptor"));
                    fields.Add(Field.Present("usagePage", hid.usagePage > 0 ? ((int)hid.usagePage).ToString(CultureInfo.InvariantCulture) : "", "HIDDeviceDescriptor"));
                    fields.Add(Field.Present("usage", hid.usage > 0 ? hid.usage.ToString(CultureInfo.InvariantCulture) : "", "HIDDeviceDescriptor"));
                    // Re-serialize a typed numeric-only descriptor, not arbitrary capabilities keys/strings.
                    fields.Add(Field.Present("hidDescriptor", hid.ToJson(), "HIDDeviceDescriptor.typed-projection"));
                } catch (Exception e) { fields.Add(new Field { name = "hidDescriptor", status = "error", source = "HIDDeviceDescriptor.FromJson", error = e.GetType().Name }); }
            }
            var controls = new List<ControlSample>();
            bool gaming = Gaming(d);
            foreach (var c in d.allControls) {
                string path = c.path.StartsWith(d.path + "/", StringComparison.Ordinal) ? c.path.Substring(d.path.Length + 1) : c.name;
                var sample = new ControlSample { path = path, layout = c.layout, noisy = c.noisy, synthetic = c.synthetic, status = "metadata-only" };
                // No keyboard/pointer text capture. No arbitrary ToString() on device values.
                if (gaming && d.added && d.enabled && c is AxisControl axis) {
                    try { float value = axis.ReadUnprocessedValue(); sample.status = float.IsNaN(value) || float.IsInfinity(value) ? "nonfinite" : "present"; if (sample.status == "present") sample.value = value.ToString("R", CultureInfo.InvariantCulture); }
                    catch (Exception e) { sample.status = "read-error-" + e.GetType().Name; }
                }
                controls.Add(sample);
            }
            return new Endpoint { id = "unity:" + d.deviceId, backend = "unity", runtimeId = d.deviceId, connected = d.added && d.enabled, fields = fields.ToArray(), controls = controls.ToArray() };
        }
        void DeviceChanged(InputDevice d, InputDeviceChange change)
        {
            if (log == null) return;
            try {
                if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled) generations.Removed("unity:" + d.deviceId);
                foreach (var key in observedValues.Keys.Where(k => k.StartsWith(d.deviceId + ":", StringComparison.Ordinal)).ToArray()) observedValues.Remove(key);
                var snapshot = Snapshot(d);
                if (change == InputDeviceChange.Removed || change == InputDeviceChange.Disconnected || change == InputDeviceChange.Disabled) snapshot.connected = false;
                generations.Observe(snapshot);
                Write("device-change", new[] { snapshot }, Array.Empty<Candidate>(), Array.Empty<CollectionError>(), eventType: change.ToString(), eventEndpoint: "unity:" + d.deviceId);
                nextScan = 0;
            } catch (Exception e) { Fail(e); }
        }
        void Write(string kind, Endpoint[] endpoints, Candidate[] candidates, CollectionError[] errors,
            string nativeBuild = "", string step = null, string observation = null, string eventType = "", string eventEndpoint = "")
        {
            var record = new Record { sequence = ++sequence, inventoryGeneration = generation, build = build.identity,
                nativeBuild = nativeBuild, unityVersion = Application.unityVersion, inputSystemVersion = InputSystem.version.ToString(),
                campaign = campaign, session = session, utc = DateTime.UtcNow.ToString("O"), collectionStartedUtc = kind == "inventory" ? collectionStart : "",
                kind = kind, step = step ?? Steps[stepIndex], observation = observation ?? pseudo.Token("observation", label), platform = Application.platform.ToString(),
                testerVerdict = kind == "tester-observation" ? Verdicts[verdictIndex] : "observation",
                eventType = eventType, eventEndpoint = pseudo.EndpointToken(eventEndpoint),
                endpoints = endpoints.Select(pseudo.Share).ToArray(), candidates = candidates.Select(pseudo.Share).ToArray(), errors = errors };
            log.WriteLine(JsonUtility.ToJson(record));
            if (kind == "tester-observation") File.AppendAllText(Path.Combine(directory, "steps.txt"), record.utc + " " + record.step + " " + record.testerVerdict + " " + record.observation + Environment.NewLine);
            // Full descriptors/control paths are retained only in the separate private capture.
            record.endpoints = endpoints; record.candidates = candidates; record.eventEndpoint = eventEndpoint;
            record.observation = kind == "inventory" ? capturePrivateObservation : label;
            privateLog?.WriteLine(JsonUtility.ToJson(record));
        }
        string Summarize(Endpoint[] endpoints, Candidate[] candidates, CollectionError[] errors)
        {
            var text = new StringBuilder("ID3 Identity Probe — M1-A-P1\n");
            text.AppendLine("Build: " + build.identity); text.AppendLine("Session: " + session);
            text.AppendLine("Campaign: " + campaign); text.AppendLine("UTC: " + DateTime.UtcNow.ToString("O"));
            text.AppendLine("Step: " + captureStep + " | Inventory: " + generation);
            foreach (var group in endpoints.GroupBy(e => e.backend)) text.AppendLine(group.Key + ": " + group.Count(e => e.connected) + " connected / " + group.Count() + " observed");
            text.AppendLine("Collection errors: " + errors.Length + "; field errors: " + endpoints.Sum(e => e.fields.Count(f => f.status == "error")));
            foreach (var group in candidates.GroupBy(c => c.reason)) text.AppendLine(group.Count() + " candidate(s): " + group.Key);
            text.AppendLine("Endpoint aliases are current observations, not physical-device guarantees. No Unity-to-Windows mapping is established automatically.");
            text.AppendLine("No motor output or gameplay integration. Raw identifiers and free-text labels are not written to this share folder.");
            return text.ToString();
        }
        void Fail(Exception e)
        {
            // Never leak an exception message/path/raw device value into the shareable log.
            status = "Recording stopped: " + e.GetType().Name;
            try { if (log != null) Write("collection-fatal", Array.Empty<Endpoint>(), Array.Empty<Candidate>(), new[] { new CollectionError { api = "managed-probe", code = e.GetType().Name } }); } catch { }
            log?.Dispose(); privateLog?.Dispose(); log = privateLog = null;
        }
        void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, Screen.width - 40, Screen.height - 40));
            GUILayout.Label("INITIAL D — DEVICE IDENTITY PROBE (read-only device access)");
            GUILayout.Label(status);
            stepIndex = GUILayout.SelectionGrid(stepIndex, Steps, 3);
            GUILayout.Label("Tester observation: label the physical control you moved (stored as a campaign-stable token).");
            label = GUILayout.TextField(label, 120);
            verdictIndex = GUILayout.SelectionGrid(verdictIndex, Verdicts, 5);
            autoCapture = GUILayout.Toggle(autoCapture, "Capture inventory every two seconds");
            GUI.enabled = log != null;
            if (GUILayout.Button("Record observation / capture now")) {
                try { Write("tester-observation", UnityInventory(), Array.Empty<Candidate>(), Array.Empty<CollectionError>()); Capture(); }
                catch (Exception e) { Fail(e); }
            }
            GUI.enabled = true;
            scroll = GUILayout.BeginScrollView(scroll); GUILayout.Label(summary); GUILayout.EndScrollView();
            GUILayout.EndArea();
        }
        void OnDestroy()
        {
            if (subscribed) InputSystem.onDeviceChange -= DeviceChanged;
            try { if (log != null) Write("session-end", Array.Empty<Endpoint>(), Array.Empty<Candidate>(), Array.Empty<CollectionError>()); } catch { }
            log?.Dispose(); privateLog?.Dispose(); log = privateLog = null;
            // No blocking join: native work only owns metadata handles and releases them in its scope.
        }
    }
}
