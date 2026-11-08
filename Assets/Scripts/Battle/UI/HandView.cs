using System.Collections.Generic;
using UnityEngine;

// Shows the cards in hand. Put on a panel at the bottom of the battle Canvas
// with a Horizontal Layout Group, and assign the CardView prefab.
// 3D cards: set "Card 3D Prefab" to Assets/Prefab/Card.prefab (or run Tools > Roguelike > Set Up 3D Cards).
// Then CardHand3D shows the hand as 3D cards with their own camera, and the 2D CardViews aren't used.
public class HandView : MonoBehaviour
{
    [SerializeField] DeckManager deck;
    [SerializeField] CardView cardPrefab;

    [Header("3D cards (leave empty to use the 2D CardView)")]
    public GameObject card3DPrefab;
    [Tooltip("How far in front of the Card Camera the cards are. Bigger = cards look smaller.")]
    public float cardDistance = 3f;
    public float cardScale = 1f;
    [Tooltip("Turn the cards around. The Card prefab's front (text) is on its +Z side, so 180 shows the front.")]
    public float cardYaw = 180f;
    [Tooltip("Space between cards (shrinks when the hand is full).")]
    public float cardSpacing = 0.42f;
    [Tooltip("How high the cards sit from the bottom edge of the screen.")]
    public float cardLift = 0.15f;
    [Tooltip("Tilt of the outer cards, in degrees.")]
    public float fanAngle = 4f;
    [Tooltip("How much lower the outer cards sit, to make an arc.")]
    public float fanDrop = 0.01f;
    [Tooltip("How far a card rises when hovered or selected.")]
    public float hoverRaise = 0.3f;
    public float hoverScale = 1.15f;
    [Tooltip("How far the selected card stays up while you pick an enemy. Small, so it doesn't cover the enemy.")]
    public float selectedRaise = 0.08f;

    readonly List<CardView> views = new List<CardView>();
    CardHand3D hand3D;

    void OnEnable()
    {
        if (!deck) deck = FindFirstObjectByType<DeckManager>();
        if (card3DPrefab)
        {
            if (!hand3D) hand3D = CardHand3D.Create(this, card3DPrefab);
            hand3D.gameObject.SetActive(true);
            hand3D.AttachToMainCamera();
        }
        deck.OnPilesChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (deck) deck.OnPilesChanged -= Rebuild;
        if (hand3D) hand3D.gameObject.SetActive(false);    // hide the 3D cards outside of battle
    }

    void OnDestroy()
    {
        if (hand3D) Destroy(hand3D.gameObject);
    }

    // 2D: destroy and recreate. 3D: CardHand3D keeps cards that are still in hand so they can slide around.
    void Rebuild()
    {
        if (hand3D)
        {
            hand3D.Sync(deck.hand);
            return;
        }

        foreach (var v in views) if (v) Destroy(v.gameObject);
        views.Clear();

        foreach (var card in deck.hand)
        {
            var view = Instantiate(cardPrefab, transform);
            view.Setup(card);
            views.Add(view);
        }
    }
}
