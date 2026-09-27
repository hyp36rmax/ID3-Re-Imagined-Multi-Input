using System;
using System.IO;
using UnityEngine;
using B=Idas3ControlBindings;
static class Checks {
    static int checks;
    static void Check(bool condition,string message){++checks;if(!condition)throw new Exception(message);}
    static bool Key(Idas3Native.FrameInput f,int key)=>(((key<32?f.key0:key<64?f.key1:key<96?f.key2:f.key3)>>(key&31))&1)!=0;
    sealed class Platform:Idas3GameOptions.IPlatform {
        public int Width=>1280;public int Height=>720;public int DisplayMode=>0;public double Now=>0;
        public bool fail;public int displayChanges;
        public Idas3GameOptions.ResolutionChoice[] Resolutions=>new[]{new Idas3GameOptions.ResolutionChoice(1280,720)};
        public void Apply(Idas3GameOptions.Values a,Idas3GameOptions.Values b,bool display){if(display)++displayChanges;if(fail)throw new Exception("synthetic failure");}
    }
    static void Main(){
        string root=Path.Combine(Path.GetTempPath(),"id3-controller-foundation-"+Guid.NewGuid().ToString("N"));
        try{
            var b=new B();b.Initialize(root);b.BeginEdit();Check(b.ApplyDraft(),"fixture save");string original=File.ReadAllText(b.FilePath);
            var before=b.Draft.Clone();Check(!b.TrySetDraftPad(B.ActionId.Accelerate,B.PadInput.B),"reject occupied pad");
            Check(B.Equivalent(before,b.Draft)&&b.LastError.Contains("Shift up"),"conflict explicit and no other action changes");
            Check(!b.TrySetDraftKey(B.ActionId.Brake,B.Slot.Primary,KeyCode.W),"reject occupied keyboard");
            Check(B.Equivalent(before,b.Draft),"keyboard conflict preserves draft");
            Check(b.ClearDraft(B.ActionId.ShiftUp,B.Slot.Controller),"explicit clear");
            Check(b.TrySetDraftPad(B.ActionId.Accelerate,B.PadInput.B),"rebind after explicit clear");
            Check(b.Draft.actions[1].pad==before.actions[1].pad&&b.Draft.actions[0].key1==before.actions[0].key1,"unrelated actions and keys preserved");
            Check(File.ReadAllText(b.FilePath)==original,"editing never writes saved config");
            var checkpoint=b.SaveDraftCheckpoint();Check(b.ClearDraft(B.ActionId.Brake,B.Slot.Controller),"setup mutation");
            b.RestoreDraftCheckpoint(checkpoint);Check(B.Equivalent(checkpoint.values,b.Draft),"cancel setup restores prior draft, not just defaults");
            b.CancelEdit(false);Check(B.Equivalent(b.Current,b.Draft)&&File.ReadAllText(b.FilePath)==original,"cancel keeps saved settings");
            b.Poll(k=>false,new B.PadState{connected=true},0);
            var pad=new B.PadState{connected=true,thumbLX=-17001,thumbLY=2222,rightTrigger=187,leftTrigger=81,buttons=0x2000};
            b.Poll(k=>k==KeyCode.W||k==KeyCode.H,pad,1);
            var game=new Idas3Native.FrameInput();b.ApplyDriving(ref game);var preview=b.EvaluateDraftDriving();
            Check(game.thumbLX==preview.thumbLX&&game.thumbLY==preview.thumbLY&&game.rightTrigger==preview.rightTrigger&&game.leftTrigger==preview.leftTrigger&&game.padButtons==preview.padButtons&&game.key2==preview.key2,"draft equals gameplay evaluator for unchanged bindings");
            Check(preview.thumbLX==-17001&&preview.thumbLY==2222&&preview.rightTrigger==187&&preview.leftTrigger==81,"analog normalization retained");
            Check(Key(preview,87)&&Key(preview,72)&&b.DraftActionHeld(B.ActionId.ShiftUp),"evaluated keys and buttons");
            b.BeginEdit();b.ClearDraft(B.ActionId.ShiftUp,B.Slot.Controller);b.TrySetDraftPad(B.ActionId.Accelerate,B.PadInput.B);
            b.Poll(k=>false,pad,2);preview=b.EvaluateDraftDriving();game=default;b.ApplyDriving(ref game);
            Check(preview.rightTrigger==255&&game.rightTrigger==187,"draft test uses evaluated rebind, gameplay uses saved mapping");
            Check(File.ReadAllText(b.FilePath)==original,"testing does not save");
            b.BeginCapture(B.ActionId.Camera,B.Slot.Controller,3);preview=b.EvaluateDraftDriving();Check(preview.rightTrigger==0&&b.SuppressInput,"capture guard retained");
            b.CancelCapture();b.Poll(k=>false,new B.PadState{connected=true},4);b.CancelEdit(false);
            b.SelectControllerProfile("wheel","Wheel",true);b.BeginEdit();
            var axis=new Idas3ControllerControl{path="wheel",label="wheel",minimum=-1,maximum=1,value=0};
            var pedal=new Idas3ControllerControl{path="pedal",label="pedal",minimum=-1,maximum=1,value=1};
            Check(b.TrySetDraftControl(B.ActionId.SteerLeft,axis,-1,0)&&b.TrySetDraftControl(B.ActionId.SteerRight,axis,1,0),"two steering directions");
            Check(b.TrySetDraftControl(B.ActionId.Accelerate,pedal,-1,1),"reversed-rest pedal binding");
            Check(b.ApplyDraft(),"apply custom profile");b.Poll(k=>false,default,5,new[]{axis,pedal});axis.value=.5f;pedal.value=-1;b.Poll(k=>false,default,6,new[]{axis,pedal});preview=b.EvaluateDraftDriving();
            Check(preview.thumbLX>=16383&&preview.thumbLX<=16384&&preview.rightTrigger==255,"custom evaluated wheel and reversed pedal");
            b.ControllerDeviceChanged();b.Poll(k=>false,default,7,new[]{axis,pedal});preview=b.EvaluateDraftDriving();Check(preview.rightTrigger==0,"reconnect-held guard retained");
            b.Poll(k=>k==KeyCode.W,default,8,Array.Empty<Idas3ControllerControl>());preview=b.EvaluateDraftDriving();Check(Key(preview,87),"keyboard recovery after disconnect");
            var reload=new B();reload.Initialize(root);Check(reload.LastError==null,"saved compatible format reloads");
            foreach(int version in new[]{1,2,3}){
                string fixture=Path.Combine(root,"legacy-"+version);Directory.CreateDirectory(fixture);
                var saved=B.Defaults();saved.version=version;string json=JsonUtility.ToJson(saved);
                File.WriteAllText(Path.Combine(fixture,Path.GetFileName(b.FilePath)),json);
                var compat=new B();compat.Initialize(fixture);
                Check(compat.LastError==null&&compat.Current.actions[0].key1==KeyCode.W,"load saved version "+version);
                Check(File.ReadAllText(compat.FilePath)==json,"loading does not rewrite version "+version);
            }
            var platform=new Platform();var options=new Idas3GameOptions(platform);options.Initialize(Path.Combine(root,"options"));
            options.BeginEdit();Check(options.ApplyDraft(),"options fixture");string oldOptions=File.ReadAllText(options.FilePath);
            options.Draft.musicVolume=.23f;options.Draft.width=1920;options.Draft.wheelForceFeedback=true;options.Draft.wheelFeedbackStrength=.61f;
            options.Draft.wheelFeedbackInvert=true;options.Draft.wheelFeedbackDevice="synthetic-output";
            Check(File.ReadAllText(options.FilePath)==oldOptions,"FFB edits not persisted before apply");
            Check(options.ApplyWheelSettings(),"scoped FFB apply");
            Check(options.Current.wheelForceFeedback&&options.Current.wheelFeedbackInvert&&options.Current.wheelFeedbackStrength==.61f&&options.Current.wheelFeedbackDevice=="synthetic-output","all working FFB settings persist");
            Check(options.Current.width==1280&&options.Current.musicVolume!=.23f&&options.Draft.width==1920&&options.Draft.musicVolume==.23f&&platform.displayChanges==0,"FFB Apply preserves unrelated unsaved settings and does not change display");
            var optionsReload=new Idas3GameOptions(new Platform());optionsReload.Initialize(Path.Combine(root,"options"));Check(optionsReload.Current.wheelFeedbackStrength==.61f,"FFB compatible reload");
            platform.fail=true;options.Draft.wheelFeedbackStrength=.19f;Check(!options.ApplyWheelSettings(),"FFB failure reported");
            Check(options.Current.wheelFeedbackStrength==.61f&&options.Draft.wheelFeedbackStrength==.19f&&options.Draft.musicVolume==.23f,"FFB failure restores current and retains draft");
            // Exercise the shared save coordinator with real binding/options persistence.
            var combined=new B();combined.Initialize(Path.Combine(root,"combined"));combined.BeginEdit();Check(combined.ApplyDraft(),"combined fixture");
            platform.fail=false;options.Draft.wheelFeedbackStrength=.37f;
            combined.ClearDraft(B.ActionId.Camera,B.Slot.Primary);
            var setupCheckpoint=combined.SaveDraftCheckpoint();combined.ClearDraft(B.ActionId.Brake,B.Slot.Controller);
            int deviceWrites=0,feedbackWrites=0;bool deviceFailure=false;string selected="preview",savedSelected="original";
            Func<Idas3ControllerSave.Result> save=()=>Idas3ControllerSave.Save(combined.ApplyDraft,()=>combined.LastError,
                ()=>{++deviceWrites;if(deviceFailure)return false;savedSelected=selected;return true;},()=>"synthetic device write failure",
                ()=>{++feedbackWrites;return options.ApplyWheelSettings();},()=>options.LastError);
            File.Delete(combined.FilePath);Directory.CreateDirectory(combined.FilePath);
            var result=save();
            Check(!result.Complete&&!result.BindingsSaved&&deviceWrites==0&&feedbackWrites==0,"binding IO failure stops remaining writes");
            Check(combined.HasUnsavedChanges&&options.Draft.wheelFeedbackStrength==.37f&&selected=="preview","binding failure keeps all pending drafts");
            combined.RestoreDraftCheckpoint(setupCheckpoint);
            Check(B.Equivalent(combined.Draft,setupCheckpoint.values),"failed binding save allows Setup checkpoint cancellation");
            Directory.Delete(combined.FilePath);combined.ClearDraft(B.ActionId.Brake,B.Slot.Controller);deviceFailure=true;
            result=save();Check(!result.Complete&&result.BindingsSaved&&feedbackWrites==0&&savedSelected=="original","device failure reports partial binding persistence and skips FFB");
            Check(!combined.HasUnsavedChanges&&selected=="preview"&&options.Draft.wheelFeedbackStrength==.37f,"device failure retains selection and FFB draft");
            deviceFailure=false;platform.fail=true;result=save();
            Check(!result.Complete&&result.BindingsSaved&&savedSelected=="preview"&&result.Message.Contains("Bindings and device saved"),"FFB failure accurately reports earlier saves");
            Check(options.Draft.wheelFeedbackStrength==.37f&&options.Current.wheelFeedbackStrength==.61f&&options.Draft.musicVolume==.23f&&options.Draft.width==1920,"partial FFB failure retains unrelated and failed drafts");
            platform.fail=false;result=save();Check(result.Complete&&options.Current.wheelFeedbackStrength==.37f,"retry completes combined save");
            Check(options.Draft.musicVolume==.23f&&options.Draft.width==1920&&options.Current.musicVolume!=.23f&&options.Current.width==1280,"combined save preserves unrelated option drafts");
            combined.ClearDraft(B.ActionId.Brake,B.Slot.Primary);options.Draft.wheelFeedbackStrength=.99f;
            combined.CancelEdit(false);Idas3GameOptions.CopyWheelSettings(options.Current,options.Draft);
            Check(!combined.HasUnsavedChanges&&options.Draft.wheelFeedbackStrength==.37f&&options.Draft.musicVolume==.23f,"discard restores last saved Controller state and preserves unrelated options");
            Console.WriteLine("PASS "+checks+" portable controller binding checks; Unity UI/native response not executed.");
        }finally{if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
