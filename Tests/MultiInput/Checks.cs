using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using B=Idas3ControlBindings;
static class Checks {
    static int count;static double time;
    static void Check(bool ok,string text){++count;if(!ok)throw new Exception(text);}
    static uint NoNative(uint slot,out Idas3Native.PadState state){state=default;return 1167;}
    static Joystick Device(int id,string name,float rest,bool button=false){var d=new Joystick{deviceId=id,path="/device"+id,description=new Description{interfaceName="HID",product=name}};AxisControl c=button?new ButtonControl():new AxisControl();c.path=d.path+"/axis";c.value=rest;d.allControls.Add(c);InputSystem.Add(d);return d;}
    static AxisControl Axis(InputDevice d)=>(AxisControl)d.allControls[0];
    static Idas3EndpointSnapshot Endpoint(Idas3ControllerDevices p,int id)=>p.Snapshot.Endpoints.Last(e=>e.Identity.Fields.Any(f=>f.Name=="runtimeId"&&f.Value==id.ToString()));
    static void Main(){
        string root=Path.Combine(Path.GetTempPath(),"id3-multi-"+Guid.NewGuid().ToString("N"));
        try{
            var wheel=Device(1,"Driver wheel name",0);var gas=Device(2,"Pedals via USB",1);var brake=Device(3,"Identical path",-1);var up=Device(4,"Shifter",0,true);var down=Device(5,"Shifter 2",0,true);
            using var provider=new Idas3ControllerDevices(()=>time,NoNative);provider.Initialize(root);
            var b=new B();b.Initialize(root);b.ApplyDraft();string original=File.ReadAllText(b.FilePath);b.BeginEdit();b.SetExperimentalDraftEnabled(true);
            void Poll(Func<KeyCode,bool> keys=null){time+=.02;provider.Tick(false);b.Poll(keys??(_=>false),new B.PadState{connected=true,buttons=0xF000,rightTrigger=255,thumbLX=30000},time,provider.Controls,provider.Snapshot);}
            bool Assign(B.ActionId action,InputDevice d,int direction,float rest){var e=Endpoint(provider,d.deviceId);return b.TrySetExperimentalControl(action,e.Token,e.ConnectionGeneration,"axis",direction,rest);}
            Poll();Check(Assign(B.ActionId.SteerLeft,wheel,-1,0)&&Assign(B.ActionId.SteerRight,wheel,1,0),"paired steering from one endpoint");
            Check(Assign(B.ActionId.Accelerate,gas,-1,1)&&Assign(B.ActionId.Brake,brake,1,-1),"opposite resting pedals on distinct USB endpoints");
            Check(Assign(B.ActionId.ShiftUp,up,1,0)&&Assign(B.ActionId.ShiftDown,down,1,0),"independent shift devices");
            Check(!Assign(B.ActionId.Camera,up,1,0)&&b.LastError.Contains("Shift up"),"same endpoint conflict explicit");
            Check(File.ReadAllText(b.FilePath)==original,"experimental drafts do not write legacy file");
            var checkpoint=b.SaveDraftCheckpoint();b.ClearDraft(B.ActionId.Accelerate,B.Slot.Controller);b.RestoreDraftCheckpoint(checkpoint);
            Check(b.HasControllerAssignment(B.ActionId.Accelerate),"setup checkpoint includes multi-input source assignment");
            Check(b.ApplyExperimentalDraft(),"separate experimental save");Poll();
            Check(File.ReadAllText(b.FilePath)==original,"saving experimental does not migrate legacy mappings");
            string json=File.ReadAllText(b.ExperimentalFilePath);Check(!json.Contains(provider.Snapshot.Session.ToString())&&!json.Contains("runtimePath")&&!json.Contains("generation"),"session handles not persisted as identity");
            Axis(wheel).value=-.5f;Axis(gas).value=0;Axis(brake).value=0;Axis(up).value=1;Poll();
            var frame=new Idas3Native.FrameInput{padButtons=0xFFFF,rightTrigger=255,thumbLX=30000};b.ApplyDriving(ref frame);
            Check(frame.thumbLX==-16384&&frame.rightTrigger==128&&frame.leftTrigger==128,"simultaneous proportional steering and both pedals");
            Check(frame.padButtons==0x2000,"only assigned shift contributes; active-device raw bits suppressed");
            var preview=b.EvaluateDraftDriving();Check(frame.thumbLX==preview.thumbLX&&frame.rightTrigger==preview.rightTrigger&&frame.leftTrigger==preview.leftTrigger&&frame.padButtons==preview.padButtons,"gameplay and Test Controls share evaluated assignments");
            var menuFrame=default(Idas3Native.FrameInput);b.ApplyMenu(ref menuFrame,false);
            Check(menuFrame.padConnected==1&&menuFrame.padButtons==0&&(menuFrame.key0&(1u<<8))==0,"driving bindings never implicitly generate menu Back");
            var historical=provider.Snapshot;time+=1;b.Poll(_=>false,default,time,null,historical);frame=default;b.ApplyDriving(ref frame);
            Check(frame.thumbLX==0&&frame.rightTrigger==0&&frame.padButtons==0,"aged samples neutralized explicitly");
            Poll();b.Poll(_=>false,default,time,null,historical);frame=default;b.ApplyDriving(ref frame);Check(frame.rightTrigger==0,"out-of-order frame cannot revive earlier samples");
            Axis(wheel).value=0;Axis(gas).value=1;Axis(brake).value=-1;Axis(up).value=0;Poll();
            Axis(wheel).value=-.5f;Axis(gas).value=0;Axis(brake).value=0;Axis(up).value=1;Poll();
            InputSystem.Remove(gas);Poll();frame=default;b.ApplyDriving(ref frame);Check(frame.rightTrigger==0&&frame.leftTrigger==128&&frame.thumbLX==-16384,"disconnect affects only assigned endpoint actions");
            InputSystem.Add(gas);Poll();frame=default;b.ApplyDriving(ref frame);Check(frame.rightTrigger==0&&b.BindingName(B.ActionId.Accelerate,B.Slot.Controller,false).Contains("reassign"),"reconnect never silently reassigned");
            Check(Assign(B.ActionId.Accelerate,gas,-1,1)&&b.ApplyExperimentalDraft(),"explicit reassignment");Poll();frame=default;b.ApplyDriving(ref frame);Check(frame.rightTrigger==0&&frame.padButtons==0,"save while held does not create action edges");
            preview=b.EvaluateDraftDriving();Check(preview.rightTrigger==frame.rightTrigger&&preview.padButtons==frame.padButtons,"preview retains same saved release guard");
            Axis(gas).value=1;Axis(up).value=0;Axis(brake).value=-1;Axis(wheel).value=0;Poll();Axis(gas).value=-1;Axis(up).value=1;Poll();frame=default;b.ApplyDriving(ref frame);Check(frame.rightTrigger==255&&frame.padButtons==0x2000,"release then fresh action accepted");
            Axis(gas).value=float.NaN;Poll();frame=default;b.ApplyDriving(ref frame);Check(frame.rightTrigger==0,"invalid input is neutral, not raw-zero half throttle");
            Axis(gas).value=-1;Poll();frame=default;b.ApplyDriving(ref frame);Check(frame.rightTrigger==0,"invalid-to-valid held sample remains guarded");Axis(gas).value=1;Poll();
            InputSystem.Remove(wheel);Poll(k=>k==KeyCode.W||k==KeyCode.A);frame=default;b.ApplyDriving(ref frame);Check((frame.key2&(1u<<(87&31)))!=0&&(frame.key2&(1u<<(65&31)))!=0,"keyboard recovery independent of missing wheel");
            var loaded=new B();loaded.Initialize(root);loaded.Poll(_=>false,default,time,null,provider.Snapshot);frame=default;loaded.ApplyDriving(ref frame);
            Check(loaded.ExperimentalEnabled&&loaded.BindingName(B.ActionId.Accelerate,B.Slot.Controller,false).Contains("reassign")&&frame.rightTrigger==0,"restart preserves preferences but never session association");
            b.BeginEdit();b.SetExperimentalDraftEnabled(false);b.CancelEdit(false);Check(b.ExperimentalDraftEnabled&&b.ExperimentalEnabled,"cancel mode toggle preserves current experimental state");
            b.SetExperimentalDraftEnabled(false);Check(b.ApplyExperimentalDraft()&&!b.ExperimentalEnabled&&!b.ExperimentalBlocksFeedback,"explicit return to existing controls");Check(File.ReadAllText(b.FilePath)==original,"return preserves original configuration bytes");
            b.BeginEdit();b.SetExperimentalDraftEnabled(true);Poll();
            var preserved=b.SaveDraftCheckpoint();b.BeginCapture(B.ActionId.Camera,B.Slot.Controller,time);Poll();Axis(brake).value=1;Axis(down).value=1;Poll();
            Check(b.IsCapturing&&b.CaptureError!=null&&b.CaptureError.Contains("one control"),"simultaneous capture rejected instead of enumeration guess");b.CancelCapture();Axis(brake).value=-1;Axis(down).value=0;Axis(up).value=0;Poll();
            b.BeginCapture(B.ActionId.Camera,B.Slot.Controller,time);Poll();Axis(down).value=1;Poll();Check(b.IsCapturing&&b.CaptureError.Contains("Shift down"),"shared capture detects per-source conflicts");b.CancelCapture();Poll();b.ClearDraft(B.ActionId.ShiftDown,B.Slot.Controller);Axis(down).value=0;Poll();b.BeginCapture(B.ActionId.Camera,B.Slot.Controller,time);Poll();Axis(down).value=1;Poll();Check(!b.IsCapturing&&b.BindingName(B.ActionId.Camera,B.Slot.Controller).Contains("Shifter 2"),"existing capture records actual source device");
            b.RestoreDraftCheckpoint(preserved);Check(!b.HasControllerAssignment(B.ActionId.Camera),"cancel setup restores previous multi-input draft");
            string savedExperimental=File.ReadAllText(b.ExperimentalFilePath);File.Delete(b.ExperimentalFilePath);Directory.CreateDirectory(b.ExperimentalFilePath);
            Check(!b.ApplyExperimentalDraft()&&b.HasExperimentalChanges&&b.ExperimentalDraftEnabled,"persistence failure preserves pending assignments/mode");Directory.Delete(b.ExperimentalFilePath);File.WriteAllText(b.ExperimentalFilePath,savedExperimental);
            Check(b.ApplyExperimentalDraft()&&b.ExperimentalBlocksFeedback,"retry succeeds; experimental force output stays disabled");
            b.SetExperimentalDraftEnabled(false);File.Delete(b.ExperimentalFilePath);Directory.CreateDirectory(b.ExperimentalFilePath);
            int laterWrites=0;
            var saveResult=Idas3ControllerSave.Save(()=>true,()=>null,()=>{++laterWrites;return true;},()=>null,()=>{++laterWrites;return true;},()=>null,b.ApplyExperimentalDraft,()=>b.ExperimentalError);
            Check(!saveResult.Complete&&saveResult.BindingsSaved&&laterWrites==0&&b.HasExperimentalChanges&&b.ExperimentalEnabled&&!b.ExperimentalDraftEnabled,"combined partial save retains multi-input draft, stops device/FFB writes, and reports earlier persistence");
            b.CancelEdit(false);Check(b.ExperimentalDraftEnabled&&!b.HasExperimentalChanges,"explicit discard after partial multi-input failure restores saved state");
            Directory.Delete(b.ExperimentalFilePath);File.WriteAllText(b.ExperimentalFilePath,savedExperimental);
            Axis(brake).value=-1;Axis(up).value=0;Axis(down).value=0;Axis(gas).value=1;Poll();b.CancelEdit();Poll();
            Check(!b.SuppressInput,"unassigned legacy raw pad cannot block experimental release recovery");
            Poll();for(int i=0;i<100;++i){b.Poll(_=>false,default,time,null,provider.Snapshot);frame=default;b.ApplyDriving(ref frame);}
            Func<KeyCode,bool> noKeys=_=>false;long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();for(int i=0;i<10000;++i){b.Poll(noKeys,default,time,null,provider.Snapshot);frame=default;b.ApplyDriving(ref frame);}timer.Stop();
            Console.WriteLine("PERF multi-input binding integration (same immutable frame, 5-device inventory): "+((GC.GetAllocatedBytesForCurrentThread()-bytes)/10000.0).ToString("F1")+" bytes/poll+evaluate, "+(timer.Elapsed.TotalMilliseconds*1000/10000).ToString("F2")+" us; synthetic, excludes provider snapshot allocations.");
            for(int n=0;n<8;++n){var device=Device(100+n,"Benchmark HID "+n,0);for(int c=1;c<32;++c)device.allControls.Add(new AxisControl{path=device.path+"/axis"+c,value=0});}
            using(var many=new Idas3ControllerDevices(()=>time,NoNative,d=>d.deviceId>=100)){
                many.Initialize(Path.Combine(root,"benchmark"));var integrated=new B();integrated.Initialize(Path.Combine(root,"benchmark"));integrated.BeginEdit();integrated.SetExperimentalDraftEnabled(true);
                integrated.Poll(noKeys,default,time,null,many.Snapshot);var source=Endpoint(many,100);
                Check(integrated.TrySetExperimentalControl(B.ActionId.Accelerate,source.Token,source.ConnectionGeneration,"axis",1,0)&&integrated.ApplyExperimentalDraft(),"larger inventory shares production assignment path");
                for(int i=0;i<100;++i){many.Tick(false);integrated.Poll(noKeys,default,time,many.Controls,many.Snapshot);frame=default;integrated.ApplyDriving(ref frame);}
                bytes=GC.GetAllocatedBytesForCurrentThread();timer.Restart();
                for(int i=0;i<10000;++i){many.Tick(false);integrated.Poll(noKeys,default,time,many.Controls,many.Snapshot);frame=default;integrated.ApplyDriving(ref frame);}timer.Stop();
                Console.WriteLine("PERF complete provider + multi-input integration (8 devices x 32 controls): "+((GC.GetAllocatedBytesForCurrentThread()-bytes)/10000.0).ToString("F1")+" bytes/frame, "+(timer.Elapsed.TotalMilliseconds*1000/10000).ToString("F2")+" us; portable adapters, not Unity/Windows.");
            }
            count += MenuNavigationChecks.Run(Path.Combine(root, "menu"));
            count += OwnershipChecks.Run(Path.Combine(root, "ownership"));
            Console.WriteLine("PASS "+count+" production multi-input checks; no Unity/Windows/hardware claim.");
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
