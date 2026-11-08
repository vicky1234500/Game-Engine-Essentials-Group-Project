using UnityEditor;
using UnityEngine;

// Tools > Roguelike > Build Modular Rooms (safe)
// Builds the Enemy and Event rooms out of the 3D Free Modular Kit pieces and saves them as NEW prefabs.
// Scenes are not changed. The old placeholder rooms move to Assets/Prefab/Rooms/_Old, where the Room Library
// doesn't look. The layout is the same idea as before: a hallway, then a wider room with 3 door slots.
//
//        z = 18   [door][door][door]         (x = -3, 0, 3)
//                 |                |
//        z = 12   |____        ____|
//                      |      |
//                      |      |              hallway: 3 m wide, 12 m long
//        z = 0         |______|
public static class ModularRoomBuilder
{
    const string KitPrefabs = "Assets/models/room/3D Free Modular Kit/Prefabs/";
    const string NeonMaterial = "Assets/models/room/3D Free Modular Kit/Meshes/Materials/Neon_01.mat";

    const string EnemyRoomPath = RoomLibraryUpdater.RoomsFolder + "/Enemy/Modular_Enemy.prefab";
    const string EventRoomPath = RoomLibraryUpdater.RoomsFolder + "/Event/Modular_Event.prefab";
    const string OldRoomsFolder = RoomLibraryUpdater.RoomsFolder + "/_Old";
    static readonly string[] OldRooms =
    {
        RoomLibraryUpdater.RoomsFolder + "/Enemy/Hallway_Basic.prefab",
        RoomLibraryUpdater.RoomsFolder + "/Event/EventRoom.prefab",
    };

    const float Tile = 3f;        // kit floors and walls are 3 x 3 m
    const float Ceiling = 3f;
    const int HallTiles = 4;
    const float HallEndZ = HallTiles * Tile;       // 12
    const float RoomEndZ = HallEndZ + 2 * Tile;    // 18

    // A kit wall only shows on one side: its +X side when it isn't rotated.
    // If the walls ever look see-through from inside a room, change this to 180.
    const float WallYawOffset = 0f;

    [MenuItem("Tools/Roguelike/Build Modular Rooms (safe)")]
    public static void Build()
    {
        if (EditorApplication.isPlaying)
        {
            EditorUtility.DisplayDialog("Modular rooms", "Stop Play mode first.", "OK");
            return;
        }
        if (!KitIsReady()) return;

        bool exists = AssetDatabase.LoadAssetAtPath<GameObject>(EnemyRoomPath) ||
                      AssetDatabase.LoadAssetAtPath<GameObject>(EventRoomPath);
        if (exists && !EditorUtility.DisplayDialog("Rebuild modular rooms?",
                "Modular_Enemy / Modular_Event already exist. Rebuilding replaces them, so any edits made to them by hand are lost.",
                "Rebuild", "Cancel")) return;

        RoomLibraryUpdater.EnsureFolders();
        if (!BuildRoom("Modular_Enemy", EnemyRoomPath, false)) return;
        if (!BuildRoom("Modular_Event", EventRoomPath, true)) return;

        RetireOldRooms();
        AssetDatabase.SaveAssets();
        RoomLibraryUpdater.Refresh(true);
        Debug.Log("Modular rooms built. Check them in Assets/Prefab/Rooms/Enemy and Event, then press Play in the Lobby.");
    }

    // The prefabs are useless without their meshes (they are big files stored with Git LFS).
    static bool KitIsReady()
    {
        foreach (var piece in new[] { "Floor_01", "Wall_Simple_01", "Roof_01", "Light_01" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPrefabs + piece + ".prefab");
            var filter = prefab ? prefab.GetComponentInChildren<MeshFilter>() : null;
            if (filter && filter.sharedMesh) continue;

            EditorUtility.DisplayDialog("Modular rooms", $"{piece} or its mesh is missing from the 3D Free Modular Kit folder.", "OK");
            return false;
        }
        return true;
    }

    // ---------------- The room ----------------

    static bool BuildRoom(string roomName, string path, bool isEvent)
    {
        var root = new GameObject(roomName);
        var geometry = new GameObject("Geometry").transform;
        geometry.SetParent(root.transform, false);

        // Hallway
        for (int i = 0; i < HallTiles; i++)
        {
            float z = Tile * (i + 0.5f);
            FloorAndRoof(geometry, 0f, z);
            Wall(geometry, -Tile / 2f, z, Vector3.right);
            Wall(geometry, Tile / 2f, z, Vector3.left);
        }
        Wall(geometry, 0f, 0f, Vector3.forward);       // behind the player

        // Wider room at the end
        for (int i = 0; i < 2; i++)
        {
            float z = HallEndZ + Tile * (i + 0.5f);
            for (int column = -1; column <= 1; column++)
                FloorAndRoof(geometry, column * Tile, z);
            Wall(geometry, -1.5f * Tile, z, Vector3.right);
            Wall(geometry, 1.5f * Tile, z, Vector3.left);
        }
        for (int column = -1; column <= 1; column++)
            Wall(geometry, column * Tile, RoomEndZ, Vector3.back);    // the door wall
        Wall(geometry, -Tile, HallEndZ, Vector3.forward);             // either side of the hallway opening
        Wall(geometry, Tile, HallEndZ, Vector3.forward);

        // Lights
        CeilingLight(geometry, 0f, 4.5f);
        CeilingLight(geometry, 0f, 10.5f);
        CeilingLight(geometry, -Tile, 15f);
        CeilingLight(geometry, Tile, 15f);

        // Markers, read by DungeonManager (same spots as the old rooms)
        var markers = new GameObject("Markers").transform;
        markers.SetParent(root.transform, false);
        var room = root.AddComponent<RoomPrefab>();
        room.playerSpawn = Marker("PlayerSpawn", markers, new Vector3(0f, 1f, 1.5f));
        room.enemySpawn = Marker(isEvent ? "EnemySpawn (unused in events)" : "EnemySpawn", markers, new Vector3(0f, 0f, 7.7f));
        room.enemySpawn.localRotation = Quaternion.Euler(0f, 180f, 0f);    // faces the player
        room.doorSlots = new Transform[3];
        for (int i = 0; i < 3; i++)
            room.doorSlots[i] = Marker("DoorSlot" + (i + 1), markers, new Vector3((i - 1) * Tile, 0f, RoomEndZ));

        if (isEvent) AddEventPillar(root);

        bool saved = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        if (!saved) Debug.LogError("Modular rooms: couldn't save " + path);
        return saved;
    }

