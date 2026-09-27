using System;
using UnityEngine;

public sealed partial class Idas3PauseMenu
{
    private static readonly string[] ControllerPages={"QUICK SETUP","OVERVIEW","BIND CONTROLS","TEST CONTROLS","FORCE FEEDBACK"};
    private static readonly Idas3ControlBindings.ActionId[] SetupActions={Idas3ControlBindings.ActionId.SteerLeft,Idas3ControlBindings.ActionId.SteerRight,
        Idas3ControlBindings.ActionId.Accelerate,Idas3ControlBindings.ActionId.Brake,Idas3ControlBindings.ActionId.ShiftUp,Idas3ControlBindings.ActionId.ShiftDown};
    private int controllerPage=1,quickStep;
    private bool controllerTesting,overviewSaved,overviewDevices,testFocused,testSuppressed;
    private Vector2 controllerDeviceScroll;
    private Idas3Native.FrameInput controllerTestFrame;
    private readonly bool[] controllerTestButtons=new bool[10];
    private Idas3ControlBindings.DraftCheckpoint quickCheckpoint;
    internal bool TestingControls=>IsOpen&&showOptions&&tab==3&&controllerPage==3&&controllerTesting;
    internal int ControllerPage=>controllerPage;
    private int ControllerRows=>controllerPage==2?12:controllerPage==0||controllerPage==4?6:controllerPage==1?4:3;
    internal void SetControllerTestSample(Idas3Native.FrameInput frame,bool[] buttons,bool focused,bool suppressed){
        controllerTestFrame=frame;testFocused=focused;testSuppressed=suppressed;
        Array.Copy(buttons,controllerTestButtons,10);
    }
    internal void SelectControllerPage(int page){
        if(bindings!=null&&bindings.IsCapturing)return;
        CancelQuickSetup();controllerTesting=false;controllerPage=Wrap(page,5);wheelEditing=false;selection=1;notice="";
        if(controllerPage==4)wheelFeedback?.RefreshDevices();
    }
    private void CancelQuickSetup(){
        if(quickCheckpoint==null)return;
        bindings.RestoreDraftCheckpoint(quickCheckpoint);quickCheckpoint=null;quickStep=0;
    }
    private void ControllerHorizontal(int delta){
        if(selection==1){SelectControllerPage(controllerPage+Math.Sign(delta));return;}
        if(selection==2){ChangeControllerDevice(Math.Sign(delta));return;}
        if(controllerPage==2&&selection<=Rows){bindingColumn=Wrap(bindingColumn+Math.Sign(delta),4);return;}
        if(controllerPage==4&&selection<=Rows)AdjustWheel(selection-3,Math.Sign(delta));
    }
    private void ControllerActivate(bool pointer=false){
        if(!pointer&&wheelNavigation&&(selection<=2||controllerPage==4&&selection<=Rows)){wheelEditing=!wheelEditing;return;}
        if(selection==1){SelectControllerPage(controllerPage+1);return;}
        if(selection==2){ChangeControllerDevice(1);return;}
        if(selection==Rows+1){ControllerDiscard();return;}
        if(selection==Rows+2){ControllerApply();return;}
        if(selection==Rows+3){Back();return;}
        if(controllerPage==2){OpenBindingChoice((Idas3ControlBindings.ActionId)(selection-3),(Idas3ControlBindings.Slot)bindingColumn);return;}
        if(controllerPage==1){if(selection==3)overviewSaved=!overviewSaved;else overviewDevices=!overviewDevices;return;}
        if(controllerPage==3){controllerTesting=true;return;}
        if(controllerPage==4){AdjustWheel(selection-3,1);return;}
        if(selection==3){
            if(controllerDevices==null||controllerDevices.Controls.Count==0){notice="Connect and select the controller to set up. Keyboard bindings remain in Bind Controls.";return;}
            if(quickCheckpoint==null){quickCheckpoint=bindings.SaveDraftCheckpoint();quickStep=0;}
            if(quickStep<SetupActions.Length){captureAction=SetupActions[quickStep];captureSlot=Idas3ControlBindings.Slot.Controller;
                bindings.BeginCapture(captureAction,captureSlot,Time.realtimeSinceStartupAsDouble);}
        }else if(selection==4&&quickCheckpoint!=null&&quickStep<SetupActions.Length){
            var b=bindings.Draft.actions[(int)SetupActions[quickStep]];
            if(quickStep<4&&b.pad==Idas3ControlBindings.PadInput.None&&string.IsNullOrEmpty(b.controlPath)){notice="Bind this required action before continuing.";return;}
            ++quickStep;notice=quickStep==6?"Review the six assignments below, then APPLY. Cancel restores your prior draft.":"Accepted in draft. Release controls before capturing the next action.";
        }else if(selection==5&&quickCheckpoint!=null&&quickStep>=4&&quickStep<6){++quickStep;notice="Existing shift assignment kept.";}
        else if(selection==6){CancelQuickSetup();notice="Quick Setup cancelled; previous draft restored.";}
    }
    private void ControllerApply(){
        if(bindings==null)return;
        if(quickCheckpoint!=null&&quickStep<6){notice="Finish and review Quick Setup before saving, or cancel it.";return;}
        if(controllerPage==4){notice=options.ApplyWheelSettings()?"FORCE FEEDBACK SETTINGS SAVED":options.LastError;return;}
        if(!bindings.ApplyDraft()){notice=bindings.LastError;return;}
        quickCheckpoint=null;
        if(controllerDevices!=null&&!controllerDevices.ApplySelectionEdit()){notice="Bindings saved; device selection could not be saved: "+controllerDevices.LastError;return;}
        notice="CONTROLLER AND KEYBOARD BINDINGS SAVED";
    }
    private void ControllerDiscard(){
        CancelQuickSetup();controllerTesting=false;controllerDevices?.CancelSelectionEdit();controllerDevices?.BeginSelectionEdit();
        bindings?.CancelEdit(false);Idas3GameOptions.CopyWheelSettings(options.Current,options.Draft);notice="Controller draft discarded. Saved configuration restored.";
    }
    private void AdjustWheel(int row,int direction){
        // Reuse the existing option adjustment path; no hardware output here.
        int previousTab=tab;try{tab=4;Adjust(row,direction);}finally{tab=previousTab;}
    }
    private bool ControllerButton(Rect rect,string text,int row,bool enabled=true){
        if(!Button(rect,text,selection==row,enabled))return false;
        selection=row;ControllerActivate(true);return true;
    }
    private void ControllerView(){
        Fill(new Rect(262,130,748,413),Panel);Text(new Rect(282,142,680,34),"CONTROLLER — SINGLE ACTIVE DEVICE",heading);
        for(int i=0;i<5;++i)if(Button(new Rect(276+i*144,181,140,32),ControllerPages[i],controllerPage==i,true,false,bindingButton))SelectControllerPage(i);
        if(selection==1)Frame(new Rect(274,179,720,36),Red);
        string active=controllerDevices?.ActiveName??bindings?.ActiveControllerProfileLabel??"No controller";
        string selected=active;
        if(controllerDevices!=null)foreach(var choice in controllerDevices.Choices)if(choice.key==controllerDevices.SelectedKey)selected=choice.label;
        ControllerButton(new Rect(278,221,712,29),"DEVICE: "+selected+"  •  CHANGE",2,controllerDevices!=null&&controllerDevices.Choices.Count>0);
        Text(new Rect(285,254,700,21),controllerDevices!=null&&controllerDevices.UsingFallback?"Active fallback: "+active+". Preferred device unavailable; keyboard stays available.":"Active: "+active+". Single controller; keyboard remains available. APPLY saves.",small);
        if(bindings==null){Text(new Rect(285,293,695,40),"Binding service failed to initialize. Controller setup cannot run.",wrapped);return;}
        if(controllerPage==0){
            string prompt=quickCheckpoint==null?"Start guided setup for this controller.":quickStep<6?(quickStep+1)+" / 6 — "+Idas3ControlBindings.ActionName(SetupActions[quickStep])+". Capture, then accept.":"REVIEW — these assignments will be saved only with APPLY.";
            Text(new Rect(285,282,700,30),prompt,label);
            ControllerButton(new Rect(280,318,225,32),quickCheckpoint==null?"START SETUP":"CAPTURE AGAIN",3,quickCheckpoint==null||quickStep<6);
            ControllerButton(new Rect(512,318,232,32),"ACCEPT / NEXT",4,quickCheckpoint!=null&&quickStep<6);
            ControllerButton(new Rect(751,318,239,32),"KEEP OPTIONAL SHIFT",5,quickCheckpoint!=null&&quickStep>=4&&quickStep<6);
            for(int i=0;i<6;++i)Text(new Rect(286,359+i*23,700,23),Idas3ControlBindings.ActionName(SetupActions[i])+": "+bindings.BindingName(SetupActions[i],Idas3ControlBindings.Slot.Controller),small);
            ControllerButton(new Rect(730,501,260,29),"CANCEL SETUP",6,quickCheckpoint!=null);
        }else if(controllerPage==1){
            ControllerButton(new Rect(280,280,455,28),overviewSaved?"SAVED — SWITCH TO DRAFT":"DRAFT — SWITCH TO SAVED",3);
            ControllerButton(new Rect(742,280,248,28),overviewDevices?"SHOW ASSIGNMENTS":"AVAILABLE DEVICES",4);
            if(overviewDevices){
                var choices=controllerDevices?.Choices;
                Text(new Rect(285,314,695,22),"Keyboard mappings stay available in Bind Controls (three slots per action).",small);
                int count=choices?.Count??0;
                controllerDeviceScroll=GUI.BeginScrollView(new Rect(280,344,712,183),controllerDeviceScroll,new Rect(0,0,687,Math.Max(183,count*30)));
                for(int i=0;i<count;++i)Text(new Rect(8,i*30,675,28),choices[i].label+" — "+(choices[i].connected?"available":"disconnected")+(choices[i].key==controllerDevices.SelectedKey?" / selected":""),small);
                GUI.EndScrollView();
            }else for(int i=0;i<10;++i){var action=(Idas3ControlBindings.ActionId)i;float y=310+i*22;
                Text(new Rect(285,y,156,22),Idas3ControlBindings.ActionName(action),small);
                Text(new Rect(441,y,240,22),bindings.BindingName(action,Idas3ControlBindings.Slot.Controller,!overviewSaved),small);
                string keys="";for(int k=0;k<3;++k)keys+=(k>0?" / ":"")+bindings.BindingName(action,(Idas3ControlBindings.Slot)k,!overviewSaved);
                Text(new Rect(685,y,305,22),keys,small);
            }
        }else if(controllerPage==2){
            string[] columns={"KEYBOARD 1","KEYBOARD 2","KEYBOARD 3","CONTROLLER"};
            for(int col=0;col<4;++col)if(Button(new Rect(col==3?804:462+col*114,278,col==3?180:110,24),columns[col],bindingColumn==col,true,false,bindingButton))SelectBindingColumn(col);
            for(int row=0;row<10;++row){var action=(Idas3ControlBindings.ActionId)row;float y=306+row*22;
                Text(new Rect(285,y+2,171,22),Idas3ControlBindings.ActionName(action),small);
                for(int col=0;col<4;++col){var slot=(Idas3ControlBindings.Slot)col;
                    if(Button(new Rect(col==3?804:462+col*114,y,col==3?180:110,21),bindings.BindingName(action,slot),selection==row+3&&bindingColumn==col,true,false,bindingButton)){
                        selection=row+3;bindingColumn=col;OpenBindingChoice(action,slot);
                    }
                }
            }
        }else if(controllerPage==3){
            if(!controllerTesting)ControllerButton(new Rect(282,284,704,36),"START LIVE TEST (ESC TO STOP)",3);
            else Text(new Rect(285,284,700,30),!testFocused?"UNFOCUSED — test unavailable":testSuppressed?"RELEASE CONTROLS — capture/reconnect guard active":"LIVE — evaluated draft bindings; gameplay blocked",label);
            if(controllerTesting&&testFocused&&!testSuppressed){
                float steer=controllerTestFrame.thumbLX/(controllerTestFrame.thumbLX<0?32768f:32767f);
                if(controllerTestButtons[2]||controllerTestButtons[3]){
                    // Keyboard steering is a digital demand; analog normalization remains the mapper's.
                    if(TestKey(controllerTestFrame,65)||TestKey(controllerTestFrame,68))steer=(TestKey(controllerTestFrame,68)?1:0)-(TestKey(controllerTestFrame,65)?1:0);
                }
                Text(new Rect(285,326,700,28),"Steering "+steer.ToString("0.000")+"   Accelerator "+(TestKey(controllerTestFrame,87)?1:controllerTestFrame.rightTrigger/255f).ToString("0.000")+"   Brake "+(TestKey(controllerTestFrame,83)?1:controllerTestFrame.leftTrigger/255f).ToString("0.000"),label);
                for(int i=0;i<10;++i)Text(new Rect(286+(i%2)*350,368+(i/2)*25,342,25),Idas3ControlBindings.ActionName((Idas3ControlBindings.ActionId)i)+": "+(controllerTestButtons[i]?"ON":"OFF"),small);
            }
            Text(new Rect(285,505,700,32),"Binding demand before native response/smoothing. ESC or the page buttons stop testing.",wrapped);
        }else{
            string[] names={"ENABLE", "OUTPUT", "STRENGTH", "INVERT"};var v=options.Draft;
            string[] values={v.wheelForceFeedback?"ON":"OFF",WheelDeviceName(v.wheelFeedbackDevice),Mathf.RoundToInt(v.wheelFeedbackStrength*100)+"%",v.wheelFeedbackInvert?"ON":"OFF"};
            for(int i=0;i<4;++i){float y=284+i*46;Text(new Rect(285,y+5,185,28),names[i],label);
                if(Button(new Rect(479,y,36,32),"‹",selection==i+3)){selection=i+3;AdjustWheel(i,-1);}
                ControllerButton(new Rect(521,y,416,32),values[i],i+3);
                if(Button(new Rect(943,y,40,32),"›",selection==i+3)){selection=i+3;AdjustWheel(i,1);}
            }
            Text(new Rect(285,476,700,54),(wheelFeedback?.StatusText??"No output device detected.")+" Settings do not run a motor test; output stays stopped while this menu is open.",wrapped);
        }
        string error=bindings.CaptureError??bindings.LastError;
        Text(new Rect(274,549,730,29),!string.IsNullOrEmpty(error)?error:!string.IsNullOrEmpty(notice)?notice:"Review assignments before APPLY. Discard keeps the saved configuration.",wrapped);
        if(Button(new Rect(262,583,177,35),"DISCARD DRAFT",selection==Rows+1))ControllerDiscard();
        if(Button(new Rect(458,583,254,35),"APPLY",selection==Rows+2,true,true))ControllerApply();
        if(Button(new Rect(731,583,279,35),"BACK",selection==Rows+3))Back();
    }
    private static bool TestKey(Idas3Native.FrameInput f,int key){uint word=key<32?f.key0:key<64?f.key1:key<96?f.key2:f.key3;return(word&(1u<<(key&31)))!=0;}
}
