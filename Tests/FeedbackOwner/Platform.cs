using System;
using System.IO;
using System.Text.Json;

// Portable value/serialization adapters. Driver calls use the existing fake
// backends in Idas3WheelFeedbackChecks, never the native DLL or a real wheel.
namespace UnityEngine
{
    public static class JsonUtility
    {
        public static string ToJson(object value, bool pretty = false) =>
            JsonSerializer.Serialize(value, value.GetType(), new JsonSerializerOptions { IncludeFields = true, WriteIndented = pretty });
    }
}
public static class Idas3Native
{
    internal struct WheelState
    {
        public uint size, version;
        public ulong simulationTicks;
        public float speed, steering, headingError, wallLateral, impact;
        public uint flags;
    }
}
public sealed class Idas3GameOptions
{
    public sealed class Values
    {
        public bool wheelForceFeedback, wheelFeedbackInvert;
        public float wheelFeedbackStrength = 1;
        public string wheelFeedbackDevice = "";
    }
}
internal static class Program
{
    private static void Main()
    {
        string root = Path.Combine(Path.GetTempPath(), "id3-feedback-owner-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Idas3WheelFeedbackChecks.Run(root);
            Console.WriteLine(File.ReadAllText(Path.Combine(root, "wheel-feedback-model-report.json")));
        }
        finally { Directory.Delete(root, true); }
    }
}
