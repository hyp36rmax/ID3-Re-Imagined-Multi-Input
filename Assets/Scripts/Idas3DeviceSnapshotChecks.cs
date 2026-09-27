#if UNITY_EDITOR
using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Explicit editor test; not called during play. No native/force output or save migration.
public static class Idas3DeviceSnapshotChecks
{
    private static void Check(bool value,string reason){if(!value)throw new InvalidOperationException(reason);}
    private static uint NoXInput(uint slot,out Idas3Native.PadState pad){pad=default;return 1167;}
    public static void Run(){
        string root=Path.Combine(Path.GetTempPath(),"id3-snapshot-unity-"+Guid.NewGuid().ToString("N"));
        var a=InputSystem.AddDevice<Gamepad>();var b=InputSystem.AddDevice<Gamepad>();
        using(var provider=new Idas3ControllerDevices(()=>Time.realtimeSinceStartupAsDouble,NoXInput,d=>d==a||d==b)){
            try{
                InputSystem.QueueStateEvent(a,new GamepadState{leftStick=new Vector2(-.5f,0)});
                InputSystem.QueueStateEvent(b,new GamepadState{leftStick=new Vector2(.75f,0)});InputSystem.Update();provider.Initialize(root);
                Idas3EndpointSnapshot ea=null,eb=null;
                foreach(var endpoint in provider.Snapshot.Endpoints)foreach(var field in endpoint.Identity.Fields){
                    if(field.Name!="runtimeId")continue;
                    if(field.Value==a.deviceId.ToString())ea=endpoint;
                    if(field.Value==b.deviceId.ToString())eb=endpoint;
                }
                Check(ea!=null&&eb!=null&&!ea.Token.Equals(eb.Token),"Two actual Unity endpoints must remain separate");
                Check(ea.TryGetControl("leftStick/x",out var av)&&eb.TryGetControl("leftStick/x",out var bv)&&Math.Abs(av.Value.Value+.5f)<.001f&&Math.Abs(bv.Value.Value-.75f)<.001f,"Read unprocessed Unity state with identical local paths");
                var retained=provider.Snapshot;InputSystem.RemoveDevice(a);
                Check(!provider.TryReadCurrent(ea.Token,ea.ConnectionGeneration,"leftStick/x",out _)&&!provider.IsCurrent(retained),"Unity removal immediately invalidates old live handle");
                Check(ea.Controls.Count>0&&av.Value==-.5f,"Historical sample remains unchanged");
                InputSystem.AddDevice(a);InputSystem.QueueStateEvent(a,new GamepadState());InputSystem.Update();provider.Tick(false);
                Check(provider.Snapshot.TryGetEndpoint(ea.Token,out var returned)&&returned.ConnectionGeneration>ea.ConnectionGeneration,"Same Unity object reconnect increments connection generation");
                Check(returned.TryGetControl("leftStick/x",out var zero)&&zero.Value==0&&zero.Validity==Idas3SampleValidity.Valid,"Valid neutral input is not unavailable");
                Directory.CreateDirectory("Verification/device-snapshots");
                File.WriteAllText("Verification/device-snapshots/unity-checks.txt","PASS real Unity synthetic state-buffer/removal/reconnect checks; no physical hardware or force output.\n");
                Debug.Log("PASS device snapshots Unity synthetic checks");
            }finally{
                if(a.added)InputSystem.RemoveDevice(a);if(b.added)InputSystem.RemoveDevice(b);
                if(Directory.Exists(root))Directory.Delete(root,true);
            }
        }
    }
}
#endif
