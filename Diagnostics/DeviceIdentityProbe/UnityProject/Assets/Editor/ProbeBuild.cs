using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Id3.IdentityProbe;

public static class ProbeBuild
{
    [DllImport("Id3IdentityInventory",CallingConvention=CallingConvention.Cdecl)] static extern int ProbeCapture(out IntPtr data,out int length);
    [Serializable] sealed class SelfTest {public bool passed=true,nativeIdentityAgreement=true,uatRestartExport=true;public string identity,unity,input;}
    static void CheckNativeAndUat(string output) {
        var expected=JsonUtility.FromJson<ProbePlayer.BuildIdentity>(File.ReadAllText("Assets/Resources/probe-build.json"));
        if(ProbeCapture(out var data,out int length)!=1||data==IntPtr.Zero||length<2||length>16*1024*1024)throw new InvalidOperationException("Native inventory self-test failed.");
        var bytes=new byte[length];Marshal.Copy(data,bytes,0,length);
        var inventory=JsonUtility.FromJson<Inventory>(Encoding.UTF8.GetString(bytes));
        if(inventory==null||inventory.nativeBuild!=expected.identity)throw new InvalidOperationException("Native/managed build identity mismatch.");
        // Do not publish the hosted runner's raw inventory. Only the equality verdict.
        string root=Path.Combine(Path.GetTempPath(),"id3-unity-uat-"+Guid.NewGuid().ToString("N")),id;
        try {
            using(var r=new UatRun(root,null,"synthetic",expected.identity,"baseline",o=>JsonUtility.ToJson(o),j=>JsonUtility.FromJson<UatManifest>(j))){
                id=r.Manifest.runId;r.Record(new Record{session="baseline",campaign="synthetic",build=expected.identity,nativeBuild=expected.identity,kind="inventory",evidenceClass="synthetic"});
                r.Result(1,"PASS","UNRESOLVED","Synthetic baseline","No hardware",true);r.CloseSession();
            }
            using(var r=new UatRun(root,id,"synthetic",expected.identity,"restart",o=>JsonUtility.ToJson(o),j=>JsonUtility.FromJson<UatManifest>(j))){
                if(r.Manifest.sessions.Length!=2)throw new InvalidOperationException("UAT restart did not attach.");
                r.Record(new Record{session="restart",campaign="synthetic",build=expected.identity,nativeBuild=expected.identity,kind="inventory",evidenceClass="synthetic"});
                r.Result(2,"PASS","UNRESOLVED","Synthetic restart","No hardware",true);r.Finish(false);
                using(var zip=ZipFile.OpenRead(r.Export()))if(zip.GetEntry("manifest.json")==null||zip.GetEntry("sessions/restart/capture.jsonl")==null)throw new InvalidOperationException("UAT export incomplete.");
            }
        } finally {if(Directory.Exists(root))Directory.Delete(root,true);}
        File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build-self-test.json"),JsonUtility.ToJson(new SelfTest{identity=expected.identity,unity=Application.unityVersion,input=InputSystem.version.ToString()},true));
    }
    public static void WindowsPlayer()
    {
        if (Application.unityVersion != "6000.6.0f1" || InputSystem.version.ToString() != "1.19.0")
            throw new InvalidOperationException("Expected Unity 6000.6.0f1 and Input System 1.19.0.");
        string output = Environment.GetEnvironmentVariable("ID3_PROBE_PLAYER");
        if (string.IsNullOrWhiteSpace(output)) throw new InvalidOperationException("ID3_PROBE_PLAYER is required.");
        if (!File.Exists("Assets/Plugins/x86_64/Id3IdentityInventory.dll") || !File.Exists("Assets/Resources/probe-build.json"))
            throw new InvalidOperationException("Run Build-Probe.ps1 first.");
        PlayerSettings.companyName = "ID3 Diagnostics"; PlayerSettings.productName = "Device Identity Probe";
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);
        PlayerSettings.defaultScreenWidth = 1100; PlayerSettings.defaultScreenHeight = 760;
        PlayerSettings.fullScreenMode = FullScreenMode.Windowed; PlayerSettings.runInBackground = true;
        PlayerSettings.resizableWindow = true;
        var importer = (PluginImporter)AssetImporter.GetAtPath("Assets/Plugins/x86_64/Id3IdentityInventory.dll");
        importer.SetCompatibleWithAnyPlatform(false); importer.SetCompatibleWithEditor(true);
        importer.SetEditorData("OS", "Windows"); importer.SetEditorData("CPU", "x86_64");
        importer.SetCompatibleWithPlatform(BuildTarget.StandaloneWindows64, true);
        importer.SetPlatformData(BuildTarget.StandaloneWindows64, "CPU", "x86_64"); importer.SaveAndReimport();
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        new GameObject("Identity probe only").AddComponent<ProbePlayer>();
        var camera = new GameObject("Camera").AddComponent<Camera>(); camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.08f, .08f, .1f);
        EditorSceneManager.SaveScene(scene, "Assets/Probe.unity");
        // Exercise Unity's actual serializer during the Windows build, not just the portable test serializer.
        var check = new Record { endpoints = new[] { new Endpoint { fields = new[] { Field.Present("serial", "synthetic", "build-self-test") } } } };
        if (JsonUtility.FromJson<Record>(JsonUtility.ToJson(check)).endpoints[0].fields[0].value != "synthetic")
            throw new InvalidOperationException("JsonUtility round-trip failed.");
        var uatCheck = new UatManifest { runId = "synthetic", sessions = new[] { new UatSession { id = "synthetic-session" } },
            results = new[] { new UatResult { step = 1, capture = "PASS", identity = "UNRESOLVED" } } };
        var uatRoundTrip = JsonUtility.FromJson<UatManifest>(JsonUtility.ToJson(uatCheck));
        if (uatRoundTrip.sessions[0].id != "synthetic-session" || uatRoundTrip.results[0].identity != "UNRESOLVED")
            throw new InvalidOperationException("UAT JsonUtility round-trip failed.");
        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = new[] { "Assets/Probe.unity" }, locationPathName = output,
            target = BuildTarget.StandaloneWindows64, options = BuildOptions.None });
        if (report.summary.result != BuildResult.Succeeded) throw new InvalidOperationException("Probe build failed: " + report.summary.result);
        CheckNativeAndUat(output);
    }
}
