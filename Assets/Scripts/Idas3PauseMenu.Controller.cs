using System;
using UnityEngine;

public sealed partial class Idas3PauseMenu
{
    private static readonly string[] ControllerPages={"QUICK SETUP","OVERVIEW","BIND CONTROLS","TEST CONTROLS","FORCE FEEDBACK"};
    private static readonly Idas3ControlBindings.ActionId[] SetupActions={Idas3ControlBindings.ActionId.SteerLeft,Idas3ControlBindings.ActionId.SteerRight,
        Idas3ControlBindings.ActionId.Accelerate,Idas3ControlBindings.ActionId.Brake,Idas3ControlBindings.ActionId.ShiftUp,Idas3ControlBindings.ActionId.ShiftDown};
    private int controllerPage=1,quickStep;
    private bool controllerTesting,overviewSaved,overviewDevices,testFocused,testSuppressed;
    private Vector2 controllerDeviceScroll,selectedNameScroll,activeNameScroll,outputNameScroll,controllerNoticeScroll;
    private bool controllerSaveIncomplete;
    private string controllerSaveMessage;
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
        if(!controllerSaveIncomplete&&quickStep<6)CancelQuickSetup();controllerTesting=false;controllerPage=Wrap(page,5);wheelEditing=false;selection=1;notice="";
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
            ++quickStep;notice=quickStep==6?"Review the assignments below, then Save Changes. Cancel restores your prior draft.":"Accepted in draft. Release controls before capturing the next action.";
        }else if(selection==5&&quickCheckpoint!=null&&quickStep>=4&&quickStep<6){++quickStep;notice="Existing shift assignment kept.";}
        else if(selection==6){CancelQuickSetup();notice="Quick Setup cancelled; previous draft restored.";}
    }
    private void ControllerApply(){
        if(bindings==null||options==null||controllerDevices==null){notice="Controller services unavailable; nothing saved.";return;}
        if(quickCheckpoint!=null&&quickStep<6){notice="Finish and review Quick Setup before saving, or cancel it.";return;}
        if(options.DisplayConfirmationPending){notice="Confirm or revert the pending display change before saving Controller settings.";return;}
        var result=Idas3ControllerSave.Save(bindings.ApplyDraft,()=>bindings.LastError,
            controllerDevices.ApplySelectionEdit,()=>controllerDevices.LastError,
            options.ApplyWheelSettings,()=>options.LastError);
        // Never restore a pre-Setup checkpoint over bindings that have reached disk.
        if(result.BindingsSaved)quickCheckpoint=null;
        controllerSaveIncomplete=!result.Complete;controllerSaveMessage=result.Message;notice=result.Message;
    }

    private void ControllerDiscard(){
        controllerSaveIncomplete=false;controllerSaveMessage=null;CancelQuickSetup();controllerTesting=false;controllerDevices?.CancelSelectionEdit();controllerDevices?.BeginSelectionEdit();
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
        ScrollControllerName(new Rect(278,217,555,31),"Selected: "+selected,ref selectedNameScroll);
        ControllerButton(new Rect(841,219,149,29),"CHANGE DEVICE",2,controllerDevices!=null&&controllerDevices.Choices.Count>0);
        ScrollControllerName(new Rect(278,250,712,30),(controllerDevices!=null&&controllerDevices.UsingFallback?"Active fallback: ":"Active: ")+active,ref activeNameScroll);
        if(bindings==null){Text(new Rect(285,293,695,40),"Binding service failed to initialize. Controller setup cannot run.",wrapped);return;}
        if(controllerPage==0){
            string prompt=quickCheckpoint==null?"Start guided setup for this controller.":quickStep<6?(quickStep<2?"1 / 5 — Steering: "+(quickStep==0?"turn left":"turn right"):(quickStep)+" / 5 — "+Idas3ControlBindings.ActionName(SetupActions[quickStep]))+". Capture, then accept.":"REVIEW — check assignments, then Save Changes.";
            Text(new Rect(285,282,700,30),prompt,label);
            ControllerButton(new Rect(280,318,225,32),quickCheckpoint==null?"START SETUP":"CAPTURE AGAIN",3,quickCheckpoint==null||quickStep<6);
            ControllerButton(new Rect(512,318,232,32),"ACCEPT / NEXT",4,quickCheckpoint!=null&&quickStep<6);
            ControllerButton(new Rect(751,318,239,32),"KEEP OPTIONAL SHIFT",5,quickCheckpoint!=null&&quickStep>=4&&quickStep<6);
            Text(new Rect(286,357,700,42),"Steering — Left: "+bindings.BindingName(SetupActions[0],Idas3ControlBindings.Slot.Controller)+"\nRight: "+bindings.BindingName(SetupActions[1],Idas3ControlBindings.Slot.Controller),wrapped);
            for(int i=2;i<6;++i)Text(new Rect(286,404+(i-2)*23,700,23),Idas3ControlBindings.ActionName(SetupActions[i])+": "+bindings.BindingName(SetupActions[i],Idas3ControlBindings.Slot.Controller),small);
            ControllerButton(new Rect(730,501,260,29),"CANCEL SETUP",6,quickCheckpoint!=null);
        }else if(controllerPage==1){
            ControllerButton(new Rect(280,280,455,28),overviewSaved?"SAVED — SWITCH TO DRAFT":"DRAFT — SWITCH TO SAVED",3);
            ControllerButton(new Rect(742,280,248,28),overviewDevices?"SHOW ASSIGNMENTS":"AVAILABLE DEVICES",4);
            if(overviewDevices){
                var choices=controllerDevices?.Choices;
                Text(new Rect(285,314,695,22),"Keyboard mappings stay available in Bind Controls (three slots per action).",small);
                int count=choices?.Count??0;
                var names=new string[count];var heights=new float[count];float total=0;
                for(int i=0;i<count;++i){names[i]=choices[i].label+" — "+(choices[i].connected?"available":"disconnected")+(choices[i].key==controllerDevices.SelectedKey?" / selected":"");
                    heights[i]=Math.Max(30,wrapped.CalcHeight(new GUIContent(names[i]),665)+8);total+=heights[i];}
                controllerDeviceScroll=GUI.BeginScrollView(new Rect(280,344,712,183),controllerDeviceScroll,new Rect(0,0,687,Math.Max(183,total)));
                float nameY=0;for(int i=0;i<count;++i){Text(new Rect(8,nameY,665,heights[i]),names[i],wrapped);nameY+=heights[i];}
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
            Text(new Rect(285,505,700,32),"Evaluated assigned input before native response processing. ESC or the page buttons stop testing.",wrapped);
        }else{
            string[] names={"ENABLE", "OUTPUT", "STRENGTH", "INVERT"};var v=options.Draft;
            string[] values={v.wheelForceFeedback?"ON":"OFF",WheelDeviceName(v.wheelFeedbackDevice),Mathf.RoundToInt(v.wheelFeedbackStrength*100)+"%",v.wheelFeedbackInvert?"ON":"OFF"};
            for(int i=0;i<4;++i){float y=284+i*46;Text(new Rect(285,y+5,185,28),names[i],label);
                if(Button(new Rect(479,y,36,32),"‹",selection==i+3)){selection=i+3;AdjustWheel(i,-1);}
                if(i==1)ScrollControllerName(new Rect(521,y,416,32),values[i],ref outputNameScroll);
                else ControllerButton(new Rect(521,y,416,32),values[i],i+3);
                if(Button(new Rect(943,y,40,32),"›",selection==i+3)){selection=i+3;AdjustWheel(i,1);}
            }
            Text(new Rect(285,476,700,54),(wheelFeedback?.StatusText??"No output device detected.")+" Settings do not run a motor test; output stays stopped while this menu is open.",wrapped);
        }
        string error=bindings.CaptureError??bindings.LastError;
        string message=controllerSaveIncomplete?controllerSaveMessage:!string.IsNullOrEmpty(error)?error:!string.IsNullOrEmpty(notice)?notice:"Save Changes saves all Controller pages. Discard Changes restores saved Controller settings.";
        float messageHeight=Math.Max(33,wrapped.CalcHeight(new GUIContent(message),710));
        controllerNoticeScroll=GUI.BeginScrollView(new Rect(274,544,730,38),controllerNoticeScroll,new Rect(0,0,710,messageHeight));
        Text(new Rect(0,0,710,messageHeight),message,wrapped);GUI.EndScrollView();
        if(Button(new Rect(262,583,177,35),"DISCARD CHANGES",selection==Rows+1))ControllerDiscard();
        if(Button(new Rect(458,583,254,35),"SAVE CHANGES",selection==Rows+2,true,true))ControllerApply();
        if(Button(new Rect(731,583,279,35),"BACK",selection==Rows+3))Back();
    }
    // Preserve the complete driver name; horizontal scrolling handles arbitrary lengths.
    private void ScrollControllerName(Rect rect,string name,ref Vector2 scroll){
        float width=Math.Max(rect.width-18,small.CalcSize(new GUIContent(name)).x+8);
        scroll=GUI.BeginScrollView(rect,scroll,new Rect(0,0,width,17));
        Text(new Rect(0,0,width,17),name,small);GUI.EndScrollView();
    }
    private static bool TestKey(Idas3Native.FrameInput f,int key){uint word=key<32?f.key0:key<64?f.key1:key<96?f.key2:f.key3;return(word&(1u<<(key&31)))!=0;}
}
