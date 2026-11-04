using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// One-time tool: Tools > Roguelike > Move Shop From Lobby To Dungeon
//   1. copies the team's ShopUI (with ShopManager) + "Press E" prompt from the Lobby Canvas into the Dungeon Canvas,
//      and hooks it to the Dungeon's player, mouse look and backpack
//   2. makes a Shop room prefab (Assets/Prefab/Rooms/Shop/ShopRoom) with a copy of the Lobby shop counter
//   3. links the Room Library to the DungeonManager
//   4. removes the shop from the Lobby
// Both scenes are saved. Commit to git first so it can be undone.
public static class ShopMover
{
    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    const string DungeonPath = "Assets/Scenes/Dungeon.unity";
    const string ShopRoomPath = RoomLibraryUpdater.RoomsFolder + "/Shop/ShopRoom.prefab";
    const string TemplatePath = RoomLibraryUpdater.RoomsFolder + "/Enemy/Hallway_Basic.prefab";

    [MenuItem("Tools/Roguelike/Move Shop From Lobby To Dungeon")]
    public static void Move()
    {
        if (!EditorUtility.DisplayDialog("Move shop to the Dungeon?",
                "This moves the ShopUI from the Lobby scene into the Dungeon scene, makes a Shop room prefab, " +
                "and removes the shop from the Lobby. Both scenes are saved.\n\nCommit to git first. Continue?",
                "Move it", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        var lobby = EditorSceneManager.OpenScene(LobbyPath, OpenSceneMode.Single);
        var shopUI = Find<ShopUI>(lobby);
        var station = Find<ShopStation>(lobby);
        if (!shopUI) { EditorUtility.DisplayDialog("Shop mover", "No ShopUI found in the Lobby (already moved?).", "OK"); return; }

        var prompt = GetRef<Text>(station, "promptText");
        var dungeon = EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Additive);

        // ---- 1. Copy the UI into the Dungeon Canvas
        var canvas = FindCanvas(dungeon);
        if (!canvas) { EditorUtility.DisplayDialog("Shop mover", "No Canvas found in the Dungeon scene.", "OK"); return; }

        var uiCopy = Object.Instantiate(shopUI.gameObject, canvas, false);
        uiCopy.name = shopUI.gameObject.name;
        Text promptCopy = null;
        if (prompt)
        {
            promptCopy = Object.Instantiate(prompt.gameObject, canvas, false).GetComponent<Text>();
            promptCopy.name = prompt.gameObject.name;
        }
        var fade = canvas.Find("FadeScreen");
        if (fade) fade.SetAsLastSibling();                           // fade stays on top

        // Hook it to the Dungeon's objects
        var newUI = uiCopy.GetComponent<ShopUI>();
        var toggle = Find<InventoryToggle>(dungeon);
        var grid = Find<InventoryGrid>(dungeon);
        var disable = new List<Object>();
        disable.AddRange(FindAll<PlayerMovement>(dungeon));
        disable.AddRange(FindAll<MouseLook>(dungeon));
        if (toggle) disable.Add(toggle);

        var so = new SerializedObject(newUI);
        so.FindProperty("inventoryToggle").objectReferenceValue = toggle;
        so.FindProperty("promptText").objectReferenceValue = promptCopy;
        var list = so.FindProperty("disableWhileOpen");
        list.arraySize = disable.Count;
        for (int i = 0; i < disable.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = disable[i];
        so.ApplyModifiedPropertiesWithoutUndo();

        var manager = uiCopy.GetComponentInChildren<ShopManager>(true);     // (includes the root)
        if (manager)
        {
            var mso = new SerializedObject(manager);
            mso.FindProperty("inventory").objectReferenceValue = grid;
            mso.ApplyModifiedPropertiesWithoutUndo();
        }
        WarnAboutLobbyReferences(uiCopy, lobby);

        // ---- 2. Shop room prefab with a copy of the Lobby counter
        if (station && !AssetDatabase.LoadAssetAtPath<GameObject>(ShopRoomPath))
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath)) BattleTestBuilder.CreateExampleRoom();
            var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);
            var room = (GameObject)PrefabUtility.InstantiatePrefab(template, dungeon);
            PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            room.name = "ShopRoom";

