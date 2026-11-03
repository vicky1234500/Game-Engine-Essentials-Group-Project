using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// The floor map screen (press M). Built from the FloorMap at runtime:
// start at the bottom, boss at the top, lines between connected rooms.
// Yellow ring = where you are, green ring = rooms you can go to next, faded = already visited.
public class MapView : MonoBehaviour
{
    [SerializeField] GameObject panel;              // the whole map window (starts hidden)
    [SerializeField] RectTransform content;         // area the nodes are drawn in
    [SerializeField] Vector2 mapSize = new Vector2(600, 620);
    [SerializeField] float nodeSize = 54;

    [Header("Colours")]
    [SerializeField] Color startColor = new Color(0.6f, 0.6f, 0.65f);
    [SerializeField] Color enemyColor = new Color(0.75f, 0.25f, 0.3f);
    [SerializeField] Color eliteColor = new Color(1f, 0.55f, 0.15f);
    [SerializeField] Color bossColor = new Color(0.65f, 0.3f, 0.9f);
    [SerializeField] Color otherColor = new Color(0.3f, 0.6f, 0.9f);
    [SerializeField] Color lineColor = new Color(1f, 1f, 1f, 0.35f);
    [SerializeField] Color currentRing = new Color(1f, 0.9f, 0.2f);
    [SerializeField] Color nextRing = new Color(0.3f, 1f, 0.4f);

    class NodeView { public Image body; public Image ring; public Color baseColor; }

    readonly Dictionary<int, NodeView> views = new Dictionary<int, NodeView>();
    FloorMap map;
    Font font;

    public bool IsOpen => panel && panel.activeSelf;

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (panel) panel.SetActive(false);
    }

    public void Toggle(MapNode current)
    {
        if (!panel) return;
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf) Refresh(current);
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }

    public void Build(FloorMap floor)
    {
        map = floor;
        if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        foreach (Transform child in content) Destroy(child.gameObject);
        views.Clear();

        // Lines first so they are drawn behind the nodes
        foreach (var n in map.nodes)
            foreach (var id in n.next)
                Line(Position(n), Position(map.nodes[id]));

        foreach (var n in map.nodes)
            views[n.id] = Node(n);
    }

    Vector2 Position(MapNode n)
    {
        int rows = map.layers + 2;                                   // start + layers + boss
        float rowStep = mapSize.y / (rows - 1);
        float y = (n.layer + 1) * rowStep - mapSize.y / 2f;
        float x = 0f;
        if (n.type != NodeType.Start && n.type != NodeType.Boss)
        {
            float laneStep = mapSize.x / Mathf.Max(1, map.lanes);
            x = (n.lane - (map.lanes - 1) / 2f) * laneStep;
        }
        return new Vector2(x, y);
    }

    NodeView Node(MapNode n)
    {
        var v = new NodeView { baseColor = ColorFor(n.type) };
        Vector2 pos = Position(n);
        float size = n.type == NodeType.Boss ? nodeSize * 1.4f : nodeSize;

        v.ring = MakeImage("Ring_" + n.id, pos, Vector2.one * (size + 14), Color.clear);
        v.body = MakeImage("Node_" + n.id, pos, Vector2.one * size, v.baseColor);

        var label = new GameObject("Label", typeof(RectTransform)).AddComponent<Text>();
        label.transform.SetParent(v.body.transform, false);
        var rt = label.rectTransform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
        label.font = font; label.fontSize = n.type == NodeType.Boss ? 22 : 30; label.fontStyle = FontStyle.Bold;
        label.alignment = TextAnchor.MiddleCenter; label.color = Color.white; label.raycastTarget = false;
        label.text = Symbol(n.type);
        return v;
    }

    // Called when you enter a room.
    public void Refresh(MapNode current)
    {
        if (map == null || current == null) return;
        foreach (var n in map.nodes)
        {
            if (!views.TryGetValue(n.id, out var v)) continue;
            Color c = v.baseColor;
            if (n.visited && n != current) c.a = 0.35f;
            v.body.color = c;

            if (n == current) v.ring.color = currentRing;
            else if (current.next.Contains(n.id)) v.ring.color = nextRing;
            else v.ring.color = Color.clear;
        }
    }

    void Line(Vector2 a, Vector2 b)
    {
        Vector2 d = b - a;
        var img = MakeImage("Line", a, new Vector2(d.magnitude, 4), lineColor);
        var rt = img.rectTransform;
        rt.pivot = new Vector2(0, 0.5f);
        rt.anchoredPosition = a;
        rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg);
    }

    Image MakeImage(string name, Vector2 pos, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(content, false);
        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return img;
    }

    Color ColorFor(NodeType t)
    {
        switch (t)
        {
            case NodeType.Start: return startColor;
            case NodeType.Enemy: return enemyColor;
            case NodeType.Elite: return eliteColor;
            case NodeType.Boss:  return bossColor;
            default:             return otherColor;
        }
    }

    static string Symbol(NodeType t)
    {
        switch (t)
        {
            case NodeType.Start: return "S";
            case NodeType.Enemy: return "?";
            case NodeType.Elite: return "!";
            case NodeType.Boss:  return "BOSS";
            case NodeType.Shop:  return "$";
            case NodeType.Event: return "E";
            case NodeType.Rest:  return "R";
            default:             return "";
        }
    }
}
