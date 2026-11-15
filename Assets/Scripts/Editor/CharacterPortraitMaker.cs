using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

// Tools > Roguelike > Make Character Portraits
// Renders each character's model (head and shoulders, on the character's colour) to
// Assets/Art/Portraits/<character>.png and sets it as the character's portrait.
// Run it again after changing a model. Characters without a model are skipped.
public static class CharacterPortraitMaker
{
    const string Folder = "Assets/Art/Portraits";
    const int Size = 512;

    // Character asset name -> model, filled in when the character's Model slot is empty.
    static readonly System.Collections.Generic.Dictionary<string, string> DefaultModels = new System.Collections.Generic.Dictionary<string, string>
    {
        { "Juggernaut", "Assets/models/player/Juggernaut/juggernaut_cyborg.glb" },   // HypJo, CC BY (see CREDITS.txt)
    };

    [MenuItem("Tools/Roguelike/Make Character Portraits")]
    public static void MakeAll()
    {
        Directory.CreateDirectory(Folder);
        int made = 0;
        foreach (var guid in AssetDatabase.FindAssets("t:CharacterData"))
        {
            var c = AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(guid));
            if (c && !c.model && DefaultModels.TryGetValue(c.name, out var modelPath))
            {
                c.model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
                if (c.model) { EditorUtility.SetDirty(c); Debug.Log($"Portraits: {c.name} model set to {modelPath}"); }
                else Debug.LogWarning($"Portraits: {modelPath} not found or not imported yet.");
            }
            if (!c || !c.model) { Debug.Log($"Portraits: {(c ? c.name : guid)} has no model, skipped."); continue; }
            string path = $"{Folder}/{c.name}.png";
            if (!Render(c, path)) continue;

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();

            c.portrait = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            EditorUtility.SetDirty(c);
            made++;
        }
        AssetDatabase.SaveAssets();
        Debug.Log($"Portraits: made {made}. They are in {Folder}.");
    }

    static bool Render(CharacterData c, string path)
    {
        var scene = EditorSceneManager.NewPreviewScene();
        var rt = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
        try
        {
            var model = (GameObject)PrefabUtility.InstantiatePrefab(c.model, scene);
            model.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            var anim = model.GetComponentInChildren<Animator>();
            if (anim) anim.enabled = false;                 // keep the bind pose

            if (!TryGetBounds(model, out var b)) { Debug.LogWarning($"Portraits: {c.name}'s model has nothing to draw."); return false; }

            // Head and shoulders: the top ~35% of the model
            float h = b.size.y;
            Vector3 focus = new Vector3(b.center.x, b.max.y - h * 0.17f, b.center.z);
            float frameHeight = h * 0.4f;

            var camGo = new GameObject("PortraitCamera");
            SceneManager_Move(camGo, scene);
            var cam = camGo.AddComponent<Camera>();
            cam.scene = scene;
            cam.fieldOfView = 25f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = Color.Lerp(c.color, Color.black, 0.55f);
            cam.targetTexture = rt;
            float dist = frameHeight * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            Vector3 dir = Quaternion.Euler(0, 20, 0) * model.transform.forward;       // slight three-quarter view
            cam.transform.position = focus + dir * dist + Vector3.up * h * 0.03f;
            cam.transform.LookAt(focus);

            AddLight(scene, Quaternion.Euler(30, 160, 0) * Vector3.forward, 1.3f, Color.white);   // key, from the front
            AddLight(scene, Quaternion.Euler(20, -30, 0) * Vector3.forward, 0.6f, c.color);       // rim, in the character colour

            // The editor compiles shaders in the background and skips anything not compiled yet,
            // which left glTF models (Juggernaut) out of the picture. Compile them now instead.
            bool wasAsync = ShaderUtil.allowAsyncCompilation;
            ShaderUtil.allowAsyncCompilation = false;
            try
            {
                var request = new RenderPipeline.StandardRequest { destination = rt };
                if (RenderPipeline.SupportsRenderRequest(cam, request)) RenderPipeline.SubmitRenderRequest(cam, request);
                else cam.Render();
            }
            finally { ShaderUtil.allowAsyncCompilation = wasAsync; }

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            return true;
        }
        finally
        {
            rt.Release();
            Object.DestroyImmediate(rt);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static void AddLight(UnityEngine.SceneManagement.Scene scene, Vector3 shineDirection, float intensity, Color color)
    {
        var go = new GameObject("PortraitLight");
        SceneManager_Move(go, scene);
        var light = go.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = intensity;
        light.color = color;
        go.transform.rotation = Quaternion.LookRotation(shineDirection);
    }

    static void SceneManager_Move(GameObject go, UnityEngine.SceneManagement.Scene scene) =>
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(go, scene);

    static bool TryGetBounds(GameObject go, out Bounds bounds)
    {
        bounds = default;
        bool any = false;
        foreach (var r in go.GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer) continue;
            if (!any) { bounds = r.bounds; any = true; }
            else bounds.Encapsulate(r.bounds);
        }
        return any;
    }
}
