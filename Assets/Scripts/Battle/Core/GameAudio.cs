using System.Collections.Generic;
using UnityEngine;

// Plays short sound effects from anywhere: GameAudio.Play(GameAudio.Hit);
// No scene setup needed. Clips live in Assets/Resources/Sounds/<name of the sound>/.
// A folder can hold several clips: one is picked at random each time, so it doesn't sound repetitive.
// An empty or missing folder plays the fallback sound if there is one, otherwise nothing.
public static class GameAudio
{
    public const string Hit = "Hit";               // punches, shield bash, enemy attacks
    public const string Slash = "Slash";           // knives, energy sword, micro blades
    public const string Laser = "Laser";           // pistol, shotgun and the attack drone
    public const string LaserBurst = "LaserBurst"; // SMG
    public const string Railgun = "Railgun";       // railgun
    public const string Explosion = "Explosion";   // grenades, self-destruct
    public const string Burn = "Burn";             // BURN applied or burning
    public const string Status = "Status";         // any other status, CORRODE/BLEED/POISON ticking
    // A status can have its own sound: make a folder with the status's name (e.g. Sounds/ShortCircuit/).
    // Statuses without their own folder use Status.
    public const string ShieldBlock = "ShieldBlock";
    public const string PlayerHurt = "PlayerHurt";
    public const string EnemyHurt = "EnemyHurt";
    public const string EnemyDeath = "EnemyDeath";
    public const string Door = "Door";

    // AoE and multi-hit cards hit everything in the same frame.
    // The same sound within this many seconds is skipped, so Frag plays one explosion, not three on top of each other.
    const float MinGap = 0.05f;

    static AudioSource source;
    static readonly Dictionary<string, AudioClip[]> clips = new Dictionary<string, AudioClip[]>();
    static readonly Dictionary<string, float> lastPlayed = new Dictionary<string, float>();

    public static void Play(string sound, float volume = 1f, string fallback = null)
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
        if (list.Length == 0)
        {
            if (fallback != null) Play(fallback, volume);
            return;
        }

        if (lastPlayed.TryGetValue(sound, out float last) && Time.unscaledTime - last < MinGap) return;
        lastPlayed[sound] = Time.unscaledTime;
        source.PlayOneShot(list[Random.Range(0, list.Length)], volume);
    }

    // Which sound folder an attack uses. Auto = Laser for gun cards, Hit for everything else.
    public static string For(AttackSound sound, bool isGun = false)
    {
        switch (sound)
        {
            case AttackSound.Slash: return Slash;
            case AttackSound.Laser: return Laser;
            case AttackSound.Explosion: return Explosion;
            case AttackSound.LaserBurst: return LaserBurst;
            case AttackSound.Railgun: return Railgun;
            case AttackSound.Punch: return Hit;
            default: return isGun ? Laser : Hit;
        }
    }
}
