using UnityEngine;

// Put on a room prefab (e.g. Modular_Enemy, Modular_BOSS) to give THAT room its own music.
// Empty slots fall back to the music for the room's type in MusicManager.
public class RoomMusicSettings : MonoBehaviour
{
    // The room that was spawned last (the one the player is in).
    public static RoomMusicSettings Current { get; private set; }

    [Tooltip("Plays while walking around this room. Empty = use the room type's music.")]
    public AudioClip exploreMusic;

    [Tooltip("Plays during the fight in this room. Empty = use the room type's battle music.")]
    public AudioClip battleMusic;

    [Range(0f, 1f)] public float volume = 1f;

    void OnEnable()
    {
        Current = this;
    }

    void OnDisable()
    {
        // Only clear it if no newer room has taken over already.
        if (Current == this) Current = null;
    }
}