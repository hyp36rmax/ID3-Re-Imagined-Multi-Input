using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using Id3.IdentityProbe;

public static class ProbeBuild
{
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
    }
}
