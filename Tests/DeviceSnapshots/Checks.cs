using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.XInput;
using B=Idas3ControlBindings;
static class Checks {
    static int checks,polls;static double clock;static uint nativeResult=1167;static bool missingNative;
    static uint Native(uint slot,out Idas3Native.PadState state){++polls;state=default;if(missingNative)throw new DllNotFoundException();if(slot!=0)return 1167;state.gamepad=new Idas3Native.GamepadState{thumbLX=-16384,rightTrigger=128,buttons=0x2000};return nativeResult;}
    static void Check(bool ok,string why){++checks;if(!ok)throw new Exception(why);}
    static Joystick Device(int id,string name,float value){
        var device=new Joystick{deviceId=id,path="/d"+id,displayName=name,description=new Description{product=name,interfaceName="HID",serial="duplicate",capabilities="{\"vendorId\":123,\"productId\":456}"}};
        device.allControls.Add(new AxisControl{path=device.path+"/axis",name="axis",value=value});return device;
    }
    static AxisControl Axis(InputDevice device)=>(AxisControl)device.allControls[0];
    static Idas3EndpointSnapshot Find(Idas3ControllerDevices p,int id)=>p.Snapshot.Endpoints.Single(d=>d.Identity.Fields.Any(f=>f.Name=="runtimeId"&&f.Value==id.ToString()));
    static void Tick(Idas3ControllerDevices p,double delta=.01){clock+=delta;p.Tick(false);}
    static void LegacyCompatibility(string root){
        InputSystem.devices.Clear();missingNative=false;nativeResult=1167;
        var a=Device(101,"wheel",.2f);var b=Device(102,"pedals",-.5f);
        Axis(a).path=a.path+"/stick/x";Axis(b).path=b.path+"/stick/y";
        var mirror=new XInputControllerWindows{deviceId=103,path="/xinput",description=new Description{interfaceName="XInput",product="mirror"}};
        InputSystem.Add(a);InputSystem.Add(b);InputSystem.Add(mirror);
        using var current=new Idas3ControllerDevices(()=>clock,Native);
        using var baseline=new BaselineControllerDevices(()=>clock,Native);
        current.Initialize(Path.Combine(root,"current"));baseline.Initialize(Path.Combine(root,"baseline"));
        void Compare(string why){
            Check(current.TryRead(out var x)==baseline.TryRead(out var y)&&UnityEngine.JsonUtility.ToJson(x)==UnityEngine.JsonUtility.ToJson(y),"legacy pad parity: "+why);
            Check(current.ActiveProfileKey==baseline.ActiveProfileKey&&current.ActiveName==baseline.ActiveName&&current.SelectedKey==baseline.SelectedKey,"legacy selection parity: "+why);
            Check(current.Controls.Count==baseline.Controls.Count&&current.Controls.Select(c=>c.path+":"+c.value).SequenceEqual(baseline.Controls.Select(c=>c.path+":"+c.value)),"legacy controls parity: "+why);
            Check(current.Choices.Select(c=>c.key+":"+c.connected).SequenceEqual(baseline.Choices.Select(c=>c.key+":"+c.connected)),"legacy choices parity: "+why);
        }
        void Both(bool allow){clock+=.6;current.Tick(allow);baseline.Tick(allow);}
        Compare("initial");
        foreach(float value in new[]{-1f,-.3f,0f,.5f,1f}){Axis(a).value=value;Both(true);Compare("analog "+value);}
        current.BeginSelectionEdit();baseline.BeginSelectionEdit();current.Select("keyboard",false);baseline.Select("keyboard",false);Both(false);Compare("keyboard-only preview");
        current.CancelSelectionEdit();baseline.CancelSelectionEdit();Both(false);Compare("cancel selection");
        nativeResult=0;Both(true);Compare("native authority");
        var late=Device(104,"late wheel",0);InputSystem.Add(late);Both(false);Compare("new HID while native authoritative");
        nativeResult=1167;Both(true);Compare("Unity fallback after native loss");
        InputSystem.Remove(a);Both(false);Compare("remove selected/legacy fallback");InputSystem.Add(a);Both(false);Compare("reconnect under menu lock");
        // Beginning with a suppressed mirror must also preserve the baseline's eventual ordering.
        current.Dispose();baseline.Dispose();nativeResult=0;
        mirror.description=new Description{interfaceName="XInput",product="mirror",serial="duplicate"};
        var twin=new XInputControllerWindows{deviceId=106,path="/twin",description=mirror.description};InputSystem.Add(twin);
        current.Initialize(Path.Combine(root,"current"));baseline.Initialize(Path.Combine(root,"baseline"));Compare("native-first initialization");
        var last=Device(105,"later HID",0);InputSystem.Add(last);Both(false);nativeResult=1167;Both(true);Compare("deferred Unity mirror enumeration");
    }
    static void Performance(string root){
        InputSystem.devices.Clear();missingNative=false;nativeResult=1167;
        for(int d=0;d<8;++d){var device=Device(200+d,"performance controller",0);device.allControls.Clear();for(int c=0;c<32;++c)device.allControls.Add(new AxisControl{path=device.path+"/axis"+c,value=.5f});InputSystem.Add(device);}
        using var current=new Idas3ControllerDevices(()=>clock,Native);using var baseline=new BaselineControllerDevices(()=>clock,Native);
        current.Initialize(Path.Combine(root,"perf"));baseline.Initialize(Path.Combine(root,"perf-baseline"));
        void Measure(string label,Action tick){for(int i=0;i<1000;++i)tick();long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();for(int i=0;i<10000;++i)tick();timer.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;Console.WriteLine("PERF "+label+": "+(bytes/10000.0).ToString("F1")+" bytes/tick, "+(timer.Elapsed.TotalMilliseconds*1000/10000).ToString("F2")+" us/tick");}
        Measure("baseline 8 devices x 32 controls",()=>{clock+=.01;baseline.Tick(false);});
        Measure("snapshot 8 devices x 32 controls",()=>Tick(current));
        int reads=InputSystem.devices.Sum(d=>d.allControls.Cast<AxisControl>().Sum(c=>c.reads));Tick(current);
        Check(InputSystem.devices.Sum(d=>d.allControls.Cast<AxisControl>().Sum(c=>c.reads))-reads==256,"one existing axis read per control; copying adds no reads");
    }
    static void Main(){
        string root=Path.Combine(Path.GetTempPath(),"idas3-snapshots-"+Guid.NewGuid().ToString("N"));
        try{
            var a=Device(10,"same model",.25f);var b=Device(20,"same model",-.75f);InputSystem.Add(a);InputSystem.Add(b);
            using var p=new Idas3ControllerDevices(()=>clock,Native);p.Initialize(root);
            var first=p.Snapshot;var ea=Find(p,10);var eb=Find(p,20);
            Check(!ea.Token.Equals(eb.Token)&&ea.Controls[0].Path==eb.Controls[0].Path,"same paths and duplicate serial/model remain separate endpoints");
            Check(ea.Controls[0].Value==.25f&&eb.Controls[0].Value==-.75f,"separate values");
            Check(ea.ConnectionGeneration==1&&ea.Identity.ResolutionStatus=="Unresolved","initial generation and no physical identity claim");
            Check(ea.Identity.Fields.Any(f=>f.Name=="windowsInterfacePath"&&f.Status==Idas3EvidenceStatus.Unsupported),"Windows association unsupported");
            bool readOnly=false;try{((IList<Idas3ControlSample>)ea.Controls)[0]=default;}catch(NotSupportedException){readOnly=true;}
            Check(readOnly,"control collection cannot mutate published storage");
            bool endpointsReadOnly=false;try{((IList<Idas3EndpointSnapshot>)first.Endpoints)[0]=eb;}catch(NotSupportedException){endpointsReadOnly=true;}
            Check(endpointsReadOnly,"endpoint collection cannot mutate published storage");
            long inventory=first.InventoryGeneration;Axis(a).value=0;Axis(b).value=.5f;Tick(p);
            Check(first.Endpoints.Contains(ea)&&ea.Controls[0].Value==.25f&&Find(p,10).Controls[0].Value==0,"old frame immutable; new zero is valid");
            Check(Find(p,10).Controls[0].Validity==Idas3SampleValidity.Valid&&p.Snapshot.InventoryGeneration==inventory&&p.Snapshot.Sequence>first.Sequence,"numeric zero distinct from inventory change");
            InputSystem.devices.Reverse();InputSystem.Event(a,InputDeviceChange.ConfigurationChanged);Tick(p);
            Check(Find(p,10).Token.Equals(ea.Token)&&Find(p,20).Token.Equals(eb.Token)&&Find(p,20).Controls[0].Value==.5f,"reordered enumeration preserves endpoint ownership");
            int reads=Axis(a).reads;var ignored=p.Snapshot;Check(Axis(a).reads==reads,"snapshot read never polls hardware");
            Check(Find(p,10).SampleSequence==p.Snapshot.Sequence&&Find(p,10).SampledAt==p.Snapshot.StartedAt,"poll publishes explicit sample sequence/time");
            var beforeRemove=p.Snapshot;InputSystem.Remove(a);
            Check(Find(p,20).SampleSequence==beforeRemove.Endpoints.Single(d=>d.Token.Equals(eb.Token)).SampleSequence,"invalidation preserves other endpoint sample age");
            Check(p.Snapshot.Kind==Idas3SnapshotKind.Invalidation&&!p.IsCurrent(beforeRemove),"removal invalidates publication immediately before Tick");
            Check(!p.TryReadCurrent(ea.Token,ea.ConnectionGeneration,"axis",out var absent)&&absent.Value==null,"old generation cannot read a removed endpoint");
            Check(Find(p,10).Controls[0].Value==null&&Find(p,20).Controls[0].Value==.5f,"removal clears only its current sample");
            InputSystem.Add(a);Tick(p);var reconnected=Find(p,10);
            Check(reconnected.Token.Equals(ea.Token)&&reconnected.ConnectionGeneration==2,"same endpoint reconnect increments generation even between ticks");
            Check(!p.TryReadCurrent(ea.Token,1,"axis",out _)&&p.TryReadCurrent(ea.Token,2,"axis",out _),"old connection handle remains invalid after reconnect");
            InputSystem.Remove(a);var replacement=Device(10,"same model",.9f);InputSystem.Add(replacement);Tick(p);
            var replaced=p.Snapshot.Endpoints.Last(d=>d.Identity.Fields.Any(f=>f.Name=="runtimeId"&&f.Value=="10"));
            Check(!replaced.Token.Equals(ea.Token)&&replaced.ConnectionGeneration==1,"runtime ID reused by a new object never inherits token");
            Check(!p.TryReadCurrent(ea.Token,2,"axis",out _),"old endpoint stays invalid after runtime ID reuse");
            Axis(b).value=float.NaN;Tick(p);var invalid=Find(p,20);
            Check(invalid.Status==Idas3EndpointStatus.PartialSample&&invalid.Controls[0].Validity==Idas3SampleValidity.Invalid&&invalid.Controls[0].Value==null,"non-finite sample explicit and not usable zero");
            Axis(b).fail=true;Tick(p);Check(Find(p,20).Controls[0].Validity==Idas3SampleValidity.Unavailable,"read exception is unavailable");Axis(b).fail=false;Axis(b).value=0;
            Axis(b).unsupported=true;Tick(p);Check(Find(p,20).Controls[0].Validity==Idas3SampleValidity.Unsupported&&Find(p,20).Controls[0].Value==null,"unsupported control sample is explicit");Axis(b).unsupported=false;
            var empty=Device(30,"no supported axes",0);empty.allControls.Clear();InputSystem.Add(empty);Tick(p);
            Check(Find(p,30).Status==Idas3EndpointStatus.UnsupportedControls&&!Find(p,30).CanRead,"unsupported layout explicit");
            b.enabled=false;InputSystem.Event(b,InputDeviceChange.Disabled);Check(Find(p,20).Status==Idas3EndpointStatus.Disabled,"disabled invalidates immediately");Tick(p);
            b.enabled=true;InputSystem.Event(b,InputDeviceChange.Enabled);Tick(p);Check(Find(p,20).ConnectionGeneration==2,"enable begins new sample connection generation");
            var mirror=new XInputControllerWindows{deviceId=40,path="/mirror",description=new Description{product="arbitrary name",interfaceName="XInput"}};InputSystem.Add(mirror);Tick(p);
            Check(Find(p,40).Status==Idas3EndpointStatus.UnsupportedControls,"Unity XInput family eligible when native absent");
            nativeResult=0;Tick(p,.6);
            Check(Find(p,40).Status==Idas3EndpointStatus.SuppressedMirror&&!Find(p,40).CanRead,"native authority suppresses Unity XInput family without name guessing");
            Check(Find(p,20).CanRead&&replaced.Token.Equals(p.Snapshot.Endpoints.Single(d=>d.Token.Equals(replaced.Token)).Token),"same VID/PID HID not suppressed");
            p.Select("xinput:0",false);Tick(p);Check(p.TryRead(out var pad)&&pad.thumbLX==-16384&&pad.rightTrigger==128&&pad.buttons==0x2000,"legacy native accessor preserves exact bits");
            var native=p.Snapshot.Endpoints.Single(d=>d.Backend=="NativeXInput"&&d.CanRead);
            Check(native.Controls[16].Value==-.5f&&native.Controls[15].Value==128/255f&&p.Controls[16].value==native.Controls[16].Value,"active native control normalization unchanged");
            int nativeBefore=polls;Tick(p);Check(polls-nativeBefore==1,"one native read per connected slot on steady tick; no extra polling loop");
            nativeResult=5;Tick(p);Check(p.Snapshot.Endpoints.Single(d=>d.Token.Equals(native.Token)).Status==Idas3EndpointStatus.ReadError,"native error code not treated as a valid zero");
            Check(Find(p,40).Status==Idas3EndpointStatus.UnsupportedControls&&Find(p,40).ConnectionGeneration==2,"native loss exposes Unity fallback with fresh generation");
            nativeResult=0;Tick(p,.6);Check(p.Snapshot.Endpoints.Single(d=>d.Token.Equals(native.Token)).ConnectionGeneration==2&&Find(p,40).Status==Idas3EndpointStatus.SuppressedMirror,"native return invalidates Unity fallback stream");
            missingNative=true;Tick(p);Check(p.Snapshot.Endpoints.Single(d=>d.Token.Equals(native.Token)).Status==Idas3EndpointStatus.BackendUnavailable,"backend missing explicit");
            Check(p.Snapshot.Endpoints.Where(d=>d.Backend=="NativeXInput").All(d=>d.Status==Idas3EndpointStatus.BackendUnavailable),"backend failure invalidates all native slots in the same publication");
            var source=new[]{new Idas3IdentityField("serial","untrusted","test",Idas3EvidenceStatus.Reported)};var evidence=new Idas3EndpointIdentity(source);source[0]=default;
            Check(evidence.Fields[0].Value=="untrusted"&&evidence.ResolutionStatus=="Unresolved","identity evidence copied and never promoted to resolution");
            // Atomic publication: writer updates both axes; background reader only reads immutable frames.
            Axis(replacement).value=Axis(b).value=0;Tick(p);
            bool done=false;Exception readerError=null;int observed=0;using var readerReady=new ManualResetEventSlim();
            var task=Task.Run(()=>{try{while(!Volatile.Read(ref done)){Interlocked.Increment(ref observed);readerReady.Set();var frame=p.Snapshot;var x=frame.Endpoints.Single(d=>d.Token.Equals(replaced.Token));var y=frame.Endpoints.Single(d=>d.Token.Equals(eb.Token));if(frame.Sequence>0&&x.Controls[0].Value!=y.Controls[0].Value)throw new Exception("mixed frame");}}catch(Exception e){readerError=e;}});
            readerReady.Wait();
            for(int i=0;i<1000;++i){Axis(replacement).value=Axis(b).value=(i%100)/100f;Tick(p);}
            Volatile.Write(ref done,true);task.Wait();Check(readerError==null&&observed>0,"concurrent reader cannot observe mixed or mutable frame");
            for(int i=0;i<100;++i)Tick(p);long bytes=GC.GetAllocatedBytesForCurrentThread();var timer=Stopwatch.StartNew();
            for(int i=0;i<10000;++i)Tick(p);timer.Stop();bytes=GC.GetAllocatedBytesForCurrentThread()-bytes;
            Console.WriteLine("PERF small inventory: "+(bytes/10000.0).ToString("F1")+" bytes/tick, "+(timer.Elapsed.TotalMilliseconds*1000/10000).ToString("F2")+" us/tick; portable adapters, not Unity/Windows timing");
            var oldSession=p.Snapshot.Session;p.Dispose();Check(p.Snapshot.Kind==Idas3SnapshotKind.Stopped&&!p.TryReadCurrent(replaced.Token,1,"axis",out _),"dispose invalidates all current lookup");
            p.Initialize(root);Check(p.Snapshot.Session!=oldSession&&!p.TryReadCurrent(replaced.Token,1,"axis",out _),"new provider session invalidates prior tokens");
            p.Dispose();LegacyCompatibility(root);Performance(root);
            Console.WriteLine("PASS "+checks+" production provider/snapshot checks with portable Unity adapters; no Unity or hardware execution.");
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
