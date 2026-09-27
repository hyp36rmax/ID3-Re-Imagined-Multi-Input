using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace Id3.IdentityProbe
{
    [Serializable] public sealed class UatSession {
        public string id = "", startedUtc = "", endedUtc = "", state = "active";
        public int inventories, validInventories;
        public bool latestInventoryValid;
    }
    [Serializable] public sealed class UatResult {
        public int step, attempt;
        public string session = "", utc = "", expected = "", actual = "", observation = "", capture = "NOT TESTED", identity = "UNRESOLVED";
    }
    [Serializable] public sealed class UatManifest {
        public int schemaVersion = 1, revision;
        public string milestone = "M1-A-P1", runId = "", build = "", commit = "", campaign = "", startedUtc = "", updatedUtc = "", endedUtc = "";
        public string state = "incomplete", captureResult = "NOT TESTED", identityResult = "UNRESOLVED", environment = "", purpose = "Baseline and application-restart identity evidence; no production or force validation";
        public UatSession[] sessions = Array.Empty<UatSession>();
        public UatResult[] results = Array.Empty<UatResult>();
    }
    // Owns only a dedicated shareable UAT run. Keys/raw campaigns are never imported or exported.
    public sealed class UatRun : IDisposable {
        public const string Milestone = "M1-A-P1";
        public static readonly string[] Verdicts = { "NOT TESTED", "PASS", "FAIL", "UNRESOLVED" };
        public static readonly string[] Expected = {
            "Available controls and metadata captured with explicit errors and matching build identities; identity ambiguity remains unresolved.",
            "New process session, same campaign/build; unchanged identifier strings have equal tokens. Runtime IDs remain session-scoped."
        };
        public const string Plan = "1. Baseline: label wheel/pedals/shifter separately, move one control for 3 seconds, wait 4 seconds for inventory, record capture and identity results. Mark unavailable hardware NOT TESTED.\n2. Restart: Save for restart, close application, launch using the displayed --campaign and --uat-run arguments. Repeat baseline observations, compare campaign identifier tokens across sessions, record results.\nLater reconnect, reverse order, unrelated/identical devices, XInput and multiple-FFB stages are PENDING REVIEW. No driving or force output.\nPASS capture does not imply PASS identity or working production multi-input/FFB. UNRESOLVED means insufficient/ambiguous evidence. FAIL means violated expectations. NOT TESTED means not performed.\n";
        readonly Func<object, string> encode;
        readonly Func<string, UatManifest> decode;
        readonly FileStream lease;
        StreamWriter capture;
        readonly string root;
        public string DirectoryPath { get; private set; }
        public UatManifest Manifest { get; private set; }
        public bool Active => capture != null;
        public string CurrentSession => Manifest.sessions.Last().id;
        static string Now() => DateTime.UtcNow.ToString("O");
        static void Segment(string value) {
            if (string.IsNullOrEmpty(value) || value.Length > 100 || value.Any(c => !(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')) throw new ArgumentException("Invalid run or session ID");
        }
        static void NewText(string path, string text) {
            using (var f = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read)) {
                byte[] b = new UTF8Encoding(false).GetBytes(text); f.Write(b, 0, b.Length); f.Flush(true);
            }
        }
        public UatRun(string root, string runId, string campaign, string build, string session,
            Func<object, string> encode, Func<string, UatManifest> decode, bool exportOnly = false) {
            this.root = root; this.encode = encode; this.decode = decode;
            if (string.IsNullOrEmpty(build) || build == "unidentified") throw new ArgumentException("Build identity required");
            Segment(session);
            bool resume = !string.IsNullOrEmpty(runId);
            if (exportOnly && !resume) throw new ArgumentException("Export requires an existing run ID");
            if (!resume) runId = DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffZ") + "-" + Guid.NewGuid().ToString("N");
            Segment(runId);
            DirectoryPath = Path.Combine(root, "UAT", Milestone, runId);
            string locks = Path.Combine(root, "UATState", Milestone); Directory.CreateDirectory(locks);
            // Exclusive writer lock stays outside exports. OS releases it after a crash.
            lease = new FileStream(Path.Combine(locks, runId + ".lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            try {
                if (resume) {
                    if (!Directory.Exists(DirectoryPath)) throw new DirectoryNotFoundException("Unknown UAT run");
                    Manifest = Load();
                    if (Manifest.runId != runId || Manifest.milestone != Milestone || Manifest.schemaVersion != 1 || Manifest.campaign != campaign || Manifest.build != build)
                        throw new InvalidOperationException("Run campaign/build mismatch");
                    if (exportOnly) return;
                    if (Manifest.state != "incomplete") throw new InvalidOperationException("Finished/cancelled runs cannot resume");
                    foreach (var old in Manifest.sessions.Where(s => s.state == "active")) { old.state = "interrupted"; old.endedUtc = Now(); }
                } else {
                    if (Directory.Exists(DirectoryPath)) throw new IOException("Run collision");
                    Directory.CreateDirectory(DirectoryPath);
                    Manifest = new UatManifest { runId = runId, campaign = campaign, build = build, commit = build.Split('-')[0], startedUtc = Now() };
                    NewText(Path.Combine(DirectoryPath, "plan.txt"), Plan);
                }
                if (Manifest.sessions.Any(s => s.id == session)) throw new InvalidOperationException("Session already used");
                Directory.CreateDirectory(Path.Combine(DirectoryPath, "history"));
                string folder = Path.Combine(DirectoryPath, "sessions", session); Directory.CreateDirectory(folder);
                capture = new StreamWriter(new FileStream(Path.Combine(folder, "capture.jsonl"), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
                Manifest.sessions = Manifest.sessions.Concat(new[] { new UatSession { id = session, startedUtc = Now() } }).ToArray();
                Save();
            } catch { capture?.Dispose(); lease.Dispose(); throw; }
        }
        UatManifest Load() {
            // Immutable snapshots survive interruption during replacement of the convenience manifest.
            foreach (var path in Directory.GetFiles(Path.Combine(DirectoryPath, "history"), "*.json").OrderByDescending(p => p, StringComparer.Ordinal)) {
                try { var m = decode(File.ReadAllText(path)); if (m != null && m.schemaVersion == 1 && m.sessions != null && m.results != null) return m; } catch { }
            }
            throw new InvalidDataException("No valid manifest snapshot");
        }
        void ReplaceText(string name, string text) {
            string target = Path.Combine(DirectoryPath, name), temp = target + "." + Guid.NewGuid().ToString("N") + ".tmp";
            NewText(temp, text);
            if (File.Exists(target)) File.Replace(temp, target, null); else File.Move(temp, target);
        }
        void Save() {
            Manifest.updatedUtc = Now();
            // Never reuse an earlier revision, including an incomplete snapshot after interruption.
            string snapshot;
            do { ++Manifest.revision; snapshot = Path.Combine(DirectoryPath, "history", Manifest.revision.ToString("D10") + ".json"); } while (File.Exists(snapshot));
            string json = encode(Manifest); NewText(snapshot, json); ReplaceText("manifest.json", json);
            ReplaceText("results.json", encode(new UatResults { results = Manifest.results }));
            ReplaceText("observations.json", encode(new UatResults { results = Manifest.results }));
            ReplaceText("summary.txt", "Initial D diagnostic UAT\nRun: " + Manifest.runId + "\nBuild: " + Manifest.build + "\nState: " + Manifest.state +
                "\nCapture result: " + Manifest.captureResult + "\nIdentity result: " + Manifest.identityResult + "\nProduction multi-input / FFB: NOT TESTED\nSessions: " + Manifest.sessions.Length +
                "\n" + string.Join("\n", Manifest.results.Select(r => "Step " + r.step + " attempt " + r.attempt + ": capture=" + r.capture + ", identity=" + r.identity + "; " + r.actual)) + "\n");
        }
        void RequireActive() { if (!Active || Manifest.state != "incomplete") throw new InvalidOperationException("Run is not recording"); }
        public void Record(Record shareRecord) {
            RequireActive();
            if (shareRecord.session != CurrentSession || shareRecord.campaign != Manifest.campaign || shareRecord.build != Manifest.build) throw new InvalidOperationException("Record identity mismatch");
            capture.WriteLine(encode(shareRecord)); capture.Flush(); ((FileStream)capture.BaseStream).Flush(true);
            if (shareRecord.kind == "inventory") {
                var s = Manifest.sessions.Last(); ++s.inventories;
                s.latestInventoryValid = shareRecord.nativeBuild == Manifest.build && shareRecord.errors.Length == 0;
                if (s.latestInventoryValid) ++s.validInventories;
                Save();
            }
        }
        public void EnvironmentNote(string reviewedText, bool reviewed) {
            RequireActive(); Review(reviewedText, reviewed); Manifest.environment = reviewedText; Save();
        }
        static void Review(string text, bool reviewed) {
            if (!reviewed || string.IsNullOrWhiteSpace(text) || text.Length > 4000) throw new ArgumentException("Review nonempty shareable text first (maximum 4000 characters)");
        }
        public void Result(int step, string captureVerdict, string identityVerdict, string actual, string observation, bool reviewed) {
            RequireActive(); Review(actual, reviewed); Review(observation, reviewed);
            if (step < 1 || step > 2 || !Verdicts.Contains(captureVerdict) || !Verdicts.Contains(identityVerdict)) throw new ArgumentException("Invalid step/result");
            var s = Manifest.sessions.Last();
            if (captureVerdict == "PASS" && (!s.latestInventoryValid || string.IsNullOrWhiteSpace(Manifest.environment))) throw new InvalidOperationException("PASS requires matching-build inventory and reviewed setup notes");
            if (step == 2 && captureVerdict == "PASS" && !Manifest.results.Any(r => r.step == 1 && r.session != CurrentSession)) throw new InvalidOperationException("Restart needs a baseline in a different process session");
            var result = new UatResult { step = step, attempt = Manifest.results.Count(r => r.step == step) + 1, session = CurrentSession, utc = Now(), expected = Expected[step-1], actual = actual, observation = observation, capture = captureVerdict, identity = identityVerdict };
            Manifest.results = Manifest.results.Concat(new[] { result }).ToArray(); Save();
        }
        public void CloseSession(bool interrupted = false) {
            if (!Active) return;
            capture.Dispose(); capture = null;
            var s = Manifest.sessions.Last(); s.state = interrupted ? "interrupted" : "closed"; s.endedUtc = Now(); Save();
        }
        public void Finish(bool cancel) {
            RequireActive();
            if (!cancel && (!Manifest.results.Any(r => r.step == 1) || !Manifest.results.Any(r => r.step == 2))) throw new InvalidOperationException("Record both steps, including NOT TESTED if unavailable, before finishing");
            CloseSession(); Manifest.state = cancel ? "cancelled" : "completed"; Manifest.endedUtc = Now();
            var latest = new[] { 1, 2 }.Select(n => Manifest.results.LastOrDefault(r => r.step == n)).ToArray();
            Manifest.captureResult = cancel ? "NOT TESTED" : Overall(latest.Select(r => r.capture).ToArray());
            Manifest.identityResult = cancel ? "UNRESOLVED" : Overall(latest.Select(r => r.identity).ToArray()); Save();
        }
        static string Overall(string[] results) => results.Contains("FAIL") ? "FAIL" : results.All(r => r == "PASS") ? "PASS" : results.Contains("UNRESOLVED") ? "UNRESOLVED" : "NOT TESTED";
        public string Export() {
            if (Active) throw new InvalidOperationException("Save for restart, finish or cancel before export");
            string exports = Path.Combine(root, "UATExports"); Directory.CreateDirectory(exports);
            string path = Path.Combine(exports, Manifest.runId + "-" + Guid.NewGuid().ToString("N") + ".zip");
            // Explicit allowlist: never recurse through campaigns, build logs, temporary files or user additions.
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var zip = new ZipArchive(file, ZipArchiveMode.Create)) {
                Add(zip, "manifest.json", encode(Manifest)); Add(zip, "plan.txt", Plan);
                Add(zip, "results.json", encode(new UatResults { results = Manifest.results }));
                Add(zip, "summary.txt", File.ReadAllText(Path.Combine(DirectoryPath, "summary.txt")));
                Add(zip, "observations.json", encode(new UatResults { results = Manifest.results }));
                foreach (var s in Manifest.sessions) {
                    Segment(s.id); string relative = "sessions/" + s.id + "/capture.jsonl";
                    var entry = zip.CreateEntry(relative); using (var dest = entry.Open()) using (var source = File.OpenRead(Path.Combine(DirectoryPath, relative))) source.CopyTo(dest);
                }
            }
            return path;
        }
        static void Add(ZipArchive zip, string name, string text) { using (var w = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false))) w.Write(text); }
        public void Dispose() { try { CloseSession(); } finally { lease.Dispose(); } }
    }
    [Serializable] public sealed class UatResults { public UatResult[] results = Array.Empty<UatResult>(); }
}
