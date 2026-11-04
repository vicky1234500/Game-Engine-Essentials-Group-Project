using UnityEngine;

// Put on any object in the LOBBY. Arriving in the Lobby starts a fresh run:
// credits go back to the character's starting credits, backpack goes back to the starting items,
// shop stock is re-rolled.
// (Because the run now starts here, untick "Starts New Run" on the Lobby portals.)
public class RunStarter : MonoBehaviour
{
    [Tooltip("EXTRA credits on top of the character's starting credits. Raise it to test the shop.")]
    [SerializeField] int startingCredits = 0;

    void Awake()
    {
        RunState.StartNewRun();
        RunState.testExtraCredits = startingCredits;
        RunState.SetStartingCredits();
    }
}