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
            var services=(IIdas3ControlsServices)menu;
            for(int page=0;page<5;++page){
                menu.SelectControllerPage(page);
                Check(menu.SelectedTab==3&&menu.ControllerPage==page&&menu.IsOpen,"all pages use one Controls category");
                if(page==0)menu.Back();
            }
            menu.SelectTab(4);Check(menu.SelectedTab==3,"old Wheel entry maps to Controls");
            bindings.ClearDraft(Idas3ControlBindings.ActionId.Camera,Idas3ControlBindings.Slot.Primary);
            var sharedDraft=bindings.Draft.Clone();
            menu.SelectControllerPage(1);menu.SelectControllerPage(2);
            Check(Idas3ControlBindings.Equivalent(sharedDraft,bindings.Draft)&&File.ReadAllText(bindings.FilePath)==savedBindings,"navigation preserves pending edits and saved bytes");
            services.Rebind(Idas3ControlBindings.ActionId.Camera,Idas3ControlBindings.Slot.Primary);
            Check(menu.BindingChoiceVisible,"Bindings uses the existing rebind/clear chooser");menu.Back();
            bindings.CancelEdit(false);
            menu.SelectControllerPage(0);Check(menu.SettingUpControls,"Quick Setup opens immediately");menu.Back();
            Check(File.ReadAllText(bindings.FilePath)==savedBindings&&!bindings.ExperimentalDraftEnabled,"cancel preserves saved settings");
            menu.SelectControllerPage(3);Check(menu.TestingControls&&menu.BlocksGameInput,"Test Inputs starts on entry and blocks gameplay");
            menu.Back();Check(!menu.TestingControls&&menu.IsOpen,"Back leaves test in one action");
            devices.Select("keyboard",false);Check(File.ReadAllText(deviceFile)==savedDevice,"selection preview does not save");
            menu.SetOpen(false);Check(devices.SelectedKey=="automatic"&&File.ReadAllText(deviceFile)==savedDevice,"close restores saved device");
            menu.OpenAttractOptions();menu.SelectTab(3);
            for(int page=1;page<5;++page){
                menu.SelectControllerPage(page);
                bindings.ClearDraft(Idas3ControlBindings.ActionId.Camera,Idas3ControlBindings.Slot.Primary);
                options.Draft.musicVolume=.23f;options.Draft.wheelFeedbackStrength=.2f+page*.1f;
                services.Save();
                Check(!bindings.HasUnsavedChanges&&options.Current.wheelFeedbackStrength==options.Draft.wheelFeedbackStrength,"scoped save persists controller services");
                Check(options.Draft.musicVolume==.23f&&options.Current.musicVolume!=.23f,"save keeps unrelated options pending");
            }
            options.Draft.wheelFeedbackStrength=.91f;platform.fail=true;services.Save();
            menu.SetOpen(false);Check(menu.IsOpen&&options.Draft.wheelFeedbackStrength==.91f,"partial failure cannot close and discard pending edits");
            menu.SelectControllerPage(4);menu.SelectTab(0);
            Check(menu.SelectedTab==3&&options.Draft.wheelFeedbackStrength==.91f,"failure remains available for retry");
            services.Discard();platform.fail=false;
            Check(options.Draft.wheelFeedbackStrength==options.Current.wheelFeedbackStrength&&options.Draft.musicVolume==.23f,"discard restores only Controls settings");
            menu.SetOpen(false);Check(!menu.IsOpen,"discard releases close guard");
            Directory.CreateDirectory("Verification/controller-foundation");File.WriteAllText("Verification/controller-foundation/unity-checks.txt","PASS "+checks+" checks; synthetic devices only; no hardware output.\n");
            Debug.Log("PASS Controller Menu Foundation Unity checks: "+checks);
        }finally{UnityEngine.Object.DestroyImmediate(go);devices?.Dispose();InputSystem.RemoveDevice(pad);if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