            var counter = Object.Instantiate(station.gameObject, room.transform, false);
            counter.name = "ShopCounter";
            counter.transform.localPosition = new Vector3(1.2f, 0f, 7f);   // right side of the hallway
            var cso = new SerializedObject(counter.GetComponent<ShopStation>());
            cso.FindProperty("shopUI").objectReferenceValue = null;          // found at runtime
            cso.FindProperty("promptText").objectReferenceValue = null;
            cso.ApplyModifiedPropertiesWithoutUndo();

            var enemySpawn = room.GetComponent<RoomPrefab>().enemySpawn;    // no fight in a shop
            if (enemySpawn) enemySpawn.gameObject.name = "EnemySpawn (unused in shops)";

            PrefabUtility.SaveAsPrefabAsset(room, ShopRoomPath);
            Object.DestroyImmediate(room);
        }

        // ---- 3. Room library on the DungeonManager
        var library = RoomLibraryUpdater.Refresh(true);
        var dm = Find<DungeonManager>(dungeon);
        if (dm)
        {
            var dso = new SerializedObject(dm);
            dso.FindProperty("roomLibrary").objectReferenceValue = library;
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ---- 4. Remove the shop from the Lobby
        Object.DestroyImmediate(shopUI.gameObject);
        if (station) Object.DestroyImmediate(station.gameObject);
        if (prompt) Object.DestroyImmediate(prompt.gameObject);

        EditorSceneManager.MarkSceneDirty(lobby);
        EditorSceneManager.MarkSceneDirty(dungeon);
        EditorSceneManager.SaveScene(lobby);
        EditorSceneManager.SaveScene(dungeon);
        SceneManager.SetActiveScene(dungeon);
        EditorSceneManager.CloseScene(lobby, true);

        Debug.Log("Shop moved to the Dungeon. Shop rooms: Assets/Prefab/Rooms/Shop. Press Play in the Lobby to test.");
    }

    // ---------------- helpers ----------------

    static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var c = root.GetComponentInChildren<T>(true);
            if (c) return c;
        }
        return null;
    }

    static List<T> FindAll<T>(Scene scene) where T : Component
    {
        var list = new List<T>();
        foreach (var root in scene.GetRootGameObjects())
            list.AddRange(root.GetComponentsInChildren<T>(true));
        return list;
    }

    static Transform FindCanvas(Scene scene)
    {
        foreach (var root in scene.GetRootGameObjects())
            if (root.name == "Canvas" && root.GetComponent<Canvas>()) return root.transform;
        foreach (var root in scene.GetRootGameObjects())
            if (root.GetComponentInChildren<BattleUI>(true)) return root.transform;
        return null;
    }

    static T GetRef<T>(Object target, string field) where T : Object
    {
        if (!target) return null;
        var p = new SerializedObject(target).FindProperty(field);
        return p != null ? p.objectReferenceValue as T : null;
    }

    // Anything in the copied UI still pointing at a Lobby object would break once the Lobby closes.
    static void WarnAboutLobbyReferences(GameObject copy, Scene lobby)
    {
        foreach (var c in copy.GetComponentsInChildren<Component>(true))
        {
            if (!c) continue;
            var it = new SerializedObject(c).GetIterator();
            while (it.NextVisible(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var o = it.objectReferenceValue;
                GameObject go = o is Component comp ? comp.gameObject : o as GameObject;
                if (go && go.scene == lobby && !go.transform.IsChildOf(copy.transform))
                    Debug.LogWarning($"Shop mover: {c.GetType().Name}.{it.name} on {c.name} still points at '{go.name}' in the Lobby. Re-link it in the Dungeon.", c);
            }
        }
    }
}
