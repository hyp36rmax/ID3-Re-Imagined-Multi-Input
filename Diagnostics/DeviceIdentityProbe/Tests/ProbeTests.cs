using System;
using System.Linq;
using System.Text.Json;
using Id3.IdentityProbe;

// Pure diagnostic tests: no Unity runtime, Windows calls, devices or real captures.
static class ProbeTests
{
    static int checks;
    static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    static void Check(bool value, string name) { if (!value) throw new Exception(name); ++checks; }
    static Endpoint E(string id, string backend, string serial = "", string path = "", string vendor = "1", string product = "2") => new Endpoint {
        id = id, backend = backend, fields = new[] { Field.Present("serial", serial, "synthetic"), Field.Present("path", path, "synthetic"),
            Field.Present("vendorId", vendor, "synthetic"), Field.Present("productId", product, "synthetic") } };
    static void Main(string[] args)
    {
        if (args.Length == 2 && args[0] == "--uat-crash") { UatTests.CrashChild(args[1]); return; }
        var u = E("unity:1", "unity", "SERIAL-PRIVATE"); var h = E("hid:path", "hid", "SERIAL-PRIVATE", "\\\\?\\HID#PRIVATE");
        var c = Associations.Examine(new[] { u, h }, true);
        Check(c.Single().status == "unresolved" && c[0].reason.StartsWith("unique-observed"), "serial hypothesis must not auto-resolve");
        var duplicate = E("hid:other", "hid", "SERIAL-PRIVATE", "other");
        Check(Associations.Examine(new[] { u, h, duplicate }, true).All(x => x.status == "unresolved" && x.reason.StartsWith("duplicate")), "duplicate serial");
        var u2 = E("unity:2", "unity", "SERIAL-PRIVATE");
        Check(Associations.Examine(new[] { u, u2, h }, true).All(x => x.reason.StartsWith("duplicate")), "duplicate Unity serial");
        Check(Associations.Examine(new[] { E("unity:3", "unity"), E("hid:p", "hid") }, true).Single().reason == "no-unit-identity-model-is-insufficient", "one identical model is not physical identity");
        Check(Associations.Examine(new[] { u, E("hid:p", "hid", "SERIAL-PRIVATE", "p", "9") }, true).Single().status == "rejected", "contradictory VID");
        Check(Associations.Examine(new[] { u, E("hid:p", "hid", "OTHER") }, true).Single().status == "rejected", "contradictory serial");
        Check(Associations.Examine(new[] { u, h }, false).Single().reason == "incomplete-inventory", "incomplete inventory");
        Check(Associations.Examine(new[] { u }, true).Single().reason == "no-hid-endpoints", "missing native inventory");
        var d = E("di:guid", "directinput", "", "\\\\?\\hid#private");
        Check(Associations.Examine(new[] { h, d }, true).Single().status == "endpoint-alias-observed", "case insensitive exact endpoint");
        Check(Associations.Examine(new[] { h, d }, false).Single().status == "unresolved", "incomplete alias inventory");
        Check(Associations.Examine(new[] { h, d, E("di:missing-path", "directinput") }, true).All(x => x.status == "unresolved"), "missing path is not evidence of uniqueness");
        var raw = E("raw:path", "rawinput", "", "\\\\?\\hid#private");
        Check(Associations.Examine(new[] { h, raw }, true).Single().status == "endpoint-alias-observed", "Raw Input endpoint alias");
        var badUsage = E("hid:badUsage", "hid", "SERIAL-PRIVATE");
        badUsage.fields = badUsage.fields.Concat(new[] { Field.Present("usage", "5", "synthetic") }).ToArray();
        u.fields = u.fields.Concat(new[] { Field.Present("usage", "4", "synthetic") }).ToArray();
        Check(Associations.Examine(new[] { u, badUsage }, true).Single().status == "rejected", "contradictory usage");
        var xbox = E("unity:99", "unity"); xbox.fields = xbox.fields.Concat(new[] { Field.Present("interface", "XInput", "synthetic") }).ToArray();
        Check(Associations.Examine(new[] { xbox, E("xinput-slot:2", "xinput") }, true).Any(x => x.reason == "possible-xinput-mirror-no-slot-to-unity-mapping" && x.status == "unresolved"), "no invented slot mapping");
        Check(Associations.Examine(new[] { h, d, E("di:other", "directinput", "", "\\\\?\\hid#private") }, true).All(x => x.status == "unresolved"), "multiple DirectInput aliases");
        d.fields = d.fields.Concat(new[] { Field.Present("container", "SAME-CONTAINER", "synthetic") }).ToArray();
        h.fields = h.fields.Concat(new[] { Field.Present("container", "same-container", "synthetic") }).ToArray();
        Check(Associations.Examine(new[] { h, d }, true).Any(x => x.reason == "shared-container-does-not-prove-unique-actuator" && x.status == "unresolved"), "container not actuator identity");
        h.connected = false;
        Check(Associations.Examine(new[] { u, h }, true).Single().reason == "no-hid-endpoints", "disconnected candidate excluded"); h.connected = true;
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var p = new Pseudonyms(key, "session-a"); var restart = new Pseudonyms(key, "session-b");
        Check(p.Share(u).fields[0].value == restart.Share(h).fields[0].value, "same serial across backends and launches");
        Check(p.Share(d).Get("path").value == p.Share(h).Get("path").value, "case normalized Windows paths");
        Check(p.EndpointToken("hid:Path") == restart.EndpointToken("hid:path"), "native endpoint token across launches");
        Check(p.EndpointToken("unity:1") != restart.EndpointToken("unity:1"), "Unity IDs are session local");
        Check(p.EndpointToken("xinput-slot:0") != restart.EndpointToken("xinput-slot:0"), "slots are session local");
        Check(p.Token("serial", "same") != p.Token("path", "same"), "domain separation");
        Check(p.Token("serial", "same") != new Pseudonyms(new byte[32], "session-a").Token("serial", "same"), "different campaign");
        var dirty = E("unity:9", "unity", "SENSITIVE-RAW");
        dirty.fields = dirty.fields.Concat(new[] { Field.Present("unknownField", "SENSITIVE-RAW", "synthetic"),
            Field.Present("capabilities", "{\"serial\":\"SENSITIVE-RAW\"}", "synthetic"),
            new Field { name = "container", status = "error", source = "SyntheticAPI", error = "5" } }).ToArray();
        var record = new Record { evidenceClass = "synthetic", endpoints = new[] { p.Share(dirty) }, candidates = c.Select(p.Share).ToArray() };
        string json = JsonSerializer.Serialize(record, Json);
        Check(!json.Contains("SENSITIVE-RAW") && !json.Contains("SERIAL-PRIVATE"), "share redaction including unknown capabilities");
        var roundTrip = JsonSerializer.Deserialize<Record>(json, Json);
        Check(roundTrip.evidenceClass == "synthetic" && roundTrip.endpoints[0].Get("container").error == "5", "serialization and API error");
        Check(roundTrip.endpoints[0].Get("path").status == "missing" && roundTrip.endpoints[0].Get("path").value == "", "missing field stays missing");
        var g = new Generations();
        g.Apply(new[] { u, h }); Check(u.connectionGeneration == 1 && h.connectionGeneration == 1, "initial generation");
        g.Apply(new[] { h, u }); Check(u.connectionGeneration == 1, "order change");
        g.Apply(new[] { h }); g.Apply(new[] { u, h }); Check(u.connectionGeneration == 2 && h.connectionGeneration == 1, "reconnect generation");
        g.Removed(u.id); u.connected = false; g.Observe(u); Check(u.connectionGeneration == 2, "disconnect event retains generation");
        u.connected = true; g.Observe(u); Check(u.connectionGeneration == 3, "reconnect event increments generation");
        g.Apply(new[] { u, h }); Check(u.connectionGeneration == 3, "event followed by snapshot doesn't double increment");
        var original = Associations.Examine(new[] { u, h, d }, true).Select(x => x.left + x.right + x.reason).OrderBy(x => x).ToArray();
        var reversed = Associations.Examine(new[] { d, h, u }, true).Select(x => x.left + x.right + x.reason).OrderBy(x => x).ToArray();
        Check(original.SequenceEqual(reversed), "candidate set independent of enumeration order");
        UatTests.Run();
        Console.WriteLine("PASS: " + checks + " synthetic diagnostic checks (not Unity/Windows/hardware validation).");
    }
}
