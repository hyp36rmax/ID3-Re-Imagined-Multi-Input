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
            // Overview starts at row 1; mode is row 7. Exercise the public menu path.
            for(int i=0;i<6;++i)menu.Navigate(1);menu.Activate();Check(b.ExperimentalDraftEnabled&&!b.ExperimentalEnabled,"mode remains draft until Save Changes");
            void Poll(){now+=.02;InputSystem.Update();provider.Tick(false);b.Poll(_=>false,default,now,provider.Controls,provider.Snapshot);}
            Idas3EndpointSnapshot Endpoint(Gamepad pad)=>provider.Snapshot.Endpoints.First(e=>e.Identity.Fields.Any(f=>f.Name=="runtimeId"&&f.Value==pad.deviceId.ToString()));
            bool Assign(B.ActionId action,Gamepad pad,string path,int direction,float rest){var e=Endpoint(pad);return b.TrySetExperimentalControl(action,e.Token,e.ConnectionGeneration,path,direction,rest);}
            Poll();Check(Assign(B.ActionId.SteerLeft,wheel,"leftStick/x",-1,0)&&Assign(B.ActionId.SteerRight,wheel,"leftStick/x",1,0),"wheel pair");
            Check(Assign(B.ActionId.Accelerate,pedals,"rightTrigger",1,0)&&Assign(B.ActionId.Brake,pedals,"leftTrigger",1,0)&&Assign(B.ActionId.ShiftUp,shifter,"buttonSouth",1,0),"independent pedals and shifter");
            options.Draft.musicVolume=.23f;menu.SelectControllerPage(1);for(int i=0;i<5;++i)menu.Navigate(1);menu.Activate();
            Check(b.ExperimentalEnabled&&!b.HasExperimentalChanges&&options.Draft.musicVolume==.23f&&options.Current.musicVolume!=.23f,"unified save commits experimental assignments and preserves unrelated draft");
            Check(File.ReadAllText(b.FilePath)==legacy,"menu save preserves untouched legacy mappings");Poll();
            InputSystem.QueueStateEvent(wheel,new GamepadState{leftStick=new Vector2(-.5f,0)});
            InputSystem.QueueStateEvent(pedals,new GamepadState{rightTrigger=.75f,leftTrigger=.25f});
            InputSystem.QueueStateEvent(shifter,new GamepadState().WithButton(GamepadButton.South));Poll();
            var game=default(Idas3Native.FrameInput);b.ApplyDriving(ref game);var preview=b.EvaluateDraftDriving();
            Check(game.thumbLX==-16384&&game.rightTrigger==191&&game.leftTrigger==64&&game.padButtons==0x2000,"real Unity state buffers evaluate three endpoints simultaneously");
            Check(game.thumbLX==preview.thumbLX&&game.rightTrigger==preview.rightTrigger&&game.padButtons==preview.padButtons,"menu and game share evaluator");
            menu.SelectControllerPage(3);menu.Navigate(1);menu.Navigate(1);menu.Activate();Check(menu.TestingControls&&menu.BlocksGameInput,"testing blocks gameplay submission");
            Check(b.ExperimentalBlocksFeedback,"experimental ownership unresolved: FFB disabled");
            var reload=new B();reload.Initialize(root);reload.Poll(_=>false,default,now,null,provider.Snapshot);game=default;reload.ApplyDriving(ref game);
            Check(reload.ExperimentalEnabled&&game.rightTrigger==0&&reload.BindingName(B.ActionId.Accelerate,B.Slot.Controller).Contains("reassign"),"Unity JsonUtility preserves preferences without restoring session identity");
            var endpoint=Endpoint(pedals);InputSystem.RemoveDevice(pedals);Poll();game=default;b.ApplyDriving(ref game);
            Check(game.rightTrigger==0&&game.leftTrigger==0&&game.thumbLX==-16384&&game.padButtons==0x2000,"removal only neutralizes affected actions");
            InputSystem.AddDevice(pedals);Poll();Check(!b.HasControllerAssignment(B.ActionId.Accelerate),"reconnect requires explicit reassignment");
            menu.Back();menu.SelectControllerPage(1);for(int i=0;i<6;++i)menu.Navigate(1);menu.Activate();
            menu.SelectControllerPage(1);for(int i=0;i<4;++i)menu.Navigate(1);menu.Activate();Check(b.ExperimentalDraftEnabled&&b.ExperimentalEnabled,"Discard cancels mode change");
            Directory.CreateDirectory("Verification/multi-input");File.WriteAllText("Verification/multi-input/unity-checks.txt","PASS "+checks+" real Unity synthetic checks; physical hardware and rendered UI remain pending.\n");
            Debug.Log("PASS multi-input Unity checks: "+checks);
        }finally{UnityEngine.Object.DestroyImmediate(go);if(wheel.added)InputSystem.RemoveDevice(wheel);if(pedals.added)InputSystem.RemoveDevice(pedals);if(shifter.added)InputSystem.RemoveDevice(shifter);if(Directory.Exists(root))Directory.Delete(root,true);}
    }
}
#endif
