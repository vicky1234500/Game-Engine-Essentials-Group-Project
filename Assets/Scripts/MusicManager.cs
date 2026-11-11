using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Music for one room type (same types as the Room Library: Start, Enemy, Elite, Boss, Shop, Event, Rest).
[System.Serializable]
public class RoomMusic
{
    public NodeType roomType = NodeType.Enemy;

    [Tooltip("Plays while walking around the room (before / after the fight).")]
    public AudioClip exploreMusic;

    [Tooltip("Plays during the fight. Only used for Enemy, Elite and Boss rooms. Leave empty for the others.")]
    public AudioClip battleMusic;

    [Range(0f, 1f)] public float volume = 1f;
}

// Plays background music in the Dungeon scene and switches it automatically:
//   - entering a room   -> the room's own music (RoomMusicSettings on the room prefab),
//                          or else that room type's explore music
//   - a fight starts    -> the room's own battle music, or else that room type's battle music
//   - victory / defeat  -> optional jingle, then back to the explore music
// Nothing else needs to call it: it watches DungeonManager, BattleManager and BattleTransition.
// Put on an empty "MusicManager" object in Dungeon.unity.
public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Music per room type")]
    [SerializeField] List<RoomMusic> roomMusic = new List<RoomMusic>();
    [Tooltip("Used when a room type has no music set (or there's no dungeon, e.g. in the Lobby).")]
    [SerializeField] AudioClip defaultMusic;
    [Tooltip("Used in a fight when no other battle music is found.")]
    [SerializeField] AudioClip defaultBattleMusic;

    [Header("Volume and fading")]
    [Range(0f, 1f)] [SerializeField] float masterVolume = 0.6f;
    [Tooltip("Crossfade time between rooms.")]
    [SerializeField] float fadeTime = 1.2f;
    [Tooltip("Faster crossfade when a fight starts or ends.")]
    [SerializeField] float battleFadeTime = 0.4f;

    [Header("Debug")]
    [Tooltip("ON: prints to the Console what room / fight it detects every time the music changes.")]
    [SerializeField] bool debugLog = false;

    AudioSource sourceA, sourceB;
    AudioSource current;               // the source playing (or fading in) right now
    AudioClip currentClip;
    float currentVolume;               // volume of the current clip before masterVolume
    Coroutine fadeRoutine;

    bool battleEnded;                  // between Victory/Defeat and the end of the fade back to exploring
    AudioClip overrideClip;            // e.g. a cutscene or menu forcing a track

    void Awake()
    {
        Instance = this;
        sourceA = CreateSource();
        sourceB = CreateSource();
        current = sourceA;
    }

    AudioSource CreateSource()
    {
        var s = gameObject.AddComponent<AudioSource>();
        s.playOnAwake = false;
        s.loop = true;
        s.spatialBlend = 0f;           // 2D, same volume everywhere
        s.volume = 0f;
        return s;
    }

    void Update()
    {
        ChooseMusic(out AudioClip clip, out float volume, out bool loop, out bool battleChange);

        if (clip != currentClip)
        {
            if (debugLog) LogDetection(clip);
            PlayClip(clip, volume, loop, battleChange ? battleFadeTime : fadeTime);
        }
        else if (fadeRoutine == null && current.isPlaying)
        {
            current.volume = currentVolume * masterVolume;   // live master volume changes
        }
    }

    // ---------------- Which music should be playing? ----------------

    void ChooseMusic(out AudioClip clip, out float volume, out bool loop, out bool battleChange)
    {
        volume = 1f;
        loop = true;
        battleChange = false;

        if (overrideClip)
        {
            clip = overrideClip;
            return;
        }

        BattleManager battle = BattleManager.Instance;
        BattleTransition transition = BattleTransition.Instance;
        RoomMusic room = CurrentRoomMusic();

        bool won = battle && battle.State == BattleState.Victory;
        bool lost = battle && battle.State == BattleState.Defeat;
        bool busy = transition && transition.Busy;

        // Remember that the fight is over, so the fade back to exploring doesn't restart the battle music.
        if (won || lost) battleEnded = true;
        else if (!busy) battleEnded = false;

        // Fighting = the fade into battle has started, or the battle is running.
        bool fighting = !battleEnded && (busy || (battle && battle.InBattle));

        if (fighting && BattleMusic(room, battle, out AudioClip battleClip, out float battleVolume))
        {
            clip = battleClip; volume = battleVolume; battleChange = true;
        }
        else if (room != null && room.exploreMusic)
        {
            clip = room.exploreMusic; volume = room.volume; battleChange = battleEnded;
        }
        else
        {
            clip = defaultMusic;
        }
    }

    // Battle music, in this order:
    //   1. the room's own battle music (RoomMusicSettings, or the room type from DungeonManager)
    //   2. based on the enemies in the fight: Boss -> Boss music, Elite -> Elite music, Normal -> Enemy music
    //   3. Default Battle Music
    bool BattleMusic(RoomMusic room, BattleManager battle, out AudioClip clip, out float volume)
    {
        if (room != null && room.battleMusic)
        {
            clip = room.battleMusic; volume = room.volume;
            return true;
        }

        RoomMusic byEnemies = FindType(EnemyRankType(battle));
        if (byEnemies != null && byEnemies.battleMusic)
        {
            clip = byEnemies.battleMusic; volume = byEnemies.volume;
            return true;
        }

        clip = defaultBattleMusic; volume = 1f;
        return clip != null;
    }

    // The strongest enemy in the fight decides the type (any Boss -> Boss, any Elite -> Elite, otherwise Enemy).
    NodeType EnemyRankType(BattleManager battle)
    {
        NodeType type = NodeType.Enemy;
        if (!battle) return type;

        foreach (var e in battle.enemies)
        {
            if (!e || !e.Data) continue;
            if (e.Data.rank == EnemyRank.Boss) return NodeType.Boss;
            if (e.Data.rank == EnemyRank.Elite) type = NodeType.Elite;
        }
        return type;
    }

    RoomMusic FindType(NodeType type)
    {
        foreach (var entry in roomMusic)
            if (entry.roomType == type) return entry;
        return null;
    }

    void LogDetection(AudioClip clip)
    {
        DungeonManager dungeon = DungeonManager.Instance;
        string roomType = dungeon && dungeon.Current != null ? dungeon.Current.type.ToString() : "none found";
        string roomSettings = RoomMusicSettings.Current ? RoomMusicSettings.Current.name : "none";
        BattleManager battle = BattleManager.Instance;
        string battleState = battle ? battle.State.ToString() : "no BattleManager";
        Debug.Log($"[Music] room type: {roomType} | RoomMusicSettings: {roomSettings} | battle: {battleState} " +
                  $"| enemies count as: {EnemyRankType(battle)} -> playing: {(clip ? clip.name : "nothing")}");
    }

    // Music for the room the player is in:
    // the room's own RoomMusicSettings first, then the music for its room type.
    RoomMusic CurrentRoomMusic()
    {
        RoomMusic typeMusic = null;
        DungeonManager dungeon = DungeonManager.Instance;
        if (dungeon && dungeon.Current != null)
            foreach (var entry in roomMusic)
                if (entry.roomType == dungeon.Current.type) { typeMusic = entry; break; }

        RoomMusicSettings room = RoomMusicSettings.Current;
        if (!room) return typeMusic;

        // Mix them: the room's own clips win, empty slots use the room type's clips.
        merged.exploreMusic = room.exploreMusic ? room.exploreMusic : typeMusic?.exploreMusic;
        merged.battleMusic = room.battleMusic ? room.battleMusic : typeMusic?.battleMusic;
        merged.volume = room.exploreMusic || room.battleMusic ? room.volume : (typeMusic != null ? typeMusic.volume : 1f);
        return merged;
    }

    readonly RoomMusic merged = new RoomMusic();   // reused every frame, so no garbage is created

    // ---------------- Playing and crossfading ----------------

    void PlayClip(AudioClip clip, float volume, bool loop, float time)
    {
        if (fadeRoutine != null) StopCoroutine(fadeRoutine);

        AudioSource from = current;
        AudioSource to = current == sourceA ? sourceB : sourceA;
        to.Stop();                                  // in case it was still fading out from an earlier switch

        currentClip = clip;
        currentVolume = volume;

        if (clip)
        {
            to.clip = clip;
            to.loop = loop;
            to.volume = 0f;
            to.Play();
            current = to;
        }

        fadeRoutine = StartCoroutine(Crossfade(from, clip ? to : null, volume * masterVolume, time));
    }

    IEnumerator Crossfade(AudioSource from, AudioSource to, float toVolume, float time)
    {
        float fromStart = from ? from.volume : 0f;

        for (float t = 0f; t < time; t += Time.unscaledDeltaTime)
        {
            float k = t / time;
            if (from) from.volume = Mathf.Lerp(fromStart, 0f, k);
            if (to) to.volume = Mathf.Lerp(0f, toVolume, k);
            yield return null;
        }

        if (from) { from.volume = 0f; from.Stop(); }
        if (to) to.volume = toVolume;
        fadeRoutine = null;
    }

    // ---------------- Optional: for other scripts ----------------

    // Force a track (e.g. a cutscene or a special event). ClearOverride() goes back to automatic.
    public void SetOverride(AudioClip clip) => overrideClip = clip;
    public void ClearOverride() => overrideClip = null;

    // For a volume slider in a settings menu.
    public void SetMasterVolume(float value) => masterVolume = Mathf.Clamp01(value);
}