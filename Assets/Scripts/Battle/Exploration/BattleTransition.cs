using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Switches between exploring and fighting IN THE SAME SCENE, staying first person:
//   fade to black -> freeze the player, face the enemy, look straight ahead
//   -> hide the world enemy, spawn battle enemies in front of the camera (the "BattleArena")
//   -> swap exploration UI for battle UI -> fade back in -> fight
// After Victory the player presses Continue (BattleUI) -> fade -> back to exploring.
//
// Put on an empty "BattleTransition" object in the dungeon scene.
public class BattleTransition : MonoBehaviour
{
    public static BattleTransition Instance { get; private set; }

    [Header("Battle")]
    [SerializeField] BattleManager battle;

    [Header("Player")]
    [SerializeField] Transform player;                 // object with PlayerMovement + CharacterController
    [SerializeField] Transform playerCamera;           // first person camera (child of player)
    [Tooltip("Turned OFF during battle: PlayerMovement, mouse-look script, InventoryToggle...")]
    [SerializeField] Behaviour[] disableDuringBattle;
    [SerializeField] InventoryToggle inventoryToggle;  // inventory is closed when a fight starts
    [Tooltip("ON: hide + lock the cursor while walking (for mouse look). It's always unlocked in battle to click cards.")]
    [SerializeField] bool lockCursorWhileExploring = true;

    [Header("UI")]
    [SerializeField] GameObject[] explorationUI;       // e.g. Canvas > InGame ("Press 0 to open inventory")
    [SerializeField] GameObject battleUI;              // the object with BattleUI on it
    [SerializeField] CanvasGroup fadeScreen;           // full-screen black Image, on top of everything
    [SerializeField] float fadeTime = 0.25f;
    [SerializeField] float blackHoldTime = 0.15f;

    [Header("Where the enemies stand")]
    [Tooltip("Empty object with the spawn points as children (centre, left, right...). Moved in front of the player each fight.")]
    [SerializeField] Transform battleArena;
    [SerializeField] Transform[] spawnPoints;
    [SerializeField] float battleDistance = 4f;
    [SerializeField] float minDistance = 2f;            // if a wall is closer than battleDistance
    [SerializeField] LayerMask wallLayers = ~0;

    public bool Busy { get; private set; }             // fading or fighting

    EnemyEncounter current;
    Quaternion savedCameraRotation;

    void Awake()
    {
        Instance = this;
        if (fadeScreen) { fadeScreen.alpha = 0; fadeScreen.blocksRaycasts = false; }
        if (battleUI) battleUI.SetActive(false);
    }

    void Start()
    {
        if (lockCursorWhileExploring) SetCursorLocked(true);
    }

    void OnEnable()  { if (battle) battle.OnBattleEnded += HandleBattleEnded; }
    void OnDisable() { if (battle) battle.OnBattleEnded -= HandleBattleEnded; }

    // ---------------- Start ----------------

    public void BeginBattle(EnemyEncounter encounter)
    {
        if (Busy || encounter == null) return;
        StartCoroutine(BeginRoutine(encounter));
    }

    IEnumerator BeginRoutine(EnemyEncounter encounter)
    {
        Busy = true;
        current = encounter;

        if (inventoryToggle) inventoryToggle.SetOpen(false);   // doc: can't see the inventory in combat
        SetExplorationControls(false);

        yield return Fade(1f);

        // Face the enemy and look straight ahead.
        Vector3 dir = encounter.transform.position - player.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f) player.rotation = Quaternion.LookRotation(dir);
        if (playerCamera)
        {
            savedCameraRotation = playerCamera.localRotation;
            playerCamera.localRotation = Quaternion.identity;
        }

        encounter.gameObject.SetActive(false);                   // world model hides, battle model takes its place
        PlaceArena(encounter.transform.position.y);              // (after hiding, so the wall check doesn't hit the enemy)

        foreach (var ui in explorationUI) if (ui) ui.SetActive(false);
        if (battleUI) battleUI.SetActive(true);
        SetCursorLocked(false);

        battle.StartBattle(encounter.Enemies, spawnPoints);

        yield return new WaitForSeconds(blackHoldTime);
        yield return Fade(0f);
    }

    // Put the arena in front of the player, facing them, pulled closer if a wall is in the way.
    void PlaceArena(float groundY)
    {
        if (!battleArena) return;

        Vector3 forward = player.forward; forward.y = 0; forward.Normalize();
        Vector3 eye = playerCamera ? playerCamera.position : player.position + Vector3.up * 1.6f;

        float distance = battleDistance;
        if (Physics.Raycast(eye, forward, out RaycastHit hit, battleDistance + 1f, wallLayers, QueryTriggerInteraction.Ignore))
            distance = Mathf.Max(minDistance, hit.distance - 1f);

        Vector3 pos = player.position + forward * distance;
        pos.y = groundY;
        battleArena.position = pos;
        battleArena.rotation = Quaternion.LookRotation(-forward);    // enemies face the player
    }

    // ---------------- End ----------------

    void HandleBattleEnded(bool won)
    {
        // Victory: BattleUI shows the panel, player presses Continue -> ReturnToExploration().
        // Defeat:  BattleUI shows the defeat panel (Restart / Main Menu).
    }

    // Hook to the Continue button on the Victory panel (BattleUI does this).
    public void ReturnToExploration()
    {
        if (!Busy) return;
        StartCoroutine(EndRoutine());
    }

    IEnumerator EndRoutine()
    {
        yield return Fade(1f);

        bool won = battle.State == BattleState.Victory;
        battle.ClearBattle();

        if (current)
        {
            if (won) Destroy(current.gameObject);
            else { current.gameObject.SetActive(true); current.ResetEncounter(); }
        }
        current = null;

        if (battleUI) battleUI.SetActive(false);
        foreach (var ui in explorationUI) if (ui) ui.SetActive(true);
        if (playerCamera) playerCamera.localRotation = savedCameraRotation;
        SetExplorationControls(true);
        if (inventoryToggle) inventoryToggle.SetOpen(false);     // resets HUD the same way the inventory does
        if (lockCursorWhileExploring) SetCursorLocked(true);

        yield return new WaitForSeconds(blackHoldTime);
        yield return Fade(0f);
        Busy = false;
    }

    // ---------------- Helpers ----------------

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void SetExplorationControls(bool on)
    {
        foreach (var b in disableDuringBattle) if (b) b.enabled = on;
    }

    IEnumerator Fade(float target)
    {
        if (!fadeScreen) yield break;
        fadeScreen.blocksRaycasts = true;
        float start = fadeScreen.alpha;
        for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
        {
            fadeScreen.alpha = Mathf.Lerp(start, target, t / fadeTime);
            yield return null;
        }
        fadeScreen.alpha = target;
        fadeScreen.blocksRaycasts = target > 0.5f;
    }
}
