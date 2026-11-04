using System;
using System.Collections.Generic;
using UnityEngine;

// Draw pile, hand, discard pile and exhaust pile for one fight.
// The deck is built from the items in the backpack (InventoryGrid).
public class DeckManager : MonoBehaviour
{
    [SerializeField] int cardsPerTurn = 5;    // TODO: confirm with team
    [SerializeField] int maxHandSize = 10;

    public readonly List<CardInstance> drawPile = new List<CardInstance>();
    public readonly List<CardInstance> hand = new List<CardInstance>();
    public readonly List<CardInstance> discardPile = new List<CardInstance>();
    public readonly List<CardInstance> exhaustPile = new List<CardInstance>();
    public readonly List<CardInstance> returnNextTurn = new List<CardInstance>();   // e.g. Throw Knife

    public int CardsPerTurn => cardsPerTurn;
    public event Action OnPilesChanged;       // HandView / BattleUI refresh on this
    public event Action<CardInstance> OnCardDrawn;   // curses like System Error / Tracker react to this

    // Build from the backpack. Uses each item's card list (count = copies).
    public void BuildFromInventory(InventoryGrid inventory)
    {
        ClearAll();
        if (!inventory) { Debug.LogWarning("DeckManager: no inventory, deck is empty."); return; }

        foreach (var item in inventory.Items)
            foreach (var entry in item.data.cards)
                if (entry.card)
                    for (int i = 0; i < entry.count; i++)
                        drawPile.Add(new CardInstance(entry.card, item.data));

        Shuffle(drawPile);
        Changed();
    }

    // For testing a battle scene without an inventory.
    public void BuildFromList(List<CardData> cards)
    {
        ClearAll();
        foreach (var c in cards) if (c) drawPile.Add(new CardInstance(c));
        Shuffle(drawPile);
        Changed();
    }

    public void Draw(int count)
    {
        for (int i = 0; i < count; i++)
        {
            if (hand.Count >= maxHandSize) break;

            if (drawPile.Count == 0)
            {
                if (discardPile.Count == 0) break;     // nothing left anywhere
                drawPile.AddRange(discardPile);         // reshuffle discard into draw pile
                discardPile.Clear();
                Shuffle(drawPile);
            }

            var card = drawPile[drawPile.Count - 1];
            drawPile.RemoveAt(drawPile.Count - 1);
            hand.Add(card);
            OnCardDrawn?.Invoke(card);
        }
        Changed();
    }

    // After a card is played.
    public void MoveToDiscardOrExhaust(CardInstance card)
    {
        hand.Remove(card);
        if (card.data.exhaust) exhaustPile.Add(card);
        else if (card.data.returnToHandNextTurn) returnNextTurn.Add(card);
        else discardPile.Add(card);
        Changed();
    }

    // Start of the player's turn: cards like Throw Knife come back.
    public void ReturnCardsToHand()
    {
        foreach (var card in returnNextTurn)
        {
            if (hand.Count < maxHandSize) hand.Add(card);
            else discardPile.Add(card);
        }
        returnNextTurn.Clear();
        Changed();
    }

    public void Exhaust(CardInstance card)
    {
        hand.Remove(card); drawPile.Remove(card); discardPile.Remove(card);
        exhaustPile.Add(card);
        Changed();
    }

    // End of turn: whole hand goes to discard. Returns the cards that were discarded (for Rejection curse).
    public List<CardInstance> DiscardHand()
    {
        var discarded = new List<CardInstance>(hand);
        discardPile.AddRange(hand);
        hand.Clear();
        Changed();
        return discarded;
    }

    // Netrunner "Corrupt": add a curse for this fight only.
    public void AddTemporaryCard(CardData card)
    {
        drawPile.Add(new CardInstance(card) { temporary = true });
        Shuffle(drawPile);
        Changed();
    }

    void ClearAll()
    {
        drawPile.Clear(); hand.Clear(); discardPile.Clear(); exhaustPile.Clear(); returnNextTurn.Clear();
    }

    static void Shuffle<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    void Changed() => OnPilesChanged?.Invoke();
}
