using System.Collections.Generic;
using UnityEngine;

// List of room prefabs for each room type.
// You don't fill this by hand: it is refreshed automatically from the folders
//   Assets/Prefab/Rooms/Start, Enemy, Elite, Boss, Shop, Event, Rest
// whenever a prefab in them is added, removed or saved (see Editor/RoomLibraryUpdater).
[CreateAssetMenu(fileName = "RoomLibrary", menuName = "Roguelike/Room Library")]
public class RoomLibrary : ScriptableObject
{
    [System.Serializable]
    public class RoomList
    {
        public NodeType type;
        public List<RoomPrefab> rooms = new List<RoomPrefab>();
    }

    public List<RoomList> lists = new List<RoomList>();

    // A random prefab for this room type.
    // Empty folder -> uses an Enemy room instead. No Enemy rooms either -> null (DungeonManager builds a plain hallway).
    public RoomPrefab Pick(NodeType type)
    {
        var room = PickFrom(type);
        if (!room && type != NodeType.Enemy) room = PickFrom(NodeType.Enemy);
        return room;
    }

    RoomPrefab PickFrom(NodeType type)
    {
        foreach (var l in lists)
        {
            if (l.type != type) continue;
            var valid = l.rooms.FindAll(r => r != null);
            if (valid.Count > 0) return valid[Random.Range(0, valid.Count)];
        }
        return null;
    }
}
