using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

// Shows the hand as 3D Card prefabs, seen by their own "Card Camera".
// - The cards and the Card Camera sit far below the level (y = -1000), on the "Cards" layer.
// - The Card Camera only renders the Cards layer, and the main camera stops rendering it.
// - The Card Camera is an Overlay camera in the main camera's stack (URP), so the cards draw
//   on top of the 3D world but under the UI.
// HandView makes this when its "Card 3D Prefab" is set. Layout settings are on HandView.
public class CardHand3D : MonoBehaviour
{
    public const string LayerName = "Cards";
    static readonly Vector3 FarAway = new Vector3(0f, -1000f, 0f);

    HandView settings;
    GameObject prefab;
    Camera cam;
    int layer;
    readonly Dictionary<CardInstance, CardView3D> views = new Dictionary<CardInstance, CardView3D>();
    readonly List<CardView3D> order = new List<CardView3D>();

    public static CardHand3D Create(HandView settings, GameObject prefab)
    {
        var go = new GameObject("3D Card Hand");
        go.transform.position = FarAway;
        var hand = go.AddComponent<CardHand3D>();
        hand.settings = settings;
        hand.prefab = prefab;
        hand.layer = LayerMask.NameToLayer(LayerName);
        if (hand.layer < 0)
        {
            Debug.LogWarning("3D cards: there's no 'Cards' layer. Run Tools > Roguelike > Set Up 3D Cards. Using layer 31 for now.");
            hand.layer = 31;
        }
        hand.MakeCamera();
        return hand;
    }

    void MakeCamera()
    {
        var camGo = new GameObject("Card Camera");
        camGo.transform.SetParent(transform, false);   // looks forward (+Z) at the cards
        cam = camGo.AddComponent<Camera>();
        cam.cullingMask = 1 << layer;                  // sees ONLY the cards
        cam.fieldOfView = 30f;
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 20f;
        cam.depth = 50;                                // EventSystem gives its clicks priority over the main camera's
        cam.GetUniversalAdditionalCameraData().renderType = CameraRenderType.Overlay;

        var raycaster = camGo.AddComponent<PhysicsRaycaster>();   // lets the cards get hover / click events
        raycaster.eventMask = 1 << layer;
    }

    // Stacks the Card Camera on whichever camera is the main one now (HandView calls this every fight).
    public void AttachToMainCamera()
    {
        var main = Camera.main;
        if (!main || main == cam)
        {
            Debug.LogWarning("3D cards: no Main Camera found, so the cards can't be shown.");
            return;
        }
        main.cullingMask &= ~(1 << layer);
        var stack = main.GetUniversalAdditionalCameraData().cameraStack;
        if (!stack.Contains(cam)) stack.Add(cam);
    }

    // Makes the 3D cards match the hand: new cards fly in, played/discarded cards disappear.
    public void Sync(List<CardInstance> hand)
    {
        for (int i = order.Count - 1; i >= 0; i--)
        {
            var v = order[i];
            if (v && hand.Contains(v.Card)) continue;
            if (v) { views.Remove(v.Card); Destroy(v.gameObject); }
            order.RemoveAt(i);
        }

        order.Clear();
        foreach (var card in hand)
        {
            if (!views.TryGetValue(card, out var view) || !view)
            {
                view = CardView3D.Create(prefab, card, transform, layer, settings.hoverRaise);
                view.transform.localPosition = DrawPileSpot();
                view.transform.localScale = view.BaseScale * settings.cardScale * 0.5f;   // starts small, grows in
                views[card] = view;
            }
            order.Add(view);
        }
    }

    // Bottom-left of the screen: where new cards come from.
    Vector3 DrawPileSpot()
    {
        float d = settings.cardDistance;
        float halfH = d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return new Vector3(-halfH * cam.aspect, -halfH - 0.5f, d);
    }

    // Fan the cards along the bottom of the screen. Hovered / selected cards rise up.
    void LateUpdate()
    {
        if (!cam) return;
        float d = settings.cardDistance;
        float halfH = d * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);   // half the screen height at that distance
        float halfW = halfH * cam.aspect;
        int n = order.Count;
        float step = n > 1 ? Mathf.Min(settings.cardSpacing, halfW * 1.5f / (n - 1)) : 0f;
        float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);                 // smooth movement

        for (int i = 0; i < n; i++)
        {
            var v = order[i];
            if (!v) continue;
            float t = i - (n - 1) / 2f;                                          // -2 -1 0 1 2...
            // Hovered: rises up and grows so you can read it.
            // Selected (waiting for you to click an enemy): only a little higher, so it doesn't cover the enemy.
            bool up = v.Hovered || v.Selected;
            float raise = v.Hovered ? settings.hoverRaise : v.Selected ? settings.selectedRaise : 0f;

            var pos = new Vector3(
                t * step,
                -halfH + settings.cardLift - t * t * settings.fanDrop + raise,
                d - i * 0.01f - (up ? 0.3f : 0f));                               // later cards slightly in front
            var rot = Quaternion.Euler(0f, settings.cardYaw, up ? 0f : -t * settings.fanAngle);
            float scale = settings.cardScale * (v.Hovered ? settings.hoverScale : 1f);

            v.transform.localPosition = Vector3.Lerp(v.transform.localPosition, pos, k);
            v.transform.localRotation = Quaternion.Slerp(v.transform.localRotation, rot, k);
            v.transform.localScale = Vector3.Lerp(v.transform.localScale, v.BaseScale * scale, k);
        }
    }
}
