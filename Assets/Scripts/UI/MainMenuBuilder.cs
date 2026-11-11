using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// Menu: Tools > Roguelike > Build Main Menu Scene
public static class MainMenuBuilder
{
    const string ScenePath = "Assets/Scenes/MainMenu.unity";
    const string BackgroundPath = "Assets/UI/Backgrounds/bg_menu.png";   // optional
    const string GameTitle = "YOUR GAME NAME";

    static readonly Color Bg = Hex("#070A12");
    static readonly Color Panel = Hex("#0E1424");
    static readonly Color Raised = Hex("#161E33");
    static readonly Color Hover = Hex("#0E3550");
    static readonly Color Pressed = Hex("#00B8CC");
    static readonly Color Disabled = Hex("#0D1220");
    static readonly Color Cyan = Hex("#00E5FF");
    static readonly Color Light = Hex("#E6F1FF");
    static readonly Color Dim = Hex("#7F8DB0");

    static Font font;

    [MenuItem("Tools/Roguelike/Build Main Menu Scene")]
    public static void Build()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
        camGo.tag = "MainCamera";
        var cam = camGo.GetComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Bg;

        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        var bg = Img("Background", canvas, Color.white);
        Stretch(bg.rectTransform);
        var sprite = LoadSprite(BackgroundPath);
        if (sprite) bg.sprite = sprite; else bg.color = Bg;

