// Portable test adapters only. Production binding code is compiled unchanged.
// This is not Unity serialization or a Unity/player build.
using System.Text.Json;
namespace UnityEngine {
public enum KeyCode {A=97,Backspace=8,C=99,D=100,DownArrow=274,E=101,Escape=27,F1=282,F11=292,F2=283,F3=284,F5=286,H=104,JoystickButton0=330,KeypadEnter=271,LeftAlt=308,LeftArrow=276,LeftControl=306,LeftShift=304,Mouse0=323,None=0,Q=113,R=114,Return=13,RightAlt=307,RightArrow=275,RightControl=305,RightShift=303,S=115,Space=32,UpArrow=273,W=119}
public static class JsonUtility {
static readonly JsonSerializerOptions Options=new JsonSerializerOptions{IncludeFields=true};
public static string ToJson(object o,bool pretty=false)=>JsonSerializer.Serialize(o,o.GetType(),Options);
public static void FromJsonOverwrite(string text,object target){
using var doc=JsonDocument.Parse(text);
foreach(var f in target.GetType().GetFields())if(doc.RootElement.TryGetProperty(f.Name,out var value))f.SetValue(target,JsonSerializer.Deserialize(value.GetRawText(),f.FieldType,Options));
}
public static T FromJson<T>(string text)=>JsonSerializer.Deserialize<T>(text,Options);
}
}
public static partial class Idas3Native {
public struct FrameInput {
public uint key0,key1,key2,key3,key4,key5,key6,key7,padConnected,padButtons,leftTrigger,rightTrigger;
public int thumbLX,thumbLY,thumbRX,thumbRY;
public void SetKey(int key){uint b=1u<<(key&31);switch(key>>5){case 0:key0|=b;break;case 1:key1|=b;break;case 2:key2|=b;break;case 3:key3|=b;break;case 4:key4|=b;break;case 5:key5|=b;break;case 6:key6|=b;break;case 7:key7|=b;break;}}
}
}
namespace UnityEngine {
public struct Vector2 {
public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public static Vector2 zero=>default;
public static bool operator ==(Vector2 a,Vector2 b)=>a.x==b.x&&a.y==b.y;
public static bool operator !=(Vector2 a,Vector2 b)=>!(a==b);
public override bool Equals(object o)=>o is Vector2 b&&this==b;public override int GetHashCode()=>x.GetHashCode()^y.GetHashCode();
}
public enum FullScreenMode{Windowed,ExclusiveFullScreen,FullScreenWindow}
public struct Resolution{public int width,height;}
public static class Screen{public static int width=1280,height=720;public static FullScreenMode fullScreenMode;public static Resolution[] resolutions=System.Array.Empty<Resolution>();public static void SetResolution(int w,int h,FullScreenMode m){}}
public static class QualitySettings{public static int antiAliasing;}
public static class Time{public static double realtimeSinceStartupAsDouble;}
public static class Mathf{public static int RoundToInt(float value)=>(int)System.Math.Round(value);public static float Clamp(float a,float lo,float hi)=>System.Math.Max(lo,System.Math.Min(hi,a));}
}
public static class Idas3FramePacing{public static void Configure(bool v,int n){}}
public static class Idas3ArcadeMeterCatalog{public static bool IsValidStyle(int n)=>n>=0;}
public static class Idas3OrnamentCatalog{public static bool IsValid(int n)=>n>=0;}
