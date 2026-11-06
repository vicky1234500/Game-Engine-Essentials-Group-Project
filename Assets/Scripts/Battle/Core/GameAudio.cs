using System.Collections.Generic;
using UnityEngine;

// Plays short sound effects from anywhere: GameAudio.Play(GameAudio.Hit);
// No scene setup needed. Clips live in Assets/Resources/Sounds/<name of the sound>/.
// A folder can hold several clips: one is picked at random each time, so it doesn't sound repetitive.
// An empty or missing folder just plays nothing.
public static class GameAudio
{
    public const string Hit = "Hit";
    public const string ShieldBlock = "ShieldBlock";
    public const string PlayerHurt = "PlayerHurt";
    public const string EnemyHurt = "EnemyHurt";
    public const string EnemyDeath = "EnemyDeath";
    public const string Door = "Door";

    static AudioSource source;
    static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();

    public static void Play(string sound, float volume = 1f)
    {
        if (!source)
        {
            var go = new GameObject("GameAudio");
            Object.DontDestroyOnLoad(go);      // keeps playing while scenes change
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;          // 2D: same volume wherever the player is
        }

        if (!clips.TryGetValue(sound, out var list))
        {
            list = Resources.LoadAll<AudioClip>("Sounds/" + sound);
            clips[sound] = list;
        }
        if (list.Length > 0)
            source.PlayOneShot(list[Random.Range(0, list.Length)], volume);
    }
}
