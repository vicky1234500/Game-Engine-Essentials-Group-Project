using System.Collections.Generic;
using UnityEngine;

// Makes a random floor map, Slay the Spire style.
//
//   - A grid of `layers` rows x `lanes` columns (5 x 3).
//   - Draw `paths` lines from the bottom to the top. Each step goes left, straight or right,
//     but never crosses a line that's already there. Where lines meet, paths merge -> they interconnect.
//   - Only grid spots a line passes through become rooms.
//   - Start connects to every room in the first row, every room in the last row connects to the Boss.
//   - Room types: first rows are always Enemy, later rows have a chance to be Elite.
public static class MapGenerator
{
    public static FloorMap Generate(int layers, int lanes, int paths, float eliteChance, int eliteFromLayer)
    {
        var map = new FloorMap { layers = layers, lanes = lanes };
        var grid = new MapNode[layers, lanes];
        var edges = new List<Vector3Int>();               // x = layer, y = from lane, z = to lane

        MapNode Get(int layer, int lane)
        {
            if (grid[layer, lane] == null)
            {
                var n = new MapNode { id = map.nodes.Count, layer = layer, lane = lane, type = NodeType.Enemy };
                map.nodes.Add(n);
                grid[layer, lane] = n;
            }
            return grid[layer, lane];
        }

        for (int p = 0; p < paths; p++)
        {
            int lane = p < lanes ? p : Random.Range(0, lanes);   // first paths start in every column
            Get(0, lane);

            for (int layer = 0; layer < layers - 1; layer++)
            {
                var options = new List<int> { lane - 1, lane, lane + 1 };
                options.RemoveAll(x => x < 0 || x >= lanes);
                Shuffle(options);

                int chosen = lane;                                  // fallback: straight up
                foreach (var o in options)
                    if (!Crosses(edges, layer, lane, o)) { chosen = o; break; }

                var from = Get(layer, lane);
                var to = Get(layer + 1, chosen);
                if (!from.next.Contains(to.id))
                {
                    from.next.Add(to.id);
                    edges.Add(new Vector3Int(layer, lane, chosen));
                }
                lane = chosen;
            }
        }

        map.start = new MapNode { id = map.nodes.Count, layer = -1, lane = lanes / 2, type = NodeType.Start, visited = true };
        map.nodes.Add(map.start);
        map.boss = new MapNode { id = map.nodes.Count, layer = layers, lane = lanes / 2, type = NodeType.Boss };
        map.nodes.Add(map.boss);

        for (int c = 0; c < lanes; c++)
        {
            if (grid[0, c] != null) map.start.next.Add(grid[0, c].id);
            if (grid[layers - 1, c] != null) grid[layers - 1, c].next.Add(map.boss.id);
        }

        // Room types (only fights for now)
        foreach (var n in map.nodes)
            if (n.type == NodeType.Enemy && n.layer >= eliteFromLayer && Random.value < eliteChance)
                n.type = NodeType.Elite;

        return map;
    }

    // Would a line from (layer, a) to (layer+1, b) cross one that's already drawn?
    static bool Crosses(List<Vector3Int> edges, int layer, int a, int b)
    {
        foreach (var e in edges)
        {
            if (e.x != layer) continue;
            int c = e.y, d = e.z;
            if ((a < c && b > d) || (a > c && b < d)) return true;
        }
        return false;
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }
}
