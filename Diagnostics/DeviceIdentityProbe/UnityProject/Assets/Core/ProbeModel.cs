using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Id3.IdentityProbe
{
    // Diagnostic DTOs only. No enrollment, binding or production resolver.
    [Serializable] public sealed class Field
    {
        public string name = "", value = "", status = "missing", source = "", error = "";
        public static Field Present(string name, string value, string source) => new Field {
            name = name, value = value ?? "", status = string.IsNullOrEmpty(value) ? "missing" : "present", source = source };
    }
    [Serializable] public sealed class CollectionError
    {
        public string api = "", code = "", status = "error";
    }
    [Serializable] public sealed class ControlSample
    {
        public string path = "", layout = "", status = "unread", value = "";
        public bool noisy, synthetic;
    }
    [Serializable] public sealed class Endpoint
    {
        public string id = "", backend = "";
        public int runtimeId = -1, slot = -1, connectionGeneration;
        public bool connected = true;
        public Field[] fields = Array.Empty<Field>();
        public ControlSample[] controls = Array.Empty<ControlSample>();
        public Field Get(string name) => fields?.FirstOrDefault(f => f.name == name);
        public string Value(string name) { var f = Get(name); return f != null && f.status == "present" ? f.value : ""; }
    }
    [Serializable] public sealed class Inventory
    {
        public int schemaVersion = 1;
        public string nativeBuild = "";
        public Endpoint[] endpoints = Array.Empty<Endpoint>();
        public CollectionError[] errors = Array.Empty<CollectionError>();
    }
    [Serializable] public sealed class Candidate
    {
        public string left = "", right = "", status = "unresolved", scope = "session", reason = "";
        public string[] evidence = Array.Empty<string>();
    }
    [Serializable] public sealed class Record
    {
        public int schemaVersion = 1, inventoryGeneration;
        public long sequence;
        public string probeVersion = "M1-A-P1.1", build = "", nativeBuild = "", unityVersion = "", inputSystemVersion = "";
        public string evidenceClass = "runtime-observed", campaign = "", session = "", utc = "", collectionStartedUtc = "", platform = "";
        public string kind = "inventory", step = "baseline", observation = "", eventType = "", eventEndpoint = "";
        public string testerVerdict = "observation";
        public Endpoint[] endpoints = Array.Empty<Endpoint>();
        public Candidate[] candidates = Array.Empty<Candidate>();
        public CollectionError[] errors = Array.Empty<CollectionError>();
    }

    public sealed class Pseudonyms
    {
        readonly byte[] key;
        readonly string session;
        public Pseudonyms(byte[] key, string session) { if (key == null || key.Length != 32) throw new ArgumentException("Campaign key must be 32 bytes."); this.key = (byte[])key.Clone(); this.session = session; }
        public string EndpointToken(string id) {
            if (string.IsNullOrEmpty(id)) return "";
            bool local = id.StartsWith("unity:", StringComparison.Ordinal) || id.StartsWith("xinput-slot:", StringComparison.Ordinal) || id.StartsWith("raw-session:", StringComparison.Ordinal);
            return Token("endpoint", local ? session + ":" + id : id.ToUpperInvariant());
        }
        public string Token(string domain, string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            // Domain separation prevents mixing serials with endpoint paths. Full SHA-256, no truncation.
            using (var mac = new HMACSHA256(key))
                return domain + ":" + BitConverter.ToString(mac.ComputeHash(Encoding.UTF8.GetBytes(domain + "\0" + value))).Replace("-", "").ToLowerInvariant();
        }
        public Field Share(Field f)
        {
            string v = f.value ?? "";
            // Explicit allowlist. Unknown fields and arbitrary capabilities/descriptions are opaque tokens.
            bool number = Numeric.Contains(f.name) && ulong.TryParse(v, out _);
            bool descriptor = f.name == "hidDescriptor"; // Caller supplies the typed numeric-only HID DTO, never raw capabilities JSON.
            string domain = f.name == "serial" ? "serial" : f.name == "path" ? "path" :
                f.name == "instance" || f.name == "parent" ? "instance" : f.name == "container" ? "container" : f.name;
            if (domain == "path" || domain == "instance" || domain == "container" || domain == "directInputGuid") v = v.ToUpperInvariant();
            return new Field { name = f.name, status = f.status, source = f.source, error = f.error,
                value = number || descriptor ? v : Token(domain, v) };
        }
        static readonly HashSet<string> Numeric = new HashSet<string> {
            "vendorId", "productId", "usagePage", "usage", "ffb", "axes", "buttons", "povs", "deviceType", "capsFlags", "packet",
            "xinputType", "xinputSubtype", "xinputFlags", "inputReportSize", "outputReportSize", "featureReportSize", "buttonMask" };
        public Endpoint Share(Endpoint e) => new Endpoint {
            id = EndpointToken(e.id), backend = e.backend, runtimeId = e.runtimeId, slot = e.slot,
            connectionGeneration = e.connectionGeneration, connected = e.connected,
            fields = (e.fields ?? Array.Empty<Field>()).Select(Share).ToArray(),
            controls = (e.controls ?? Array.Empty<ControlSample>()).Select(c => new ControlSample {
                path = Token("controlPath", c.path), layout = Token("controlLayout", c.layout),
                status = c.status, value = c.value, noisy = c.noisy, synthetic = c.synthetic }).ToArray() };
        public Candidate Share(Candidate c) => new Candidate { left = EndpointToken(c.left), right = EndpointToken(c.right),
            status = c.status, scope = c.scope, reason = c.reason, evidence = c.evidence };
    }

    public static class Associations
    {
        // All Unity/Windows pairings remain unresolved hypotheses. Only a matching Windows path
        // establishes an endpoint alias. No candidate ever selects an input or a force target.
        public static Candidate[] Examine(Endpoint[] endpoints, bool inventoryComplete)
        {
            var result = new List<Candidate>();
            foreach (var u in endpoints.Where(e => e.backend == "unity" && e.connected))
            {
                var native = endpoints.Where(e => e.backend == "hid" && e.connected).ToArray();
                if (native.Length == 0) result.Add(new Candidate { left = u.id, reason = "no-hid-endpoints" });
                foreach (var n in native)
                {
                    string serial = u.Value("serial");
                    bool sameSerial = serial.Length > 0 && serial == n.Value("serial");
                    bool vendorConflict = Conflict(u, n, "vendorId") || Conflict(u, n, "productId");
                    bool usageConflict = Conflict(u, n, "usagePage") || Conflict(u, n, "usage");
                    bool serialConflict = serial.Length > 0 && n.Value("serial").Length > 0 && !sameSerial;
                    var evidence = new List<string>();
                    if (sameSerial) evidence.Add("equal-serial-reported");
                    if (Equal(u, n, "vendorId") && Equal(u, n, "productId")) evidence.Add("equal-vid-pid-model-only");
                    if (Equal(u, n, "usagePage") && Equal(u, n, "usage")) evidence.Add("equal-usage-role-only");
                    bool duplicate = sameSerial && (endpoints.Count(e => e.connected && e.backend == "unity" && e.Value("serial") == serial) != 1 ||
                        native.Count(e => e.Value("serial") == serial) != 1);
                    result.Add(new Candidate { left = u.id, right = n.id, evidence = evidence.ToArray(),
                        status = vendorConflict || usageConflict || serialConflict ? "rejected" : "unresolved",
                        scope = sameSerial ? "physical-hypothesis" : "session",
                        reason = vendorConflict || usageConflict || serialConflict ? "contradictory-metadata" : duplicate ? "duplicate-serial-or-collections" :
                            !inventoryComplete ? "incomplete-inventory" : sameSerial ? "unique-observed-serial-not-validated-physical-identity" : "no-unit-identity-model-is-insufficient" });
                }
                if (u.Value("interface") == "XInput") foreach (var x in endpoints.Where(e => e.backend == "xinput" && e.connected))
                    result.Add(new Candidate { left = u.id, right = x.id, reason = "possible-xinput-mirror-no-slot-to-unity-mapping", evidence = new[] { "xinput-backend-family" } });
            }
            foreach (var d in endpoints.Where(e => (e.backend == "directinput" || e.backend == "rawinput") && e.connected))
            {
                var hid = endpoints.Where(e => e.backend == "hid" && e.connected).ToArray();
                var matching = hid.Where(h => Equal(d, h, "path", true)).ToArray();
                bool pathsComplete = hid.All(h => h.Value("path").Length > 0) && endpoints.Where(e => e.backend == d.backend && e.connected).All(e => e.Value("path").Length > 0);
                if (matching.Length == 0) result.Add(new Candidate { left = d.id, reason = "no-exact-hid-path-association", scope = "endpoint" });
                foreach (var h in matching)
                {
                    bool conflict = Conflict(d, h, "vendorId") || Conflict(d, h, "productId");
                    bool one = matching.Length == 1 && endpoints.Count(e => e.backend == d.backend && e.connected && Equal(e, h, "path", true)) == 1;
                    result.Add(new Candidate { left = d.id, right = h.id, scope = "endpoint", evidence = new[] { "equal-windows-interface-path" },
                        status = conflict ? "rejected" : one && inventoryComplete && pathsComplete ? "endpoint-alias-observed" : "unresolved",
                        reason = conflict ? "contradictory-metadata" : !one ? "multiple-path-aliases" : !inventoryComplete || !pathsComplete ? "incomplete-inventory" : "same-endpoint-not-portable-physical-proof" });
                }
            }
            // Surface composite/duplicate-container evidence, but never infer a unique actuator from it.
            var containers = endpoints.Where(e => e.connected && e.Value("container").Length > 0).GroupBy(e => e.Value("container"), StringComparer.OrdinalIgnoreCase);
            foreach (var group in containers) {
                var a = group.OrderBy(e => e.id, StringComparer.Ordinal).ToArray();
                for (int i = 0; i < a.Length; ++i) for (int j = i + 1; j < a.Length; ++j)
                    result.Add(new Candidate { left = a[i].id, right = a[j].id, scope = "endpoint-group", reason = "shared-container-does-not-prove-unique-actuator", evidence = new[] { "equal-container" } });
            }
            return result.ToArray();
        }
        static bool Equal(Endpoint a, Endpoint b, string field, bool ignoreCase = false) => a.Value(field).Length > 0 &&
            string.Equals(a.Value(field), b.Value(field), ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
        static bool Conflict(Endpoint a, Endpoint b, string field) => a.Value(field).Length > 0 && b.Value(field).Length > 0 && !Equal(a, b, field);
    }

    public sealed class Generations
    {
        readonly Dictionary<string, int> counts = new Dictionary<string, int>();
        HashSet<string> live = new HashSet<string>();
        public void Observe(Endpoint e) {
            if (e.connected) {
                if (!live.Contains(e.id)) counts[e.id] = counts.TryGetValue(e.id, out int n) ? n + 1 : 1;
                live.Add(e.id);
            } else live.Remove(e.id);
            e.connectionGeneration = counts.TryGetValue(e.id, out int value) ? value : 0;
        }
        public void Apply(Endpoint[] endpoints)
        {
            var next = new HashSet<string>();
            foreach (var e in endpoints) {
                if (e.connected) {
                    if (!live.Contains(e.id)) counts[e.id] = counts.TryGetValue(e.id, out int n) ? n + 1 : 1;
                    next.Add(e.id);
                }
                e.connectionGeneration = counts.TryGetValue(e.id, out int value) ? value : 0;
            }
            live = next;
        }
        public void Removed(string id) { live.Remove(id); }
    }
}
