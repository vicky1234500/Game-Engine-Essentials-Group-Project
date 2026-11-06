using UnityEditor;
using UnityEngine;

// Tools > Roguelike > Apply Enemy Models (safe)
// Gives each EnemyData its 3D model. Only those fields change, and scenes aren't touched.
// To try a different model or size, edit the list below and run it again, or change the EnemyData in the Inspector.
public static class EnemyModelSetup
{
    struct Entry
    {
        public string enemy, prefab;
        public float height, lift;
        public bool voice;

        public Entry(string enemy, string prefab, float height, float lift, bool voice)
        {
            this.enemy = enemy; this.prefab = prefab; this.height = height; this.lift = lift; this.voice = voice;
        }
    }

    static readonly Entry[] Entries =
    {
        //         EnemyData          Assets/Prefab/...                         height  lift  says "augh"
        new Entry("SecurityDrone",   "Enemy_EyeDrone",                          0.8f,  0.9f, false),
        new Entry("Rogue",           "protective suit Variant",                 1.8f,  0f,   true),
        new Entry("StreetEnforcer",  "boss",                                    2.3f,  0f,   false),
        new Entry("Netrunner",       "Sci_Fi_Character_08_01 Variant",          1.9f,  0f,   true),
        new Entry("WardenMech",      "Biomech_Mutant_Skin_1_bloody Variant",    3.0f,  0f,   true),   // boss, shown as "Biomech Warlord"
    };

    [MenuItem("Tools/Roguelike/Apply Enemy Models (safe)")]
    public static void Apply()
    {
        int done = 0;
        foreach (var e in Entries)
        {
            var data = AssetDatabase.LoadAssetAtPath<EnemyData>($"Assets/Data/Enemies/{e.enemy}.asset");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefab/{e.prefab}.prefab");
            if (!data || !prefab)
            {
                Debug.LogWarning($"Enemy models: skipped {e.enemy}, couldn't find its EnemyData or the prefab '{e.prefab}'.");
                continue;
            }
            if (prefab.GetComponentsInChildren<Renderer>(true).Length == 0)
                Debug.LogWarning($"Enemy models: '{e.prefab}' has no visible mesh yet. Its model files may be missing.", prefab);

            var so = new SerializedObject(data);
            so.FindProperty("modelPrefab").objectReferenceValue = prefab;
            so.FindProperty("modelHeight").floatValue = e.height;
            so.FindProperty("modelLift").floatValue = e.lift;
            so.FindProperty("hasHurtVoice").boolValue = e.voice;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(data);
            done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Enemy models: {done} of {Entries.Length} enemies updated. If one faces the wrong way, change Model Yaw on its EnemyData.");
    }
}
