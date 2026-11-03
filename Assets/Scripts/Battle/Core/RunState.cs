// Things that last for the whole run (not just one fight). Shop / events can read and change these.
public static class RunState
{
    public static CharacterData selectedCharacter;   // chosen in the Lobby (null = use the scene's defaults)
    public static int credits;
    public static int lastCreditsEarned;   // shown on the victory panel

    // Called when leaving the Lobby through the portal.
    public static void StartNewRun()
    {
        credits = 0;
        lastCreditsEarned = 0;
    }
}
