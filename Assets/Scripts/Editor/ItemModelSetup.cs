using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

// Tools > Roguelike > Apply Item Models + Make Icons (safe)
// 1. Gives each ItemData its 3D model (list below). The 3D cards show it.
// 2. Takes a picture of the model in the item's shape (Knife 2x1 = 256x128) and saves it to
//    Assets/Art/ItemIcons/<Item>.png, then uses it as the item's icon. The inventory and 2D cards show the icon.
// Only the "model" and "icon" fields change. Old icon images aren't deleted.
// Items not in the list keep their old icon. To change a model, edit the list and run it again.
public static class ItemModelSetup
{
    const string IconFolder = "Assets/Art/ItemIcons";
    const int PixelsPerSlot = 128;

    static readonly string[,] Models =
    {
        // ItemData (Assets/Data/Items/)   model prefab
        { "AttackDrone",        "Assets/Prefab/Enemy_EyeDrone.prefab" },
        { "Bandage",            "Assets/Prefab/medical_tape_4k 1.prefab" },
        { "BlackMarketImplant", "Assets/Prefab/Chip Variant 1.prefab" },
        { "Cyberdeck",          "Assets/Prefab/cyberdeck.prefab" },
        { "Knife",              "Assets/Prefab/LightKnife  Blue.prefab" },
        { "EnergySword",        "Assets/models/weapons/EnergySword/Energy Sword.glb" },   // ova Xen, CC0
        { "GrapplingHook",      "Assets/Prefab/cartoon_grappling_hook.prefab" },
        { "GrenadeBelt",        "Assets/Prefab/SCi-fi_Grenade Variant.prefab" },
        { "KineticVest",        "Assets/models/items/sci-fi_chest_armor.glb" },
        { "MapUplink",          "Assets/Prefab/modern_tablet_.prefab" },
        { "MedPod",             "Assets/Prefab/sci-fi_first_aid_kit.prefab" },
        { "NanoInjector",       "Assets/Prefab/Prop_Syringe.prefab" },
        { "NanoPlating",        "Assets/models/items/sci-fi_armor_part.glb" },
        { "NeuralBooster",      "Assets/models/items/circuit-board-design/source/circuitboard.glb" },
        { "OverclockedExoSuit", "Assets/Prefab/armor.prefab" },
        { "OverclockedSMG",     "Assets/models/weapons/Sci Fi Guns/Prefabs/Sci Fi SMG.prefab" },
        { "Pistol",             "Assets/Prefab/K-39.prefab" },
        { "PortableWorkbench",  "Assets/Prefab/desk__design___sci-fi_workstation__neon_termi.prefab" },
        { "Railgun",            "Assets/Prefab/Railgun 1.prefab" },
        { "RiotShield",         "Assets/Prefab/sci-fi_energy_riot_shield.prefab" },
        { "ShockBaton",         "Assets/Prefab/stun_baton.prefab" },
        { "Shotgun",            "Assets/Prefab/HandRailGun.prefab" },
        { "SmokeGrenades",      "Assets/Prefab/incendiary_grenade Variant.prefab" },
        { "StimPack",           "Assets/Prefab/Prop_Syringe.prefab" },
        { "StolenCreditChip",   "Assets/Prefab/credit_card 1.prefab" },
        { "TacticalAICore",     "Assets/Prefab/sci_fi_energy_core_1.prefab" },
    };

