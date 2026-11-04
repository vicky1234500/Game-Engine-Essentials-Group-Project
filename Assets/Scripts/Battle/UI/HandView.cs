using System.Collections.Generic;
using UnityEngine;

// Shows the cards in hand. Put on a panel at the bottom of the battle Canvas
// with a Horizontal Layout Group, and assign the CardView prefab.
public class HandView : MonoBehaviour
{
    [SerializeField] DeckManager deck;
    [SerializeField] CardView cardPrefab;

    readonly List<CardView> views = new List<CardView>();

    void OnEnable()
    {
        if (!deck) deck = FindFirstObjectByType<DeckManager>();
        deck.OnPilesChanged += Rebuild;
        Rebuild();
    }

    void OnDisable()
    {
        if (deck) deck.OnPilesChanged -= Rebuild;
    }

    // Simple version: destroy and recreate. TODO: pool / animate draw & play later.
    void Rebuild()
    {
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
