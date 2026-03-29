using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class FirebaseImporter
{
    static readonly string SdkPath = System.IO.Path.Combine(
        System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile),
        "Downloads", "firebase_unity_sdk");
    static readonly string TriggerFile = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "banganka_firebase_import_trigger");

    static readonly string[] Packages = new[]
    {
        "FirebaseAnalytics.unitypackage",
        "FirebaseAuth.unitypackage",
        "FirebaseFirestore.unitypackage",
        "FirebaseDatabase.unitypackage",
        "FirebaseFunctions.unitypackage",
        "FirebaseMessaging.unitypackage",
        "FirebaseCrashlytics.unitypackage",
    };

    static FirebaseImporter()
    {
        if (System.IO.File.Exists(TriggerFile))
        {
            System.IO.File.Delete(TriggerFile);
            EditorApplication.delayCall += ImportAll;
        }
    }

    [MenuItem("Tools/Firebase/Import All Firebase Packages")]
    public static void ImportAll()
    {
        Debug.Log("[FirebaseImporter] Starting import of all Firebase packages...");
        foreach (var pkg in Packages)
        {
            string path = System.IO.Path.Combine(SdkPath, pkg);
            if (!System.IO.File.Exists(path))
            {
                Debug.LogError($"[FirebaseImporter] Not found: {path}");
                continue;
            }
            Debug.Log($"[FirebaseImporter] Importing {pkg}...");
            AssetDatabase.ImportPackage(path, false);
        }
        Debug.Log("[FirebaseImporter] All Firebase packages import completed.");
    }
}
