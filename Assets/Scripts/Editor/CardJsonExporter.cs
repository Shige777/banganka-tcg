using UnityEngine;
using UnityEditor;
using System.IO;
using Banganka.Core.Data;

public static class CardJsonExporter
{
    [MenuItem("Banganka/Export Cards to StreamingAssets JSON")]
    public static void ExportAll()
    {
        string dir = Path.Combine(Application.streamingAssetsPath, "Cards");
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        int count = 0;
        foreach (var kv in CardDatabase.AllCards)
        {
            string json = JsonUtility.ToJson(kv.Value, true);
            string path = Path.Combine(dir, kv.Key + ".json");
            File.WriteAllText(path, json);
            count++;
        }

        // Leaders
        string leaderDir = Path.Combine(Application.streamingAssetsPath, "Leaders");
        if (!Directory.Exists(leaderDir)) Directory.CreateDirectory(leaderDir);

        foreach (var kv in CardDatabase.AllLeaders)
        {
            string json = JsonUtility.ToJson(kv.Value, true);
            string path = Path.Combine(leaderDir, kv.Key + ".json");
            File.WriteAllText(path, json);
        }

        AssetDatabase.Refresh();
        Debug.Log($"[CardJsonExporter] Exported {count} cards + {CardDatabase.AllLeaders.Count} leaders to StreamingAssets/");
    }
}
