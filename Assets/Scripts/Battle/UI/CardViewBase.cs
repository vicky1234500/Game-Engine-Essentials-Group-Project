using UnityEngine;

// What BattleUI needs from a card in the hand, so it works with both the 2D CardView and the 3D CardView3D.
public abstract class CardViewBase : MonoBehaviour
{
    public CardInstance Card { get; protected set; }

    public abstract void SetSelected(bool selected);
}
