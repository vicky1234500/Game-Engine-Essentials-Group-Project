using UnityEditor;
using UnityEngine;

// Tools > Roguelike > Rest Room: Wall Over Window
// The bar's back wall has a louvred window with a bright backdrop behind it, right where the exit doors go.
// This hides the louvres and covers the opening with a plain wall panel in the bar's own wall material.
// Safe to run twice (it replaces its own panel). Close the prefab in Prefab Mode first.
public static class RestRoomWindowFix
{
    const string PrefabPath = "Assets/Prefab/Rooms/Rest/sci-fi_bar.prefab";
    const string PanelName = "WindowWall";

    [MenuItem("Tools/Roguelike/Rest Room: Wall Over Window")]
    static void Run()
    {
        var root = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var old = root.transform.Find(PanelName);
            if (old) Object.DestroyImmediate(old.gameObject);

            // Opening = everything the louvre parts cover (works on the parts even if they are already hidden)
            Bounds b = default; bool any = false;
            Material wallMat = null;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.Contains("Louver"))
                {
                    bool wasActive = r.gameObject.activeSelf;
                    r.gameObject.SetActive(true);
                    if (!any) { b = r.bounds; any = true; } else b.Encapsulate(r.bounds);
                    r.gameObject.SetActive(wasActive);
                }
                if (!wallMat && r.name.Contains("Murallas_lambert5")) wallMat = r.sharedMaterial;
            }
            if (!any) { Debug.LogWarning("Rest room: no louvre parts found."); return; }

            // Hide the louvre group (or the single parts if there is no group)
            var group = FindDeep(root.transform, "Louver");
            if (group) group.gameObject.SetActive(false);
            else foreach (var r in root.GetComponentsInChildren<Renderer>(true)) if (r.name.Contains("Louver")) r.gameObject.SetActive(false);

            // Panel: a bit bigger than the opening, flush with the back wall, behind the exit doors
            float s = root.transform.lossyScale.x;
            Vector3 size = new Vector3(b.size.x + 0.3f, b.size.y + 0.3f, 0.1f);       // metres
            Vector3 local = root.transform.InverseTransformPoint(b.center);             // model units
            local.z = -0.187f + 0.05f / s;          // flush with the bar model's back wall (outer face at -0.187)

            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            panel.name = PanelName;
            Object.DestroyImmediate(panel.GetComponent<Collider>());   // the room already has a collision wall here
            panel.transform.SetParent(root.transform, false);
            panel.transform.localPosition = local;
            panel.transform.localRotation = Quaternion.identity;
            panel.transform.localScale = size / s;
            Vector3 center = local * s;
            if (wallMat) panel.GetComponent<Renderer>().sharedMaterial = wallMat;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Debug.Log($"[RB] Rest room: louvres hidden, wall panel at {center:F2} size {size:F2}, material {(wallMat ? wallMat.name : "default")}");
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static Transform FindDeep(Transform t, string name)
    {
        if (t.name == name) return t;
        foreach (Transform c in t) { var f = FindDeep(c, name); if (f) return f; }
        return null;
    }
}
