using System.Collections.Generic;

// What's behind a door. Only Enemy / Elite / Boss rooms are built for now;
// Shop / Event / Rest are here so the map can use them later.
public enum NodeType { Start, Enemy, Elite, Boss, Shop, Event, Rest }

// One room on the floor map.
public class MapNode
{
    public int id;
    public int layer;        // -1 = start, 0..layers-1 = normal rooms, layers = boss
    public int lane;         // column 0..lanes-1 (left to right)
    public NodeType type;
    public readonly List<int> next = new List<int>();   // ids of the rooms its doors lead to
    public bool visited;

    public bool IsCombat => type == NodeType.Enemy || type == NodeType.Elite || type == NodeType.Boss;
}

// The whole floor: start -> 5 layers of branching rooms -> boss.
public class FloorMap
{
    public readonly List<MapNode> nodes = new List<MapNode>();
    public MapNode start;
    public MapNode boss;
    public int layers;
    public int lanes;

    // Rooms the current room's doors lead to, sorted left to right.
    public List<MapNode> Next(MapNode node)
    {
        var list = new List<MapNode>();
        foreach (var id in node.next) list.Add(nodes[id]);
        list.Sort((a, b) => a.lane.CompareTo(b.lane));
        return list;
    }
}
