using System;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;

// Explicit Unity editor validation. Never runs during normal startup or creates motor output.
public static class Idas3ControllerFoundationChecks
{
    private sealed class Platform:Idas3GameOptions.IPlatform {
        public int Width=>1280;public int Height=>720;public int DisplayMode=>0;public double Now=>0;
        public Idas3GameOptions.ResolutionChoice[] Resolutions=>new[]{new Idas3GameOptions.ResolutionChoice(1280,720)};
        public bool fail;
        public void Apply(Idas3GameOptions.Values a,Idas3GameOptions.Values b,bool display){if(fail)throw new Exception("synthetic FFB persistence failure");}
    }
    static int checks;
    static void Check(bool ok,string why){++checks;if(!ok)throw new Exception(why);}
    public static void Run(){
        checks=0;string root=Path.Combine(Path.GetTempPath(),"id3-menu-foundation-"+Guid.NewGuid().ToString("N"));
        var go=new GameObject("Controller foundation checks");var pad=InputSystem.AddDevice<Gamepad>();
        Idas3ControllerDevices devices=null;
        try{
            var bindings=new Idas3ControlBindings();bindings.Initialize(root);
            var platform=new Platform();var options=new Idas3GameOptions(platform);options.Initialize(root);
            devices=new Idas3ControllerDevices(()=>0,(uint slot,out Idas3Native.PadState state)=>{state=default;return 1167;},d=>d==pad);
            devices.ActiveDeviceChanged+=()=>{bindings.SelectControllerProfile(devices.ActiveProfileKey,devices.ActiveName,devices.ActiveIsGeneric);bindings.ControllerDeviceChanged();};
            devices.Initialize(root);Check(devices.Controls.Count>0,"synthetic selected controller exposes existing controls");
            devices.Select("automatic");string deviceFile=Path.Combine(root,"controller-device.json"),savedDevice=File.ReadAllText(deviceFile);
            bindings.BeginEdit();Check(bindings.ApplyDraft(),"initial binding fixture");string savedBindings=File.ReadAllText(bindings.FilePath);
            var menu=go.AddComponent<Idas3PauseMenu>();menu.Initialize(options);menu.InitializeBindings(bindings);menu.InitializeControllerDevices(devices);menu.OpenAttractOptions();menu.SelectTab(3);
            bindings.Poll(k=>false,default,0,devices.Controls,devices.Snapshot);
            for(int page=0;page<5;++page){menu.SelectControllerPage(page);Check(menu.SelectedTab==4&&menu.ControllerPage==page&&menu.IsOpen,"all five pages reachable");}
            menu.SelectTab(3);Check(menu.SelectedTab==3,"CONTROLS stays a separate category");
            bindings.ClearDraft(Idas3ControlBindings.ActionId.Camera,Idas3ControlBindings.Slot.Primary);
            var sharedDraft=bindings.Draft.Clone();menu.SelectTab(4);
            Check(menu.SelectedTab==4&&Idas3ControlBindings.Equivalent(sharedDraft,bindings.Draft),"WHEEL preserves CONTROLS pending bindings");
            menu.SelectTab(3);Check(Idas3ControlBindings.Equivalent(sharedDraft,bindings.Draft)&&File.ReadAllText(bindings.FilePath)==savedBindings,"category navigation neither saves nor replaces draft");
            menu.SelectBindingColumn(0);menu.Activate();Check(menu.BindingChoiceVisible,"familiar CONTROLS table opens shared binding chooser");menu.Back();
            bindings.CancelEdit(false);
            menu.SelectControllerPage(0);menu.Navigate(1);menu.Navigate(1);menu.Activate();Check(bindings.IsCapturing&&bindings.ExperimentalDraftEnabled&&!bindings.ExperimentalEnabled,"New Wheel Setup captures in separate draft without switching gameplay");
            menu.Back();bindings.Poll(k=>false,default,1,devices.Controls,devices.Snapshot);menu.Back();
            Check(File.ReadAllText(bindings.FilePath)==savedBindings&&!bindings.HasUnsavedChanges&&!bindings.ExperimentalDraftEnabled,"Quick Setup cancel retains saved/draft assignments and original mode");
            devices.Select("keyboard",false);Check(File.ReadAllText(deviceFile)==savedDevice&&devices.SelectionHasChanges,"preview selection does not save");
            menu.SetOpen(false);Check(devices.SelectedKey=="automatic"&&File.ReadAllText(deviceFile)==savedDevice,"closing restores device selection");
            menu.OpenAttractOptions();menu.SelectTab(3);menu.SelectControllerPage(2);menu.SelectBindingColumn(0);menu.Activate();
            Check(menu.BindingChoiceVisible,"Bind Controls opens existing capture/clear chooser");menu.Back();
            menu.SelectControllerPage(3);menu.Navigate(1);menu.Navigate(1);menu.Activate();Check(menu.TestingControls&&menu.BlocksGameInput,"test mode blocks gameplay");
            menu.Back();Check(!menu.TestingControls&&menu.IsOpen,"Back leaves test without closing menu");
            menu.SelectControllerPage(4);menu.Navigate(1);menu.Navigate(1);bool previous=options.Draft.wheelForceFeedback;menu.Activate();
            Check(options.Draft.wheelForceFeedback!=previous&&options.Current.wheelForceFeedback==previous,"FFB page edits draft only without backend");
            menu.SetOpen(false);Check(File.ReadAllText(deviceFile)==savedDevice&&File.ReadAllText(bindings.FilePath)==savedBindings,"no implicit saves across pages");
            menu.OpenAttractOptions();menu.SelectTab(3);
            int[] pageRows={6,4,12,3,6};
            for(int page=0;page<5;++page){
                menu.SelectControllerPage(page);bindings.Poll(k=>false,default,10+page,devices.Controls,devices.Snapshot);
                bindings.ClearDraft(Idas3ControlBindings.ActionId.Camera,Idas3ControlBindings.Slot.Primary);
                devices.Select(page%2==0?"keyboard":"automatic",false);
                options.Draft.musicVolume=.23f;options.Draft.wheelFeedbackStrength=.2f+page*.1f;
                bindings.Poll(k=>false,default,20+page,devices.Controls,devices.Snapshot);
                for(int n=0;n<pageRows[page]+1;++n)menu.Navigate(1);menu.Activate();
                Check(!bindings.HasUnsavedChanges&&!devices.SelectionHasChanges&&options.Current.wheelFeedbackStrength==options.Draft.wheelFeedbackStrength,"Save Changes on page "+page+" commits all Controller services");
                Check(options.Draft.musicVolume==.23f&&options.Current.musicVolume!=.23f,"save retains unrelated draft on page "+page);
            }
            menu.SelectTab(3);options.Draft.wheelFeedbackStrength=.83f;
            // Familiar CONTROLS has one device row, ten action rows, then Discard / Save / Back.
            for(int n=0;n<12;++n)menu.Navigate(1);menu.Activate();
            Check(options.Current.wheelFeedbackStrength==.83f&&options.Draft.musicVolume==.23f,"CONTROLS Save uses shared scoped coordinator");
            menu.SelectTab(3);options.Draft.wheelFeedbackStrength=.72f;
            for(int n=0;n<11;++n)menu.Navigate(1);menu.Activate();
            Check(options.Draft.wheelFeedbackStrength==.83f&&options.Draft.musicVolume==.23f,"CONTROLS Discard preserves unrelated drafts");
            menu.SelectControllerPage(1);
            for(int n=0;n<7;++n)menu.Navigate(1);menu.Activate();
            Check(menu.CategoryFocused,"WHEEL Back footer remains reachable by directional navigation");
            menu.SelectControllerPage(1);options.Draft.wheelFeedbackStrength=.91f;platform.fail=true;
            for(int n=0;n<5;++n)menu.Navigate(1);menu.Activate();
            menu.SetOpen(false);Check(menu.IsOpen&&options.Draft.wheelFeedbackStrength==.91f,"partial save cannot silently close and discard");
            menu.SelectControllerPage(4);Check(options.Draft.wheelFeedbackStrength==.91f,"partial draft survives page navigation");
            menu.SelectTab(0);Check(menu.ControllerPage==4&&menu.IsOpen,"partial save remains available for retry/discard");
            // Explicit Discard retains the already saved components and unrelated options.
            for(int n=0;n<6;++n)menu.Navigate(1);menu.Activate();platform.fail=false;
            Check(options.Draft.wheelFeedbackStrength==options.Current.wheelFeedbackStrength&&options.Draft.musicVolume==.23f,"Discard after partial save restores only pending Controller edits");
            menu.SetOpen(false);Check(!menu.IsOpen,"explicit discard releases close guard");
            Directory.CreateDirectory("Verification/controller-foundation");File.WriteAllText("Verification/controller-foundation/unity-checks.txt","PASS "+checks+" checks; synthetic devices only; no hardware output.\n");
            Debug.Log("PASS Controller Menu Foundation Unity checks: "+checks);
        }finally{UnityEngine.Object.DestroyImmediate(go);devices?.Dispose();InputSystem.RemoveDevice(pad);if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