    [MenuItem("Tools/Roguelike/Apply Item Models + Make Icons (safe)")]
    public static void Apply()
    {
        Directory.CreateDirectory(IconFolder);
        int done = 0;
        for (int i = 0; i < Models.GetLength(0); i++)
        {
            string itemName = Models[i, 0], modelPath = Models[i, 1];
            var item = AssetDatabase.LoadAssetAtPath<ItemData>($"Assets/Data/Items/{itemName}.asset");
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            if (!item || !model)
            {
                string why = modelPath.EndsWith(".glb") || modelPath.Contains("Prefab/")
                    ? " If it's a .glb model (or a prefab made from one), install glTFast: Package Manager > + > Add package by name > com.unity.cloud.gltfast"
                    : "";
                Debug.LogWarning($"Item models: skipped {itemName}, couldn't load the item or '{modelPath}'.{why}");
                continue;
            }

            Sprite icon = null;
            try { icon = MakeIcon(model, item.size, $"{IconFolder}/{itemName}.png"); }
            catch (System.Exception e) { Debug.LogWarning($"Item models: couldn't make the icon for {itemName}: {e.Message}"); }

            var so = new SerializedObject(item);
            so.FindProperty("model").objectReferenceValue = model;
            if (icon) so.FindProperty("icon").objectReferenceValue = icon;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(item);
            done++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Item models: {done} items updated. Icons are in {IconFolder}.");
    }

    // Renders the model in a hidden scene with its own camera and lights, in the item's shape.
    static Sprite MakeIcon(GameObject prefab, Vector2Int slots, string path)
    {
        int w = Mathf.Max(1, slots.x) * PixelsPerSlot, h = Mathf.Max(1, slots.y) * PixelsPerSlot;
        float aspect = (float)w / h;
        Scene scene = EditorSceneManager.NewPreviewScene();
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return null;

            // Try the model lying different ways and keep the one that fills the slot best (long knife = sideways).
            var tilt = Quaternion.Euler(20f, -30f, 0f);          // a little from above and the side, so it looks 3D
            Quaternion best = Quaternion.identity;
            float bestFill = -1f;
            foreach (float x in new[] { 0f, 90f })
            foreach (float y in new[] { 0f, 90f })
            foreach (float z in new[] { 0f, 90f })
            {
                model.transform.rotation = tilt * Quaternion.Euler(x, y, z);
                Bounds b = BoundsOf(renderers);
                float scale = Mathf.Min(aspect / Mathf.Max(b.size.x, 1e-4f), 1f / Mathf.Max(b.size.y, 1e-4f));
                // fill = how much of the slot it covers; times the real size seen from the front,
                // so a knife pointing at the camera (a small dot) doesn't win.
                float fill = scale * scale * b.size.x * b.size.y / aspect * (b.size.x * b.size.y);
                if (fill > bestFill * 1.05f) { bestFill = fill; best = model.transform.rotation; }
            }
            model.transform.rotation = best;
            Bounds bounds = BoundsOf(renderers);

            var camGo = new GameObject("Icon Camera");
            SceneManager.MoveGameObjectToScene(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(bounds.size.y, bounds.size.x / aspect) * 0.5f * 1.1f;
            cam.transform.position = bounds.center - Vector3.forward * (bounds.extents.z + 5f);
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = bounds.size.z + 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.targetTexture = rt;

            AddLight(scene, new Vector3(40f, -30f, 0f), 1.2f);  // key light
            AddLight(scene, new Vector3(-20f, 150f, 0f), 0.6f); // fill light from behind

            // Some materials (like the see-through tape) don't write alpha, so the icon came out empty.
            // Instead: take the picture on black and on white. Where the two differ is background.
            Color32[] onBlack = Shoot(cam, rt, Color.black), onWhite = Shoot(cam, rt, Color.white);
            var pixels = new Color32[onBlack.Length];
            for (int p = 0; p < pixels.Length; p++)
            {
                Color b = onBlack[p], wh = onWhite[p];
                float alpha = Mathf.Clamp01(1f - ((wh.r - b.r) + (wh.g - b.g) + (wh.b - b.b)) / 3f);
                Color c = alpha > 0.001f ? new Color(b.r / alpha, b.g / alpha, b.b / alpha, alpha) : Color.clear;
                pixels[p] = c;
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(pixels);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            cam.targetTexture = null;
        }
        finally
        {
            rt.Release();
            Object.DestroyImmediate(rt);
            EditorSceneManager.ClosePreviewScene(scene);
        }

        AssetDatabase.ImportAsset(path);
        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    // Renders the camera once with this background colour and returns the pixels.
    static Color32[] Shoot(Camera cam, RenderTexture rt, Color background)
    {
        cam.backgroundColor = background;
        var request = new RenderPipeline.StandardRequest { destination = rt };
        if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
        else cam.Render();

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false);
        var previous = RenderTexture.active;
        RenderTexture.active = rt;
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = previous;
        var pixels = tex.GetPixels32();
        Object.DestroyImmediate(tex);
        return pixels;
    }

    static Bounds BoundsOf(Renderer[] renderers)
    {
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    static void AddLight(Scene scene, Vector3 euler, float intensity)
    {
        var go = new GameObject("Icon Light");
        SceneManager.MoveGameObjectToScene(go, scene);
        go.transform.rotation = Quaternion.Euler(euler);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
    }
}
