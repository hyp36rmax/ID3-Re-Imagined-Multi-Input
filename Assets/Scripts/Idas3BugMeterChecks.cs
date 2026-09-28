using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;

public static class Idas3BugMeterChecks
{
    public static void Run(){
        Debug.Log(Idas3MeterSignalChecks.RunChecks());
        string output=Path.GetFullPath("Verification/discord-bugs-20260927/meter-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss"));Directory.CreateDirectory(output);
        int checks=0;void Check(bool ok,string why){++checks;if(!ok)throw new Exception(why);}
        var go=new GameObject("Meter palette GPU check");var camera=go.AddComponent<Camera>();camera.enabled=false;camera.cullingMask=0;
        camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=Color.black;camera.allowHDR=camera.allowMSAA=false;
        var target=new RenderTexture(1280,720,24,RenderTextureFormat.ARGB32);target.Create();camera.targetTexture=target;
        var pixels=new Texture2D(1280,720,TextureFormat.RGB24,false);var commands=new CommandBuffer();camera.AddCommandBuffer(CameraEvent.BeforeForwardOpaque,commands);
        try{
            foreach(int id in new[]{0,1,3,4,8,9,10,11,12,13,14,15,19,29,35,37,57,63,68,117}){
                var meter=Idas3ArcadeMeterCatalog.Get(id+2);var original=meter.layers;
                meter.layers=original.Where(l=>l.role=="speed1").ToArray();
                try{
                    foreach(int band in new[]{0,1,2,3})using(var renderer=new Idas3ArcadeHud()){
                        float speed=new[]{88f,100f,180f,220f}[band];var options=new Idas3GameOptions.Values{hudMeterStyle=id+2};
                        commands.Clear();renderer.Build(options,new Idas3ArcadeHud.Telemetry{size=40,version=4,flags=1|(6u<<16),revLimit=8500,speedKmh=speed,gear=5},1280,720,0,true,out _);
                        renderer.Render(commands,1280,720);camera.Render();var previous=RenderTexture.active;
                        try{RenderTexture.active=target;pixels.ReadPixels(new Rect(0,0,1280,720),0,0);pixels.Apply();}finally{RenderTexture.active=previous;}
                        int red=0,yellow=0,blue=0;
                        foreach(var p in pixels.GetPixels32()){
                            if(p.r>80&&p.r>p.g*2&&p.r>p.b*2)++red;
                            if(p.r>80&&p.g>70&&p.b<Math.Min(p.r,p.g)*.5f)++yellow;
                            if(p.b>80&&p.b>p.r*2)++blue;
                        }
                        Check((band==1?yellow:band==2?blue:red)>10,$"{meter.name} lacks visible palette band {band} (red={red},yellow={yellow},blue={blue})");
                        File.WriteAllBytes(Path.Combine(output,$"{id}-{band}.png"),pixels.EncodeToPNG());
                    }
                }finally{meter.layers=original;commands.Clear();}
            }
            foreach(int id in new[]{1,12,13,35,37,57,117}){
                var meter=Idas3ArcadeMeterCatalog.Get(id+2);var layer=Array.Find(meter.layers,l=>l.role=="gear");
                var t=new Idas3ArcadeHud.Telemetry{version=4,flags=6u<<16,gear=5};
                Check(Idas3ImportedMeter.GearDigit(layer,t)==7,meter.name+" six-speed fifth gear should use silver cell");
                t.flags=5u<<16;Check(Idas3ImportedMeter.GearDigit(layer,t)==5,meter.name+" five-speed fifth gear lost gold cell");
                t.gear=6;t.flags=6u<<16;Check(Idas3ImportedMeter.GearDigit(layer,t)==6,meter.name+" sixth gear lost gold cell");
            }
            Idas8ImportedShadowChecks.Gpu(Check,output);
            File.WriteAllText(Path.Combine(output,"PASS.txt"),checks+" GPU palette, maximum-gear and imported shadow checks passed");Debug.Log("PASS "+checks+" meter/shadow bug checks: "+output);
        }finally{camera.RemoveAllCommandBuffers();commands.Dispose();camera.targetTexture=null;target.Release();UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(pixels);UnityEngine.Object.DestroyImmediate(go);}
    }
}