    // EventRoomTrigger finds its target by the name "EventPillar".
    static void AddEventPillar(GameObject root)
    {
        var pillar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pillar.name = "EventPillar";
        pillar.transform.SetParent(root.transform, false);
        pillar.transform.localPosition = new Vector3(0f, 1f, 7.7f);
        pillar.transform.localScale = new Vector3(0.8f, 1f, 0.8f);
        var neon = AssetDatabase.LoadAssetAtPath<Material>(NeonMaterial);
        if (neon) pillar.GetComponent<Renderer>().sharedMaterial = neon;

        var label = new GameObject("Label");
        label.transform.SetParent(root.transform, false);
        label.transform.localPosition = new Vector3(0f, 2.5f, 7.7f);
        var text = label.AddComponent<TextMesh>();
        var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.font = font;
        label.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        text.text = "EVENT";
        text.fontSize = 64;
        text.characterSize = 0.05f;
        text.anchor = TextAnchor.MiddleCenter;
        text.alignment = TextAlignment.Center;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.5f, 0.85f, 1f);

        root.AddComponent<EventRoomTrigger>();
    }

    // ---------------- Placing kit pieces ----------------
    // Kit pieces have their pivot on a corner, so each piece is measured and then moved into place.

    static void FloorAndRoof(Transform parent, float x, float z)
    {
        var floor = Piece("Floor_01", parent, 0f);
        Align(floor, new Vector3(x, 0f, z), 0, -1, 0);

        var roof = Piece("Roof_01", parent, 0f);
        Align(roof, new Vector3(x, Ceiling, z), 0, -1, 0);
    }

    static void CeilingLight(Transform parent, float x, float z)
    {
        var light = Piece("Light_01", parent, 0f);
        Align(light, new Vector3(x, Ceiling, z), 0, 1, 0);
    }

    // (x, z) = a point on the surface you see, in the middle of the wall.
    // "faces" = the direction the visible side looks, so into the room.
    static void Wall(Transform parent, float x, float z, Vector3 faces)
    {
        float yaw = -Mathf.Atan2(faces.z, faces.x) * Mathf.Rad2Deg + WallYawOffset;
        var wall = Piece("Wall_Simple_01", parent, yaw);
        Align(wall, new Vector3(x, 0f, z), Mathf.RoundToInt(faces.x), -1, Mathf.RoundToInt(faces.z));
    }

    static Transform Piece(string pieceName, Transform parent, float yaw)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(KitPrefabs + pieceName + ".prefab");
        var piece = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        piece.transform.localPosition = Vector3.zero;
        piece.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return piece.transform;
    }

    // Moves a piece so one point of its box lands on target.
    // For each axis: -1 = the low side of the box, 0 = its middle, 1 = its high side.
    static void Align(Transform piece, Vector3 target, int sideX, int sideY, int sideZ)
    {
        var renderers = piece.GetComponentsInChildren<Renderer>();
        var box = renderers[0].bounds;
        foreach (var r in renderers) box.Encapsulate(r.bounds);

        var point = box.center + Vector3.Scale(box.extents, new Vector3(sideX, sideY, sideZ));
        piece.position += target - point;
    }

    static Transform Marker(string markerName, Transform parent, Vector3 position)
    {
        var marker = new GameObject(markerName).transform;
        marker.SetParent(parent, false);
        marker.localPosition = position;
        return marker;
    }

    // ---------------- Old rooms ----------------

    static void RetireOldRooms()
    {
        foreach (var path in OldRooms)
        {
            if (!AssetDatabase.LoadAssetAtPath<GameObject>(path)) continue;
            if (!AssetDatabase.IsValidFolder(OldRoomsFolder))
                AssetDatabase.CreateFolder(RoomLibraryUpdater.RoomsFolder, "_Old");

            string error = AssetDatabase.MoveAsset(path, OldRoomsFolder + "/" + System.IO.Path.GetFileName(path));
            if (!string.IsNullOrEmpty(error)) Debug.LogError("Modular rooms: couldn't move " + path + ": " + error);
        }
    }
}
