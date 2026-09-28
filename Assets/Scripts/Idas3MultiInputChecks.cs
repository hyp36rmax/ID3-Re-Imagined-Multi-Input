#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using B=Idas3ControlBindings;

// Invoked explicitly by the sample builder. Real Unity state buffers and serializer;
// synthetic devices only, never hardware acceptance or force output.
public static class Idas3MultiInputChecks {
    sealed class Platform:Idas3GameOptions.IPlatform {
        public int Width=>1280;public int Height=>720;public int DisplayMode=>0;public double Now=>0;
        public Idas3GameOptions.ResolutionChoice[] Resolutions=>new[]{new Idas3GameOptions.ResolutionChoice(1280,720)};
        public void Apply(Idas3GameOptions.Values a,Idas3GameOptions.Values b,bool display){}
    }
    static int checks;
    static void Check(bool ok,string why){++checks;if(!ok)throw new InvalidOperationException(why);}
    public static void Run(){
        checks=0;string root=Path.Combine(Path.GetTempPath(),"id3-multi-unity-"+Guid.NewGuid().ToString("N"));
        var wheel=InputSystem.AddDevice<Gamepad>();var pedals=InputSystem.AddDevice<Gamepad>();var shifter=InputSystem.AddDevice<Gamepad>();
        var go=new GameObject("Multi-input menu checks");double now=0;
        using(var provider=new Idas3ControllerDevices(()=>now,(uint slot,out Idas3Native.PadState p)=>{p=default;return 1167;},d=>d==wheel||d==pedals||d==shifter))try{
            provider.Initialize(root);var b=new B();b.Initialize(root);b.ApplyDraft();string legacy=File.ReadAllText(b.FilePath);
            var options=new Idas3GameOptions(new Platform());options.Initialize(root);
            var menu=go.AddComponent<Idas3PauseMenu>();menu.Initialize(options);menu.InitializeBindings(b);menu.InitializeControllerDevices(provider);menu.OpenAttractOptions();menu.SelectTab(3);menu.SelectControllerPage(1);
            var services = (IIdas3ControlsServices)menu;
            // The batch check has no IMGUI event. Exercise the real session and
            // adapter; rendered navigation is a separate player acceptance check.
            b.BeginWheelSetup();
            Check(b.ExperimentalDraftEnabled&&!b.ExperimentalEnabled,"setup remains draft until Save Changes");
            void Poll(){now+=.02;InputSystem.Update();provider.Tick(false);b.Poll(_=>false,default,now,provider.Controls,provider.Snapshot);}
            Idas3EndpointSnapshot Endpoint(Gamepad pad)=>provider.Snapshot.Endpoints.First(e=>e.Identity.Fields.Any(f=>f.Name=="runtimeId"&&f.Value==pad.deviceId.ToString()));
            bool Assign(B.ActionId action,Gamepad pad,string path,int direction,float rest){var e=Endpoint(pad);return b.TrySetExperimentalControl(action,e.Token,e.ConnectionGeneration,path,direction,rest);}
            Poll();Check(Assign(B.ActionId.SteerLeft,wheel,"leftStick/x",-1,0)&&Assign(B.ActionId.SteerRight,wheel,"leftStick/x",1,0),"wheel pair");
            Check(Assign(B.ActionId.Accelerate,pedals,"rightTrigger",1,0)&&Assign(B.ActionId.Brake,pedals,"leftTrigger",1,0)&&Assign(B.ActionId.ShiftUp,shifter,"buttonSouth",1,0),"independent pedals and shifter");
            options.Draft.musicVolume=.23f;services.Save();
            Check(b.ExperimentalEnabled&&!b.HasExperimentalChanges&&options.Draft.musicVolume==.23f&&options.Current.musicVolume!=.23f,"unified save commits experimental assignments and preserves unrelated draft");
            Check(File.ReadAllText(b.FilePath)==legacy,"menu save preserves untouched legacy mappings");Poll();
            InputSystem.QueueStateEvent(wheel,new GamepadState{leftStick=new Vector2(-.5f,0)});
            InputSystem.QueueStateEvent(pedals,new GamepadState{rightTrigger=.75f,leftTrigger=.25f});
            InputSystem.QueueStateEvent(shifter,new GamepadState().WithButton(GamepadButton.South));Poll();
            var game=default(Idas3Native.FrameInput);b.ApplyDriving(ref game);var preview=b.EvaluateDraftDriving();
            Check(game.thumbLX==-16384&&game.rightTrigger==191&&game.leftTrigger==64&&game.padButtons==0x2000,"real Unity state buffers evaluate three endpoints simultaneously");
            Check(game.thumbLX==preview.thumbLX&&game.rightTrigger==preview.rightTrigger&&game.padButtons==preview.padButtons,"menu and game share evaluator");
            var menuPedals = Endpoint(pedals);
            Check(b.TrySetMenuControl(B.MenuActionId.Confirm, menuPedals.Token, menuPedals.ConnectionGeneration, "rightTrigger", 1, 0), "menu group deliberately reuses driving pedal");
            Check(b.ApplyExperimentalDraft(), "Unity version 2 navigation save");
            InputSystem.QueueStateEvent(pedals, new GamepadState()); Poll(); b.EvaluateMenuNavigation(2, true, false);
            InputSystem.QueueStateEvent(pedals, new GamepadState{rightTrigger=.8f}); Poll(); b.EvaluateMenuNavigation(2, true, false);
            Check(b.MenuEvent(B.MenuActionId.Confirm), "Unity pedal threshold event");
            Poll(); b.EvaluateMenuNavigation(2, true, false); Check(b.MenuEvents==0, "Unity held Confirm never repeats");
            var navigationReload = new B(); navigationReload.Initialize(root);
            Check(navigationReload.MenuBindingName(B.MenuActionId.Confirm).Contains("reassign"), "actual JsonUtility reload retains menu preference, not session identity");
            menu.SelectControllerPage(2); services.RebindMenu(B.MenuActionId.Up);
            Check(menu.BindingChoiceVisible, "Menu adapter opens shared chooser"); menu.Back();
            menu.SelectControllerPage(3);Check(menu.TestingControls&&menu.BlocksGameInput,"testing blocks gameplay submission");
            Check(b.ExperimentalBlocksFeedback,"experimental ownership unresolved: FFB disabled");
            var reload=new B();reload.Initialize(root);reload.Poll(_=>false,default,now,null,provider.Snapshot);game=default;reload.ApplyDriving(ref game);
            Check(reload.ExperimentalEnabled&&game.rightTrigger==0&&reload.BindingName(B.ActionId.Accelerate,B.Slot.Controller).Contains("reassign"),"Unity JsonUtility preserves preferences without restoring session identity");
            InputSystem.QueueStateEvent(wheel,new GamepadState()); InputSystem.QueueStateEvent(shifter,new GamepadState()); Poll();
            InputSystem.QueueStateEvent(wheel,new GamepadState{leftStick=new Vector2(-.5f,0)}); InputSystem.QueueStateEvent(shifter,new GamepadState().WithButton(GamepadButton.South)); Poll();
            var endpoint=Endpoint(pedals);InputSystem.RemoveDevice(pedals);Poll();game=default;b.ApplyDriving(ref game);
            Check(game.rightTrigger==0&&game.leftTrigger==0&&game.thumbLX==-16384&&game.padButtons==0x2000,"removal only neutralizes affected actions");
            InputSystem.AddDevice(pedals);Poll();Check(!b.HasControllerAssignment(B.ActionId.Accelerate),"reconnect requires explicit reassignment");
            menu.Back();menu.SelectControllerPage(1);services.SelectSavedController();
            services.Discard();Check(b.ExperimentalDraftEnabled&&b.ExperimentalEnabled,"Discard cancels mode change");
            Directory.CreateDirectory("Verification/multi-input");File.WriteAllText("Verification/multi-input/unity-checks.txt","PASS "+checks+" real Unity synthetic checks; physical hardware and rendered UI remain pending.\n");
            Debug.Log("PASS multi-input Unity checks: "+checks);
        }finally{UnityEngine.Object.DestroyImmediate(go);if(wheel.added)InputSystem.RemoveDevice(wheel);if(pedals.added)InputSystem.RemoveDevice(pedals);if(shifter.added)InputSystem.RemoveDevice(shifter);if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
#endif
