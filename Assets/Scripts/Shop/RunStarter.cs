using UnityEngine;

// Put on any object in the LOBBY. Arriving in the Lobby starts a fresh run:
// credits reset, backpack goes back to the starting items, shop stock is re-rolled.
// (Because the run now starts here, untick "Starts New Run" on the Lobby portals.)
public class RunStarter : MonoBehaviour
{
    [Tooltip("Credits at the start of a run. Raise it to test the shop.")]
    [SerializeField] int startingCredits = 0;

    void Awake()
    {
        RunState.StartNewRun();
        RunState.credits = startingCredits;
    }
}