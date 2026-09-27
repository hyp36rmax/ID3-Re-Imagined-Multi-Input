// Portable adapters for Unity's public surface, not Unity event buffers/drivers.
// Actual provider, snapshot, gamepad conversion and binding code are linked unchanged.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem.Controls;
namespace UnityEngine.InputSystem {
public struct Description { public string interfaceName,manufacturer,product,serial,capabilities,deviceClass,version; }
public enum InputDeviceChange { Added, Removed, Disconnected, Reconnected, Disabled, Enabled, ConfigurationChanged }
public struct StateBlock { public uint byteOffset,bitOffset,sizeInBits; }
public class InputControl {public string path,name,displayName;public bool noisy,synthetic;public InputControl parent;public StateBlock stateBlock;}
public class InputDevice:InputControl {public int deviceId;public bool added=true,enabled=true;public string layout="Synthetic";public Description description;public List<InputControl> allControls=new List<InputControl>();}
public class Keyboard:InputDevice {} public class Pointer:InputDevice {} public class Sensor:InputDevice {}
public class Gamepad:InputDevice {
public static Gamepad current;public static List<Gamepad> all=new List<Gamepad>();
public StickControl leftStick=new StickControl(),rightStick=new StickControl();public DpadControl dpad=new DpadControl();
public ButtonControl startButton=new ButtonControl(),selectButton=new ButtonControl(),leftStickButton=new ButtonControl(),rightStickButton=new ButtonControl(),leftShoulder=new ButtonControl(),rightShoulder=new ButtonControl(),buttonSouth=new ButtonControl(),buttonEast=new ButtonControl(),buttonWest=new ButtonControl(),buttonNorth=new ButtonControl();
public AxisControl leftTrigger=new AxisControl(),rightTrigger=new AxisControl();
}
public class Joystick:InputDevice {public StickControl stick;public ButtonControl trigger;public DpadControl hatswitch;}
public static class InputSystem {
public static List<InputDevice> devices=new List<InputDevice>();public static event Action<InputDevice,InputDeviceChange> onDeviceChange;
public static void Event(InputDevice d,InputDeviceChange c)=>onDeviceChange?.Invoke(d,c);
public static void Add(InputDevice d){d.added=true;devices.Add(d);Event(d,InputDeviceChange.Added);}
public static void Remove(InputDevice d){d.added=false;devices.Remove(d);Event(d,InputDeviceChange.Removed);}
}
}
namespace UnityEngine.InputSystem.Controls {
public class AxisControl:InputControl {public bool normalize,invert,fail,unsupported;public float normalizeZero,normalizeMin,value;public int reads;public float ReadUnprocessedValue(){++reads;if(unsupported)throw new NotSupportedException("synthetic unsupported read");if(fail)throw new InvalidOperationException("synthetic unreadable control");return value;}}
public class ButtonControl:AxisControl {public bool isPressed=>ReadUnprocessedValue()>=.5f;}
public class StickControl:InputControl {public Vector2 value;public Vector2 ReadUnprocessedValue()=>value;}
public class DpadControl:StickControl {public ButtonControl up=new ButtonControl(),down=new ButtonControl(),left=new ButtonControl(),right=new ButtonControl();}
}
namespace UnityEngine.InputSystem.XInput {public class XInputController:Gamepad{} public class XInputControllerWindows:XInputController{}}
namespace UnityEngine.InputSystem.HID {
public class HID:UnityEngine.InputSystem.Joystick {
public HIDDeviceDescriptor hidDescriptor;
public enum UsagePage {GenericDesktop=1,Button=9}
public enum GenericDesktop {Joystick=4,Gamepad=5,MultiAxisController=8,X=48,Y,Z,Rx,Ry,Rz,Slider,Dial,Wheel,HatSwitch,Vx,Vy,Vz,Vbrx,Vbry,Vbrz,Select,Start,DpadUp,DpadDown,DpadLeft,DpadRight}
public enum HIDReportType {Input,Output,Feature}
public struct HIDElementDescriptor {public UsagePage usagePage;public int usage,reportOffsetInBits,reportSizeInBits;public HIDReportType reportType;public bool isConstant;}
public struct HIDDeviceDescriptor {public UsagePage usagePage;public int usage;public HIDElementDescriptor[] elements;public static HIDDeviceDescriptor FromJson(string text)=>JsonUtility.FromJson<HIDDeviceDescriptor>(text);}
}
}
public static partial class Idas3Native {
public struct PadState {public uint packet;public GamepadState gamepad;}
public struct GamepadState {public ushort buttons;public byte leftTrigger,rightTrigger;public short thumbLX,thumbLY,thumbRX,thumbRY;}
public static uint ReadGamepad(uint slot,out PadState state){state=default;return 1167;}
}
public static class Idas3WheelFeedback {public struct InputIdentity {public bool connected;public string key;public uint vendorId,productId;}}
