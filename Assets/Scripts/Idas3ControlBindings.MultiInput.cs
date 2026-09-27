using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

// Experimental source references extend the existing capture/evaluator, not a second mapper.
public sealed partial class Idas3ControlBindings
{
    [Serializable] internal sealed class ExperimentalAssignment {
        public string deviceName;
        public Binding binding=new Binding();
        internal Idas3EndpointToken endpoint;
        internal long generation;
        internal string runtimePath;
        internal bool assigned, waitingRelease=true;
        internal ExperimentalAssignment Clone(){var a=(ExperimentalAssignment)MemberwiseClone();a.binding=binding.Clone();return a;}
    }
    [Serializable] internal sealed class ExperimentalValues {
        public int version=1;public bool enabled;
        public ExperimentalAssignment[] actions=new ExperimentalAssignment[10];
        public ExperimentalValues(){for(int i=0;i<10;++i)actions[i]=new ExperimentalAssignment();}
        internal ExperimentalValues Clone(){var v=new ExperimentalValues{enabled=enabled};for(int i=0;i<10;++i)v.actions[i]=actions[i].Clone();return v;}
    }
    private sealed class ExperimentalSource {
        internal Idas3EndpointToken endpoint;internal long generation;internal string localPath,name;
        internal Idas3ControllerControl control;
    }
    private ExperimentalValues experimentalCurrent=new ExperimentalValues(),experimentalDraft=new ExperimentalValues();
    private readonly Values experimentalCurrentEvaluation=Defaults(),experimentalDraftEvaluation=Defaults();
    private readonly Dictionary<string,ExperimentalSource> experimentalSources=new Dictionary<string,ExperimentalSource>(StringComparer.Ordinal);
    private readonly Dictionary<string,Idas3ControllerControl> experimentalControls=new Dictionary<string,Idas3ControllerControl>(StringComparer.Ordinal);
    private Idas3DeviceFrame experimentalFrame;
    private Guid experimentalSession;
    private long experimentalInventory=-1,experimentalSequence=-1;
    internal const double ExperimentalMaxSampleAge=.25;
    private bool experimentalDirty;
    private string experimentalFile;
    public bool ExperimentalEnabled=>experimentalCurrent.enabled;
    public bool ExperimentalDraftEnabled=>experimentalDraft.enabled;
    public bool ExperimentalInUse=>ExperimentalEnabled||ExperimentalDraftEnabled;
    public bool ExperimentalBlocksFeedback=>ExperimentalInUse;
    public string ExperimentalFilePath=>experimentalFile;
    public string ExperimentalError { get; private set; }
    public bool HasExperimentalChanges=>experimentalDirty;
    public bool CanCaptureExperimental=>experimentalControls.Count>0;
    private void InitializeExperimental(string saveRoot){
        experimentalFile=Path.Combine(saveRoot,"experimental-input.json");experimentalCurrent=new ExperimentalValues();ExperimentalError=null;
        try{
            if(File.Exists(experimentalFile)){
                if(new FileInfo(experimentalFile).Length>65536)throw new InvalidDataException("Experimental input file is too large.");
                var v=UnityEngine.JsonUtility.FromJson<ExperimentalValues>(File.ReadAllText(experimentalFile));
                if(v==null||v.version!=1||v.actions==null||v.actions.Length!=10)throw new InvalidDataException("Unsupported experimental input settings.");
                foreach(var a in v.actions){if(a==null||a.binding==null||!ValidExperimentalBinding(a.binding))throw new InvalidDataException("Invalid experimental assignment.");a.assigned=false;a.runtimePath=null;a.waitingRelease=true;}
                experimentalCurrent=v;
            }
        }catch(Exception e){ExperimentalError="Experimental settings not loaded: "+e.Message;}
        CancelExperimentalEdit();
    }
    private static bool ValidExperimentalBinding(Binding b)=>string.IsNullOrEmpty(b.controlPath)||
        (b.controlPath.Length<=1024&&Finite(b.controlMin)&&Finite(b.controlMax)&&Finite(b.controlRest)&&b.controlMax>b.controlMin&&b.controlRest>=b.controlMin&&b.controlRest<=b.controlMax&&(b.controlDirection==1||b.controlDirection==-1));
    private void CancelExperimentalEdit(){experimentalDraft=experimentalCurrent.Clone();experimentalDirty=false;}
    public void SetExperimentalDraftEnabled(bool enabled){CancelCapture();experimentalDraft.enabled=enabled;experimentalDirty=true;foreach(var a in experimentalDraft.actions)a.waitingRelease=true;}
    public bool ApplyExperimentalDraft(){
        if(!experimentalDirty)return true;
        string temporary=experimentalFile+"."+Guid.NewGuid().ToString("N")+".tmp";
        try{
            Directory.CreateDirectory(Path.GetDirectoryName(experimentalFile));
            foreach(var a in experimentalDraft.actions)if(!ValidExperimentalBinding(a.binding))throw new InvalidDataException("Invalid experimental calibration.");
            byte[] data=new UTF8Encoding(false).GetBytes(UnityEngine.JsonUtility.ToJson(experimentalDraft,true));
            using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(data,0,data.Length);stream.Flush(true);}
            if(File.Exists(experimentalFile))File.Replace(temporary,experimentalFile,experimentalFile+".previous");else File.Move(temporary,experimentalFile);
            bool returning=experimentalCurrent.enabled&&!experimentalDraft.enabled;
            foreach(var a in experimentalDraft.actions)a.waitingRelease=true;experimentalCurrent=experimentalDraft.Clone();
            experimentalDirty=false;ExperimentalError=null;if(returning)ControllerDeviceChanged();return true;
        }catch(Exception e){try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}ExperimentalError="Could not save multi-input assignments: "+e.Message;return false;}
    }
    private static string ExperimentalPath(Idas3EndpointSnapshot endpoint,string path)=>endpoint.Token+"/"+endpoint.ConnectionGeneration+"/"+path;
    private void UpdateExperimentalFrame(Idas3DeviceFrame frame,double now){
        // The provider and host share one monotonic clock. Historical or aged frames
        // are evidence only, never permission to keep driving with an old value.
        if(frame!=null){
            if(frame.Session!=experimentalSession)experimentalSequence=-1;
            if(double.IsNaN(now)||double.IsInfinity(now)||frame.Sequence<experimentalSequence||now<frame.CompletedAt||now-frame.CompletedAt>ExperimentalMaxSampleAge)frame=null;
            else experimentalSequence=frame.Sequence;
        }
        experimentalFrame=frame;experimentalControls.Clear();
        if(frame!=null){
            if(frame.Session!=experimentalSession||frame.InventoryGeneration!=experimentalInventory){
                experimentalSources.Clear();experimentalSession=frame.Session;experimentalInventory=frame.InventoryGeneration;
                foreach(var endpoint in frame.Endpoints)if(endpoint.Status==Idas3EndpointStatus.Ready||endpoint.Status==Idas3EndpointStatus.PartialSample||endpoint.Status==Idas3EndpointStatus.ReadError)foreach(var sample in endpoint.Controls){
                    string path=ExperimentalPath(endpoint,sample.Path);
                    experimentalSources[path]=new ExperimentalSource{endpoint=endpoint.Token,generation=endpoint.ConnectionGeneration,localPath=sample.Path,name=endpoint.Name,
                        control=new Idas3ControllerControl{path=path,label=sample.Label,minimum=sample.Minimum,maximum=sample.Maximum,button=sample.Button}};
                }
            }
            foreach(var pair in experimentalSources){var source=pair.Value;
                if(frame.TryGetEndpoint(source.endpoint,out var endpoint)&&endpoint.ConnectionGeneration==source.generation&&endpoint.SampledAt.HasValue&&now>=endpoint.SampledAt.Value&&now-endpoint.SampledAt.Value<=ExperimentalMaxSampleAge&&endpoint.TryGetControl(source.localPath,out var sample)){
                    source.control.value=sample.Value.Value;experimentalControls.Add(pair.Key,source.control);
                }
            }
        }
        PrepareExperimentalEvaluation(experimentalCurrent,current,experimentalCurrentEvaluation);
        PrepareExperimentalEvaluation(experimentalDraft,draft,experimentalDraftEvaluation);
    }
    private void PrepareExperimentalEvaluation(ExperimentalValues assignments,Values keyboard,Values target){
        using(var scope=new ExperimentalScope(this))for(int i=0;i<10;++i){
            var a=assignments.actions[i];var b=target.actions[i];CopyController(a.binding,b);b.pad=PadInput.None;
            b.key1=keyboard.actions[i].key1;b.key2=keyboard.actions[i].key2;b.key3=keyboard.actions[i].key3;
            bool live=a.assigned&&a.runtimePath!=null&&experimentalControls.ContainsKey(a.runtimePath);
            b.controlPath=live?a.runtimePath:null;
            if(!live){a.waitingRelease=true;continue;}
            if(a.waitingRelease){if(CustomAmount(b)<=.15f)a.waitingRelease=false;else b.controlPath=null;}
        }
    }
    private readonly struct ExperimentalScope : IDisposable {
        private readonly Idas3ControlBindings owner;private readonly Dictionary<string,Idas3ControllerControl> previous;private readonly PadState previousPad;private readonly bool previousGeneric,previousBlocked;
        internal ExperimentalScope(Idas3ControlBindings owner){this.owner=owner;previous=owner.controls;previousPad=owner.pad;previousGeneric=owner.genericProfile;previousBlocked=owner.controllerReleaseBlocked;
            owner.controls=owner.experimentalControls;owner.pad=default;owner.genericProfile=true;owner.controllerReleaseBlocked=false;}
        public void Dispose(){owner.controls=previous;owner.pad=previousPad;owner.genericProfile=previousGeneric;owner.controllerReleaseBlocked=previousBlocked;}
    }
    private bool SetExperimentalControl(ActionId action,Binding binding){
        if(!experimentalSources.TryGetValue(binding.controlPath,out var source)||!experimentalControls.ContainsKey(binding.controlPath)){LastError="Source unavailable; capture the action again.";return false;}
        if(!ValidExperimentalBinding(binding)){LastError="Invalid control calibration.";return false;}
        for(int i=0;i<10;++i){var other=experimentalDraft.actions[i];
            if(i!=(int)action&&other.assigned&&other.runtimePath==binding.controlPath&&other.binding.controlDirection==binding.controlDirection){LastError="Already assigned to "+ActionName((ActionId)i)+". Clear that assignment first; no other action changed.";return false;}}
        var saved=binding.Clone();saved.controlPath=source.localPath;
        experimentalDraft.actions[(int)action]=new ExperimentalAssignment{deviceName=source.name,binding=saved,endpoint=source.endpoint,generation=source.generation,runtimePath=binding.controlPath,assigned=true};
        experimentalDirty=true;LastError=null;return true;
    }
    internal bool TrySetExperimentalControl(ActionId action,Idas3EndpointToken token,long generation,string localPath,int direction,float rest){
        CheckAction(action);if(!experimentalDraft.enabled||experimentalFrame==null||!experimentalFrame.TryGetEndpoint(token,out var endpoint)||endpoint.ConnectionGeneration!=generation){LastError="Reassign a connected source.";return false;}
        string path=ExperimentalPath(endpoint,localPath);
        return experimentalControls.TryGetValue(path,out var control)&&TrySetDraftControl(action,control,direction,rest);
    }
    public bool HasControllerAssignment(ActionId action){
        if(!ExperimentalDraftEnabled){var b=draft.actions[(int)action];return b.pad!=PadInput.None||!string.IsNullOrEmpty(b.controlPath);}
        var a=experimentalDraft.actions[(int)action];return a.assigned&&a.runtimePath!=null&&experimentalControls.ContainsKey(a.runtimePath);
    }
    private string ExperimentalBindingName(ActionId action,bool useDraft){
        var a=(useDraft?experimentalDraft:experimentalCurrent).actions[(int)action];
        if(string.IsNullOrEmpty(a.binding.controlPath))return "Unassigned";
        bool sameConnection=a.assigned&&experimentalFrame!=null&&experimentalFrame.TryGetEndpoint(a.endpoint,out var endpoint)&&endpoint.ConnectionGeneration==a.generation&&
            (endpoint.Status==Idas3EndpointStatus.Ready||endpoint.Status==Idas3EndpointStatus.PartialSample||endpoint.Status==Idas3EndpointStatus.ReadError);
        string status=!a.assigned?" — reassign":experimentalFrame==null?" — unavailable":!sameConnection?" — reassign":a.runtimePath==null||!experimentalControls.ContainsKey(a.runtimePath)?" — unavailable":a.waitingRelease?" — release control":"";
        return a.deviceName+" / "+(a.binding.controlLabel??a.binding.controlPath)+status;
    }
    private bool ExperimentalAnyAssignedHeld(){
        if(KeysHeld())return true;
        using(var scope=new ExperimentalScope(this))foreach(var binding in experimentalCurrentEvaluation.actions)if(CustomAmount(binding)>.15f)return true;
        return false;
    }
    private bool ExperimentalActionHeld(ActionId action,bool useDraft){
        using(var scope=new ExperimentalScope(this))return KeyboardHeld((useDraft?draft:current).actions[(int)action])||Digital((useDraft?experimentalDraftEvaluation:experimentalCurrentEvaluation).actions[(int)action]);
    }
    private int ExperimentalSteeringOrthogonal(Values values){
        var a=(ReferenceEquals(values,experimentalDraftEvaluation)?experimentalDraft:experimentalCurrent).actions;
        var left=a[2];var right=a[3];
        if(!left.assigned||!right.assigned||left.waitingRelease||right.waitingRelease||!left.endpoint.Equals(right.endpoint)||left.generation!=right.generation||left.binding.controlPath!=right.binding.controlPath||left.binding.controlDirection==right.binding.controlDirection)return 0;
        string paired=PairedStickPath(left.binding.controlPath);
        if(paired==null||experimentalFrame==null||!experimentalFrame.TryGetEndpoint(left.endpoint,out var e)||e.ConnectionGeneration!=left.generation||!e.TryGetControl(paired,out var sample)||sample.Button||sample.Minimum>=0||sample.Maximum<=0)return 0;
        float value=sample.Value.Value;float amount=Math.Max(-1,Math.Min(1,value/(value<0?-sample.Minimum:sample.Maximum)));return(int)Math.Round(amount*(amount<0?32768:32767));
    }
}
