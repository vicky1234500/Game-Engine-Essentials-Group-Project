using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Tools > Roguelike > Set Up Event Rooms
//   1. makes a placeholder Event room prefab (Assets/Prefab/Rooms/Event/EventRoom) with an EventRoomTrigger
//   2. adds the team's EventExplorationBridge to the Dungeon scene (12 event prefabs + backpack + battle hooked up),
//      unless it's already there
//   3. refreshes the Room Library
// Only the new bridge object is added to the Dungeon scene; nothing else in it is changed.
public static class EventRoomSetup
{
    const string DungeonPath = "Assets/Scenes/Dungeon.unity";
    const string EventPrefabsFolder = "Assets/Events/Prefabs";
    const string TemplatePath = RoomLibraryUpdater.RoomsFolder + "/Enemy/Hallway_Basic.prefab";
    const string EventRoomPath = RoomLibraryUpdater.RoomsFolder + "/Event/EventRoom.prefab";

    [MenuItem("Tools/Roguelike/Set Up Event Rooms")]
    public static void SetUp()
    {
        if (!EditorUtility.DisplayDialog("Set up event rooms?",
                "Makes a placeholder Event room prefab and adds the EventExplorationBridge " +
                "(the 12 event popups) to the Dungeon scene.\n\nCommit to git first. Continue?", "Set up", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        MakeEventRoom();
        FillCardLookup();
        var library = RoomLibraryUpdater.Refresh(true);

        var scene = EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Single);
        if (Find<EventExplorationBridge>(scene))
        {
            Debug.Log("Event rooms: the Dungeon already has an EventExplorationBridge, left it as it is.");
        }
        else
        {
            var prefabs = new List<GameObject>();
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { EventPrefabsFolder }))
                prefabs.Add(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
            prefabs.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            if (prefabs.Count != 12)
                Debug.LogWarning($"Event rooms: found {prefabs.Count} event prefabs in {EventPrefabsFolder}, the bridge expects exactly 12.");

            var go = new GameObject("EventExplorationBridge");
            SceneManager.MoveGameObjectToScene(go, scene);
            var bridge = go.AddComponent<EventExplorationBridge>();
            var so = new SerializedObject(bridge);
            var list = so.FindProperty("eventPrefabs");
            list.arraySize = prefabs.Count;
            for (int i = 0; i < prefabs.Count; i++) list.GetArrayElementAtIndex(i).objectReferenceValue = prefabs[i];
            so.FindProperty("inventory").objectReferenceValue = Find<InventoryGrid>(scene);
            so.FindProperty("inventoryToggle").objectReferenceValue = Find<InventoryToggle>(scene);
            so.FindProperty("battleManager").objectReferenceValue = Find<BattleManager>(scene);
            so.FindProperty("enableF8Test").boolValue = false;        // F8 = open a random event anywhere (testing only)
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        var dm = Find<DungeonManager>(scene);
        if (dm)
        {
            var dso = new SerializedObject(dm);
            if (!dso.FindProperty("roomLibrary").objectReferenceValue)
                dso.FindProperty("roomLibrary").objectReferenceValue = library;
            dso.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("Event rooms set up. Event rooms on the map now open a random event popup when you walk in.");
    }

    // Events add cards / items by name at runtime (Rejection, Tracker, Static, Cyberdeck, Lock Pick...),
    // so Resources/ItemDatabase gets a list of every card + the items that aren't in rewards.
    static void FillCardLookup()
    {
        var db = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/Resources/ItemDatabase.asset");
        if (!db)
        {
            Debug.LogWarning("Event rooms: Assets/Resources/ItemDatabase not found. Run Tools > Roguelike > Create or Update All Items first.");
            return;
        }
        db.cards = new List<CardData>();
        foreach (var guid in AssetDatabase.FindAssets("t:CardData", new[] { "Assets/Data/Cards" }))
            db.cards.Add(AssetDatabase.LoadAssetAtPath<CardData>(AssetDatabase.GUIDToAssetPath(guid)));
        db.otherItems = new List<ItemData>();
        foreach (var guid in AssetDatabase.FindAssets("t:ItemData", new[] { "Assets/Data/Items" }))
        {
            var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
            if (item && !db.items.Contains(item)) db.otherItems.Add(item);
        }
        EditorUtility.SetDirty(db);
        AssetDatabase.SaveAssets();
        Debug.Log($"Event rooms: {db.cards.Count} cards and {db.otherItems.Count} extra items added to the ItemDatabase lookup.");
    }

    // Placeholder room: the basic hallway + a glowing pillar labelled EVENT + the trigger.
    static void MakeEventRoom()
    {
        // Any event room already there is enough (never overwrite the team's version).
        if (AssetDatabase.FindAssets("t:Prefab", new[] { RoomLibraryUpdater.RoomsFolder + "/Event" }).Length > 0) return;
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath)) BattleTestBuilder.CreateExampleRoom();
        var template = AssetDatabase.LoadAssetAtPath<GameObject>(TemplatePath);

        var room = (GameObject)PrefabUtility.InstantiatePrefab(template);
        PrefabUtility.UnpackPrefabInstance(room, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        room.name = "EventRoom";

        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "EventPillar (placeholder)";
        pillar.transform.SetParent(room.transform, false);
        pillar.transform.localPosition = new Vector3(0, 1f, 7.7f);
        pillar.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        var mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/DoorOther.mat")
               ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/PortalCyan.mat");
        if (mat) pillar.GetComponent<Renderer>().sharedMaterial = mat;

        var label = new GameObject("Label");
        label.transform.SetParent(room.transform, false);
        label.transform.localPosition = new Vector3(0, 2.8f, 7.7f);
        var tm = label.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        tm.font = font;
        label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.text = "EVENT"; tm.fontSize = 64; tm.characterSize = 0.05f;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center; tm.fontStyle = FontStyle.Bold;
        tm.color = new Color(0.5f, 0.85f, 1f);

        var rp = room.GetComponent<RoomPrefab>();
        if (rp && rp.enemySpawn) rp.enemySpawn.gameObject.name = "EnemySpawn (unused in events)";
        room.AddComponent<EventRoomTrigger>();

        PrefabUtility.SaveAsPrefabAsset(room, EventRoomPath);
        Object.DestroyImmediate(room);
    }

    static T Find<T>(Scene scene) where T : Component
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            var c = root.GetComponentInChildren<T>(true);
            if (c) return c;
        }
        return null;
    }
}
