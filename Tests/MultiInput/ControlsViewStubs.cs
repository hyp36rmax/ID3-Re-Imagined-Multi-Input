// Headless adapter to compile and exercise the real module's commands. These
// drawing calls are not a renderer and cannot establish screenshot acceptance.
using System.Collections.Generic;

namespace UnityEngine
{
    public enum TextClipping
    {
        Clip
    }

    public enum FontStyle
    {
        Bold
    }

    public enum TextAnchor
    {
        MiddleCenter
    }

    public struct Rect
    {
        public float x, y, width, height;
        public Rect(float x, float y, float w, float h)
        {
            this.x = x;
            this.y = y;
            width = w;
            height = h;
        }
    }

    public struct Color
    {
        public Color(float r, float g, float b)
        {
        }

        public static Color white => default;
    }

    public struct Color32
    {
        public Color32(byte r, byte g, byte b, byte a)
        {
        }

        public static implicit operator Color(Color32 c) => default;
    }

    public class Texture2D
    {
        public static Texture2D whiteTexture = new Texture2D();
    }

    public class GUIContent
    {
        public string text;
        public GUIContent(string text)
        {
            this.text = text;
        }

        public static GUIContent none = new GUIContent("");
    }

    public class GUIStyleState
    {
        public Color textColor;
    }

    public class GUIStyle
    {
        public int fontSize;
        public bool wordWrap;
        public TextClipping clipping;
        public FontStyle fontStyle;
        public TextAnchor alignment;
        public GUIStyleState normal = new GUIStyleState();
        public GUIStyle()
        {
        }

        public GUIStyle(GUIStyle other)
        {
        }

        public float CalcHeight(GUIContent content, float width) => 30;
    }

    public class GUISkin
    {
        public GUIStyle label = new GUIStyle(), button = new GUIStyle();
    }

    public static class GUI
    {
        public static GUISkin skin = new GUISkin();
        public static Color color, backgroundColor;
        public static bool enabled = true;
        public static string Click;
        public static readonly List<string> Labels = new List<string>();
        public static void Label(Rect rect, string value, GUIStyle style)
        {
            Labels.Add(value);
        }

        public static bool Button(Rect rect, string value, GUIStyle style)
        {
            if (enabled && Click == value)
            {
                Click = null;
                return true;
            }

            return false;
        }

        public static void Box(Rect rect, GUIContent content)
        {
        }

        public static void DrawTexture(Rect rect, Texture2D texture)
        {
        }

        public static Vector2 BeginScrollView(Rect outer, Vector2 scroll, Rect inner) => scroll;
        public static void EndScrollView()
        {
        }
    }
}

public sealed partial class Idas3WheelFeedback
{
    internal sealed class DeviceChoice
    {
        internal string id, name;
    }

    internal IReadOnlyList<DeviceChoice> Choices => System.Array.Empty<DeviceChoice>();
    internal string StatusText => "No output";

    internal void RefreshDevices()
    {
    }
}