        var title = Txt("Title", canvas, GameTitle, 110, TextAnchor.MiddleLeft, Light, FontStyle.Bold);
        PlaceLeft(title.rectTransform, new Vector2(160, 250), new Vector2(1100, 150));
        var outline = title.gameObject.AddComponent<UnityEngine.UI.Outline>();
        outline.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.55f);
        outline.effectDistance = new Vector2(3, -3);
        var sub = Txt("Subtitle", canvas, "TURN-BASED ROGUELIKE", 30, TextAnchor.MiddleLeft, Dim);
        PlaceLeft(sub.rectTransform, new Vector2(166, 160), new Vector2(900, 50));

        var start = Btn("StartButton", canvas, "START GAME", new Vector2(160, 40));
        var load = Btn("LoadButton", canvas, "LOAD GAME", new Vector2(160, -64));
        var settings = Btn("SettingsButton", canvas, "SETTINGS", new Vector2(160, -168));
        var quit = Btn("QuitButton", canvas, "QUIT", new Vector2(160, -272));

        var panel = new GameObject("SettingsPanel", typeof(RectTransform));
        panel.transform.SetParent(canvas, false);
        Stretch((RectTransform)panel.transform);
        var dim = Img("Dim", panel.transform, new Color(0, 0, 0, 0.75f));
        Stretch(dim.rectTransform);
        var box = Img("Box", panel.transform, Panel);
        Center(box.rectTransform, Vector2.zero, new Vector2(900, 560));
        AddBorder(box.gameObject);
        var boxTitle = Txt("Title", box.transform, "SETTINGS", 56, TextAnchor.MiddleCenter, Light, FontStyle.Bold);
        Center(boxTitle.rectTransform, new Vector2(0, 205), new Vector2(800, 80));

        var volLabel = Txt("VolumeLabel", box.transform, "VOLUME", 34, TextAnchor.MiddleLeft, Light);
        PlaceLeftCentered(volLabel.rectTransform, new Vector2(-400, 70), new Vector2(300, 50));
        var slider = MakeSlider(box.transform, new Vector2(130, 70), new Vector2(480, 36));
        var toggle = MakeToggle(box.transform, "FULLSCREEN", new Vector2(-400, -50));

        var back = Btn("BackButton", box.transform, "BACK", new Vector2(0, -190));
        var backRt = (RectTransform)back.transform;
        backRt.anchorMin = backRt.anchorMax = new Vector2(0.5f, 0.5f);
        backRt.pivot = new Vector2(0.5f, 0.5f);
        backRt.sizeDelta = new Vector2(360, 80);

        var ctrlGo = new GameObject("MainMenuController", typeof(MainMenuController));
        var so = new SerializedObject(ctrlGo.GetComponent<MainMenuController>());
        so.FindProperty("startButton").objectReferenceValue = start;
        so.FindProperty("loadButton").objectReferenceValue = load;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("backButton").objectReferenceValue = back;
        so.FindProperty("settingsPanel").objectReferenceValue = panel;
        so.FindProperty("volumeSlider").objectReferenceValue = slider;
        so.FindProperty("fullscreenToggle").objectReferenceValue = toggle;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        PutMenuFirstInBuild();
        Debug.Log("Built " + ScenePath + " and put it first in the build list.");
    }

    static void PutMenuFirstInBuild()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    static Sprite LoadSprite(string path)
    {
        if (!System.IO.File.Exists(path)) return null;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer != null && importer.textureType != TextureImporterType.Sprite)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static Slider MakeSlider(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = DefaultControls.CreateSlider(new DefaultControls.Resources());
        go.name = "VolumeSlider";
        go.transform.SetParent(parent, false);
        Center((RectTransform)go.transform, pos, size);
        go.transform.Find("Background").GetComponent<Image>().color = Raised;
        go.transform.Find("Fill Area/Fill").GetComponent<Image>().color = Cyan;
        go.transform.Find("Handle Slide Area/Handle").GetComponent<Image>().color = Light;
        var slider = go.GetComponent<Slider>();
        slider.minValue = 0f;
        slider.maxValue = 1f;
        slider.value = 0.8f;
        return slider;
    }

    static Toggle MakeToggle(Transform parent, string label, Vector2 pos)
    {
        var root = new GameObject("FullscreenToggle", typeof(RectTransform), typeof(Toggle));
        root.transform.SetParent(parent, false);
        var rt = (RectTransform)root.transform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(520, 56);

        var bgImg = Img("Background", root.transform, Raised);
        var bgRt = bgImg.rectTransform;
        bgRt.anchorMin = bgRt.anchorMax = new Vector2(0f, 0.5f);
        bgRt.pivot = new Vector2(0f, 0.5f);
        bgRt.anchoredPosition = Vector2.zero;
        bgRt.sizeDelta = new Vector2(44, 44);
        AddBorder(bgImg.gameObject);

        var check = Img("Checkmark", bgImg.transform, Cyan);
        Stretch(check.rectTransform);
        check.rectTransform.offsetMin = new Vector2(8, 8);
        check.rectTransform.offsetMax = new Vector2(-8, -8);

        var text = Txt("Label", root.transform, label, 34, TextAnchor.MiddleLeft, Light);
        text.rectTransform.anchorMin = Vector2.zero;
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(64, 0);
        text.rectTransform.offsetMax = Vector2.zero;

        var toggle = root.GetComponent<Toggle>();
        toggle.targetGraphic = bgImg;
        toggle.graphic = check;
        toggle.isOn = true;
        return toggle;
    }

    static Button Btn(string name, Transform parent, string label, Vector2 pos)
    {
        var img = Img(name, parent, Color.white);
        PlaceLeft(img.rectTransform, pos, new Vector2(460, 84));
        AddBorder(img.gameObject);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var colors = b.colors;
        colors.normalColor = Raised;
        colors.highlightedColor = Hover;
        colors.selectedColor = Hover;
        colors.pressedColor = Pressed;
        colors.disabledColor = Disabled;
        b.colors = colors;
        var nav = b.navigation;
        nav.mode = Navigation.Mode.None;
        b.navigation = nav;
        var t = Txt("Label", img.transform, label, 38, TextAnchor.MiddleCenter, Light, FontStyle.Bold);
        Stretch(t.rectTransform);
        return b;
    }

    static void AddBorder(GameObject go)
    {
        var o = go.AddComponent<UnityEngine.UI.Outline>();
        o.effectColor = new Color(Cyan.r, Cyan.g, Cyan.b, 0.7f);
        o.effectDistance = new Vector2(2, -2);
    }

    static Image Img(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.color = color;
        return img;
    }

    static Text Txt(string name, Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.alignment = align;
        t.color = color; t.fontStyle = style; t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero;
    }

    static void PlaceLeft(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void PlaceLeftCentered(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Center(RectTransform rt, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static Color Hex(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out var c);
        return c;
    }
}