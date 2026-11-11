using UnityEngine;
using UnityEngine.EventSystems;

// One 3D card in the hand. CardHand3D adds this to a copy of the Card prefab.
// Clicks and hover come from the Physics Raycaster on the Card Camera, like EnemyClickTarget does with the main camera.
public class CardView3D : CardViewBase, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    public bool Hovered { get; private set; }
    public bool Selected { get; private set; }
    public Vector3 BaseScale { get; private set; }   // the prefab's own scale (it's thin in Z), hover scales on top of this

    // Makes the card from the prefab, fills in its texts and puts it on the Cards layer.
    // extraHitboxBelow: the click box reaches this far down, so a card raised on hover doesn't slip away from the mouse.
    public static CardView3D Create(GameObject prefab, CardInstance card, Transform parent, int layer, float extraHitboxBelow)
    {
        var go = Instantiate(prefab, parent, false);
        go.transform.localRotation = Quaternion.identity;
        var view = go.AddComponent<CardView3D>();
        view.Card = card;
        view.BaseScale = go.transform.localScale;

        // The prefab's own Card script fills in name, cost and description, and puts the item's model on the card.
        GameObject itemModel = card.sourceItem ? card.sourceItem.model : null;
        bool modelPlaced = false;
        foreach (var c in go.GetComponentsInChildren<Card>(true))
        {
            c.Setup(card.data, modelPlaced ? null : itemModel, 1);   // only one copy of the model
            if (c.models && itemModel && !modelPlaced) { modelPlaced = true; MakeStill(c.models); FitModel(c.models, go.transform); }
            if (c.manaCost) c.manaCost.text = card.Playable ? card.ManaCost.ToString() : "-";
        }

        // Only the Card Camera sees the Cards layer.
        foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

        // The card's text canvas shouldn't catch UI clicks.
        foreach (var gr in go.GetComponentsInChildren<UnityEngine.UI.GraphicRaycaster>(true)) gr.enabled = false;

        view.AddHitbox(extraHitboxBelow);
        view.AddOutline();
        return view;
    }

    // Item models can come with gun scripts or physics. On a card they should just sit there.
    static void MakeStill(Transform modelHolder)
    {
        foreach (var rb in modelHolder.GetComponentsInChildren<Rigidbody>(true)) Destroy(rb);
        foreach (var b in modelHolder.GetComponentsInChildren<MonoBehaviour>(true)) b.enabled = false;
        foreach (var a in modelHolder.GetComponentsInChildren<Animator>(true)) a.enabled = false;
        // The Light Knife has a glowing light built in: on a card it lit up the whole hand.
        foreach (var l in modelHolder.GetComponentsInChildren<Light>(true)) l.enabled = false;
        foreach (var ps in modelHolder.GetComponentsInChildren<ParticleSystem>(true)) ps.gameObject.SetActive(false);
    }

    // Card.Setup makes the model 1 unit big, but the card is only 0.44 x 0.64, so the model covered everything.
    // Shrink it to fit the picture area between the name and the description, and put it on the front face.
    const float ModelSize = 0.26f;   // world units at card scale 1

    static void FitModel(Transform holder, Transform cardRoot)
    {
        if (holder.childCount == 0) return;
        var model = holder.GetChild(holder.childCount - 1);
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0) return;

        // Lay the model so its long side lies flat on the card (a knife pointing at you looked like a dot).
        // Try it turned different ways and keep the one that shows the most of it from the front.
        Quaternion start = model.localRotation, best = start;
        float bestArea = -1f;
        foreach (float x in new[] { 0f, 90f })
        foreach (float y in new[] { 0f, 90f })
        foreach (float z in new[] { 0f, 90f })
        {
            model.localRotation = Quaternion.Euler(x, y, z) * start;
            Bounds tb = BoundsOf(renderers);
            float area = tb.size.x * tb.size.y;                 // how big it looks from the front
            if (area > bestArea * 1.05f) { bestArea = area; best = model.localRotation; }
        }
        model.localRotation = best;

        Bounds b = BoundsOf(renderers);
        float biggest = Mathf.Max(b.size.x, b.size.y);           // Z is squashed flat by the card anyway
        float cardScale = Mathf.Max(cardRoot.lossyScale.x, 0.0001f);
        if (biggest > 0f) model.localScale *= ModelSize * cardScale / biggest;

        b = BoundsOf(renderers);
        Vector3 front = cardRoot.forward * (b.extents.z + 0.006f * cardScale);   // the text side is +Z
        model.position += holder.position - b.center + front;
    }

    static Bounds BoundsOf(Renderer[] renderers)
    {
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        return b;
    }

    // A box collider around the whole card so the Card Camera's raycaster can hit it.
    void AddHitbox(float extraBelow)
    {
        Vector3 size = new Vector3(0.44f, 0.64f, 0.05f);   // Card prefab size if it has no mesh
        Vector3 center = Vector3.zero;
        var renderers = GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            var b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            Vector3 s = transform.lossyScale;
            center = transform.InverseTransformPoint(b.center);
            size = new Vector3(b.size.x / s.x, b.size.y / s.y, Mathf.Max(b.size.z / s.z, 0.05f));
        }
        float below = extraBelow / Mathf.Max(transform.lossyScale.y, 0.0001f);
        var box = gameObject.AddComponent<BoxCollider>();
        box.center = center - new Vector3(0, below / 2f, 0);
        box.size = size + new Vector3(0, below, 0);
    }

    // Outlines the card itself (not the item model on it) while it is selected.
    const float SelectedOutlineWidth = 6f;
    Outline outline;

    void AddOutline()
    {
        var card = GetComponentInChildren<Card>(true);
        foreach (var filter in GetComponentsInChildren<MeshFilter>(true))
        {
            if (card && card.models && filter.transform.IsChildOf(card.models)) continue;
            if (!filter.sharedMesh || !filter.sharedMesh.isReadable) continue;

            outline = filter.gameObject.AddComponent<Outline>();
            outline.OutlineWidth = SelectedOutlineWidth;
            outline.enabled = false;
            return;
        }
        Debug.LogWarning("CardView3D: no readable card mesh found, so the card can't show an outline. Turn on Read/Write for the card model.", this);
    }

    public override void SetSelected(bool selected)
    {
        Selected = selected;
        if (outline) outline.enabled = selected;
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (BattleUI.Instance) BattleUI.Instance.OnCardClicked(this);
        else SetSelected(!Selected);   // no battle running (the preview scene): just toggle it
    }

    public void OnPointerEnter(PointerEventData e) => Hovered = true;
    public void OnPointerExit(PointerEventData e) => Hovered = false;
}
