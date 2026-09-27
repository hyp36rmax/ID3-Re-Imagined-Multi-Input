using UnityEngine;

// This build identity separates saves and keeps development runs out of the
// release updater/community service. Existing products retain their behavior.
public static class Idas3SampleBuild {
    public const string ProductName="Initial D Multi Input Sample";
    internal static bool Active=>Application.productName==ProductName;
}
