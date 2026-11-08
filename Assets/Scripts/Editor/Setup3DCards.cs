using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Tools > Roguelike > Set Up 3D Cards (safe)
// 1. Adds a "Cards" layer to the project if it's missing (only the Card Camera renders it).
// 2. Sets "Card 3D Prefab" on every HandView in the open scene(s) to Assets/Prefab/Card.prefab.
// Only that one field changes. Save the scene afterwards (Ctrl+S).
public static class Setup3DCards
{
    const string PrefabPath = "Assets/Prefab/Card.prefab";

    [MenuItem("Tools/Roguelike/Set Up 3D Cards (safe)")]
    public static void Run()
    {
        AddLayer(CardHand3D.LayerName);

        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (!prefab) { Debug.LogError("3D cards: couldn't find " + PrefabPath); return; }

        var hands = Object.FindObjectsByType<HandView>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var h in hands)
        {
            var so = new SerializedObject(h);
            so.FindProperty("card3DPrefab").objectReferenceValue = prefab;
            so.FindProperty("cardYaw").floatValue = 180f;   // the Card prefab's text faces +Z, so turn it to face the camera
            so.ApplyModifiedProperties();      // with Undo, so Ctrl+Z works
            EditorSceneManager.MarkSceneDirty(h.gameObject.scene);
        }

        if (hands.Length == 0)
            Debug.LogWarning("3D cards: no HandView in the open scene. Open Dungeon (or BattleTest) and run this again.");
        else
            Debug.Log($"3D cards: turned on for {hands.Length} HandView(s). Save the scene (Ctrl+S). " +
                      "To go back to 2D cards, clear 'Card 3D Prefab' on the HandView.");
    }

    static void AddLayer(string name)
    {
        if (LayerMask.NameToLayer(name) >= 0) return;
        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers = tagManager.FindProperty("layers");
        for (int i = 8; i < layers.arraySize; i++)            // 8+ are free user layers
        {
            var layer = layers.GetArrayElementAtIndex(i);
            if (!string.IsNullOrEmpty(layer.stringValue)) continue;
            layer.stringValue = name;
            tagManager.ApplyModifiedProperties();
            Debug.Log($"3D cards: added layer '{name}' (layer {i}).");
            return;
        }
        Debug.LogError("3D cards: no free layer slot for '" + name + "'.");
    }
}
