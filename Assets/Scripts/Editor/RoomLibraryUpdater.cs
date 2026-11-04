using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Keeps Assets/Data/RoomLibrary.asset in sync with the room folders:
//   Assets/Prefab/Rooms/<Start | Enemy | Elite | Boss | Shop | Event | Rest>/*.prefab
// Runs by itself whenever something in Assets/Prefab/Rooms changes,
// or by hand with Tools > Roguelike > Refresh Room Library.
public class RoomLibraryUpdater : AssetPostprocessor
{
    public const string RoomsFolder = "Assets/Prefab/Rooms";
    public const string LibraryPath = "Assets/Data/RoomLibrary.asset";

    static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom))
            EditorApplication.delayCall += () => Refresh(false);   // wait until the import has finished
    }

    static bool Touches(string[] paths)
    {
        foreach (var p in paths)
            if (p.StartsWith(RoomsFolder)) return true;
        return false;
    }

    [MenuItem("Tools/Roguelike/Refresh Room Library")]
    public static void RefreshMenu() => Refresh(true);

    // Makes the folders + library if missing, then fills it from the folders.
    public static RoomLibrary Refresh(bool log)
    {
        EnsureFolders();

        var lib = AssetDatabase.LoadAssetAtPath<RoomLibrary>(LibraryPath);
        if (!lib)
        {
            lib = ScriptableObject.CreateInstance<RoomLibrary>();
            AssetDatabase.CreateAsset(lib, LibraryPath);
        }

        lib.lists = new List<RoomLibrary.RoomList>();
        var summary = new System.Text.StringBuilder("Room Library refreshed:");
        foreach (NodeType type in System.Enum.GetValues(typeof(NodeType)))
        {
            var list = new RoomLibrary.RoomList { type = type };
            string folder = RoomsFolder + "/" + type;
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { folder }))
            {
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                var room = go ? go.GetComponent<RoomPrefab>() : null;
                if (room) list.rooms.Add(room);
                else if (go) Debug.LogWarning($"{go.name} in {folder} has no RoomPrefab component on its root, skipped.", go);
            }
            lib.lists.Add(list);
            summary.Append($"  {type}: {list.rooms.Count}");
        }

        EditorUtility.SetDirty(lib);
        AssetDatabase.SaveAssets();
        if (log) Debug.Log(summary.ToString());
        return lib;
    }

    public static void EnsureFolders()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Prefab")) AssetDatabase.CreateFolder("Assets", "Prefab");
        if (!AssetDatabase.IsValidFolder(RoomsFolder)) AssetDatabase.CreateFolder("Assets/Prefab", "Rooms");
        foreach (NodeType type in System.Enum.GetValues(typeof(NodeType)))
            if (!AssetDatabase.IsValidFolder(RoomsFolder + "/" + type))
                AssetDatabase.CreateFolder(RoomsFolder, type.ToString());
        if (!AssetDatabase.IsValidFolder("Assets/Data")) AssetDatabase.CreateFolder("Assets", "Data");
    }
}
