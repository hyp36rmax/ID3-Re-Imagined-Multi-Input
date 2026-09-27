using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Id3.IdentityProbe;

static class UatTests {
    static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    static string Encode(object o) => JsonSerializer.Serialize(o, o.GetType(), Options);
    static UatManifest Decode(string s) => JsonSerializer.Deserialize<UatManifest>(s, Options);
    const string Build = "6bdfb9240ed0d751ac8d7999995d43555f1fcd80-synthetic", Campaign = "synthetic-campaign-token";
    static int checks;
    static void Check(bool yes, string what) { if (!yes) throw new Exception(what); ++checks; }
    static void Reject(Action a, string what) { bool rejected = false; try { a(); } catch { rejected = true; } Check(rejected, what); }
    static UatRun Open(string root, string run = null, string session = "session-a", string campaign = Campaign, string build = Build) => new UatRun(root, run, campaign, build, session, Encode, Decode);
    static Record Inventory(string session = "session-a", string native = Build) => new Record { evidenceClass = "synthetic", session = session, campaign = Campaign, build = Build, nativeBuild = native, kind = "inventory" };
    static void Pass(UatRun r, int step) => r.Result(step, "PASS", "UNRESOLVED", "Synthetic expected records present", "Synthetic identity remains ambiguous", true);
    public static void CrashChild(string root) {
        var r = Open(root); File.WriteAllText(Path.Combine(root, "child-id"), r.Manifest.runId);
        r.Record(Inventory());
        Environment.Exit(0); // Deliberately skips Dispose / normal session end, like abrupt process loss.
    }
    public static void Run() {
        string root = Path.Combine(Path.GetTempPath(), "id3-uat-synthetic-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(root);
        try {
            string id, firstPath;
            using (var r = Open(root)) {
                id = r.Manifest.runId; firstPath = r.DirectoryPath;
                using (var other = Open(root, session: "other")) Check(other.DirectoryPath != firstPath, "separate runs unique");
                Check(r.Manifest.build == Build && r.Manifest.commit == Build.Split('-')[0], "exact build/commit");
                Check(r.Manifest.state == "incomplete" && r.Manifest.captureResult != "PASS", "new run cannot pass");
                Reject(() => Open(root, id, "parallel"), "exclusive run writer");
                Reject(() => Pass(r, 1), "PASS without inventory refused");
                Reject(() => r.EnvironmentNote("PRIVATE-UNREVIEWED", false), "unreviewed setup refused");
                r.EnvironmentNote("Synthetic wheel model / OS / driver / default settings", true);
                r.Record(Inventory(native: "wrong")); Reject(() => Pass(r, 1), "mismatched native identity cannot pass capture");
                r.Record(Inventory()); Pass(r, 1);
                r.Record(Inventory(native: "wrong-later")); Reject(() => Pass(r, 1), "latest mismatched inventory cannot pass");
                r.Record(Inventory());
                Reject(() => Pass(r, 2), "restart cannot use baseline session");
                Reject(() => r.Result(1,"PASS","PASS","PRIVATE-UNREVIEWED","raw",false), "unreviewed result refused");
                Reject(() => r.Record(Inventory("wrong-session")), "record identity check");
                Reject(() => r.Finish(false), "missing step prevents finish");
                Reject(() => r.Export(), "active export refused");
                Check(r.Manifest.captureResult == "NOT TESTED", "partial PASS never becomes overall PASS");
                r.CloseSession();
                string z = r.Export(); using (var zip = ZipFile.OpenRead(z)) Check(zip.GetEntry("manifest.json") != null, "partial export available");
            }
            Reject(() => Open(root, id, "session-b", "wrong-campaign"), "campaign mismatch refused");
            Reject(() => Open(root, id, "session-b", build: "other-build"), "build mismatch refused");
            using (var r = Open(root, id, "session-b")) {
                Check(r.Manifest.sessions.Length == 2 && r.Manifest.sessions[0].state == "closed", "restart attached previous closed session");
                Check(r.Manifest.results.Length == 1 && r.Manifest.campaign == Campaign, "results and campaign preserved");
                r.Record(Inventory("session-b")); Pass(r, 2);
                r.Result(2,"UNRESOLVED","UNRESOLVED","Synthetic retry","Synthetic insufficient evidence",true);
                Pass(r, 2);
                Check(r.Manifest.results.Length == 4 && r.Manifest.results.Last().attempt == 3, "retries append not overwrite");
                r.Finish(false);
                Check(r.Manifest.state == "completed" && r.Manifest.captureResult == "PASS" && r.Manifest.identityResult == "UNRESOLVED", "capture and identity distinct");
                Directory.CreateDirectory(Path.Combine(root,"Campaigns","default","private"));
                File.WriteAllText(Path.Combine(root,"Campaigns","default","campaign-key.secret"),"PRIVATE-KEY");
                File.WriteAllText(Path.Combine(r.DirectoryPath,"Unity.log"),"PRIVATE-RAW");
                File.WriteAllText(Path.Combine(r.DirectoryPath,"secret.extra"),"PRIVATE-UNREVIEWED");
                string z = r.Export(), z2 = r.Export(); Check(z != z2, "exports never overwrite");
                using (var zip = ZipFile.OpenRead(z)) {
                    Check(zip.Entries.Count == 7 && zip.GetEntry("observations.json") != null && zip.GetEntry("plan.txt") != null && zip.GetEntry("results.json") != null, "exact export allowlist");
                    Check(zip.GetEntry("sessions/session-a/capture.jsonl") != null && zip.GetEntry("sessions/session-b/capture.jsonl") != null, "both sessions packaged");
                    foreach (var e in zip.Entries) using (var sr = new StreamReader(e.Open())) Check(!sr.ReadToEnd().Contains("PRIVATE-"), "no private data in " + e.FullName);
                    string restore = Path.Combine(root,"restore"); zip.ExtractToDirectory(restore);
                    Check(Decode(File.ReadAllText(Path.Combine(restore,"manifest.json"))).captureResult == "PASS", "export restores with state");
                }
            }
            Reject(() => Open(root,id,"session-c"), "completed run immutable");
            using (var view = new UatRun(root,id,Campaign,Build,"export-only",Encode,Decode,true)) {
                Check(!view.Active && view.Manifest.sessions.Length == 2 && view.Manifest.state == "completed", "export-only does not attach or alter terminal run");
                Check(File.Exists(view.Export()), "terminal export available after relaunch");
            }
            using (var r = Open(root,session:"cancel")) {
                r.EnvironmentNote("Synthetic setup",true); r.Record(Inventory("cancel")); Pass(r,1); r.Finish(true);
                Check(r.Manifest.state == "cancelled" && r.Manifest.captureResult == "NOT TESTED", "cancelled cannot appear passed");
                Reject(() => Open(root,r.Manifest.runId,"new"), "cancelled run cannot resume");
            }
            foreach (string verdict in new[] { "NOT TESTED", "FAIL", "UNRESOLVED" }) {
                using (var r = Open(root,session:"verdict-check")) {
                    r.Result(1,verdict,"UNRESOLVED","Synthetic actual","Synthetic observation",true);
                    r.Result(2,"NOT TESTED","NOT TESTED","Restart not performed","Synthetic unavailable",true);
                    r.Finish(false);
                    Check(r.Manifest.state == "completed" && r.Manifest.captureResult == verdict, "completed does not imply pass: " + verdict);
                }
            }
            string crashRoot = Path.Combine(root,"crash"); Directory.CreateDirectory(crashRoot);
            var start = new ProcessStartInfo(Environment.ProcessPath) { UseShellExecute = false };
            start.ArgumentList.Add("--uat-crash"); start.ArgumentList.Add(crashRoot);
            using (var child = Process.Start(start)) { child.WaitForExit(); Check(child.ExitCode == 0, "abrupt child exits"); }
            string crashId = File.ReadAllText(Path.Combine(crashRoot,"child-id"));
            string history = Path.Combine(crashRoot,"UAT",UatRun.Milestone,crashId,"history");
            File.WriteAllText(Path.Combine(history,"0000000999.json"),"{partial");
            using (var r = Open(crashRoot,crashId,"recovered")) {
                Check(r.Manifest.sessions[0].state == "interrupted" && r.Manifest.state == "incomplete", "crash recovered without pass");
                Check(r.Manifest.revision > 2, "snapshot recovery advances revision");
                Check(File.ReadAllText(Path.Combine(r.DirectoryPath,"sessions","session-a","capture.jsonl")).Contains("synthetic"), "flushed evidence survived process loss");
                r.CloseSession(); using (var zip=ZipFile.OpenRead(r.Export())) Check(zip.GetEntry("sessions/session-a/capture.jsonl") != null,"interrupted evidence exported");
            }
            Reject(() => Open(root,"../escape"), "path traversal refused");
            Console.WriteLine("PASS: " + checks + " synthetic UAT storage/lifecycle/export checks; no Windows or Unity execution.");
        } finally { Directory.Delete(root,true); }
    }
}
