using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class EncounterGroup
{
    public List<EnemyData> enemies = new List<EnemyData>();
}

// Runs one floor of the dungeon, room by room, in ONE scene:
//   - makes a random map (MapGenerator)
//   - builds the current room from a random room prefab of that type (RoomLibrary,
//     filled from Assets/Prefab/Rooms/<Type>/), then puts the enemy on its Enemy Spawn
//     and one door per next room on its Door Slots.
//     No prefab at all -> builds a plain hallway in code instead.
//   - walking through a door fades to black and builds the next room (HP + backpack stay)
//   - beating the boss shows "Floor cleared" -> back to the Lobby
// Put on an empty "DungeonManager" object.
public class DungeonManager : MonoBehaviour
{
    public static DungeonManager Instance { get; private set; }

    [Header("Map")]
    [Tooltip("Rooms you go through before the boss.")]
    [SerializeField] int layers = 5;
    [SerializeField] int lanes = 3;
    [Tooltip("Lines drawn from start to boss. More = more connections between paths.")]
    [SerializeField] int paths = 4;
    [Range(0, 1)] [SerializeField] float eliteChance = 0.2f;
    [Tooltip("Rows before this (0-based) are always normal enemies.")]
    [SerializeField] int eliteFromLayer = 2;
    [Tooltip("Non-fight rooms (0 = never). Rest rooms are empty walk-through rooms until they're made.")]
    [Range(0, 1)] [SerializeField] float shopChance = 0.15f;
    [Range(0, 1)] [SerializeField] float eventChance = 0.25f;
    [Range(0, 1)] [SerializeField] float restChance = 0f;
    [Tooltip("Rows before this (0-based) never have shops / events.")]
    [SerializeField] int shopFromLayer = 1;
    [SerializeField] int eventFromLayer = 1;

    [Header("Rooms")]
    [Tooltip("Assets/Data/RoomLibrary - auto-filled from the folders in Assets/Prefab/Rooms.")]
    [SerializeField] RoomLibrary roomLibrary;

    [Header("Difficulty")]
    [Tooltip("Multiplies every enemy's HP. 1 = as set in its EnemyData.")]
    [SerializeField] float enemyHpMultiplier = 1f;
    [Tooltip("Multiplies every enemy's damage. 1 = as set in its EnemyData.")]
    [SerializeField] float enemyDamageMultiplier = 1f;
    [Tooltip("Extra HP + damage for each room deeper. 0.06 = room 5 enemies are +24%. The boss isn't scaled.")]
    [SerializeField] float scalingPerRoom = 0.06f;

    [Header("Encounters (one group is picked at random)")]
    [SerializeField] List<EncounterGroup> normalEncounters = new List<EncounterGroup>();
    [SerializeField] List<EncounterGroup> eliteEncounters = new List<EncounterGroup>();
    [SerializeField] List<EncounterGroup> bossEncounters = new List<EncounterGroup>();
    [SerializeField] float engageRange = 3f;
    [Tooltip("Most enemies in one normal/elite fight. If a group has more, that many are picked at random. The boss isn't limited.")]
    [SerializeField] int maxEnemiesPerRoom = 1;

    [Header("Scene")]
    [SerializeField] BattleManager battle;
    [SerializeField] Transform player;
    [Tooltip("Turned off when the floor is cleared (PlayerMovement, MouseLook, InventoryToggle).")]
    [SerializeField] Behaviour[] disableOnFloorCleared;

    [Header("Room look")]
    [SerializeField] Material floorMat;
    [SerializeField] Material wallMat;
    [SerializeField] Material enemyMat;
    [SerializeField] Material doorEnemyMat;
    [SerializeField] Material doorEliteMat;
    [SerializeField] Material doorBossMat;
    [SerializeField] Material doorOtherMat;          // shop / event / rest

    [Header("UI")]
    [SerializeField] CanvasGroup fadeScreen;
    [SerializeField] float fadeTime = 0.3f;
    [SerializeField] Text hudText;
    [SerializeField] Text messageText;
    [SerializeField] GameObject floorClearedPanel;
    [SerializeField] MapView mapView;
    [SerializeField] string lobbyScene = "Lobby";

    public FloorMap Map { get; private set; }
    public MapNode Current { get; private set; }
    public bool RoomCleared { get; private set; }

    // Room size
    const float HallLength = 14f, HallWidth = 4f, WallHeight = 4f;
    const float ChamberDepth = 4f, ChamberWidth = 9.4f, DoorSpacing = 3f;

    Transform roomRoot;
    bool moving, floorDone;
    float messageHideAt;
    Font font;

    void Awake()
    {
        Instance = this;
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    void OnEnable()  { if (battle) battle.OnBattleEnded += OnBattleEnded; }
    void OnDisable() { if (battle) battle.OnBattleEnded -= OnBattleEnded; }

    void Start()
    {
        Map = MapGenerator.Generate(layers, lanes, paths, eliteChance, eliteFromLayer,
                                   shopChance, eventChance, restChance, shopFromLayer, eventFromLayer);
        if (floorClearedPanel) floorClearedPanel.SetActive(false);
        if (mapView) mapView.Build(Map);
        EnterRoom(Map.start);
    }

    void OnBattleEnded(bool won)
    {
        if (won) RoomCleared = true;

        // Fight Ring elite: pay the bet-free prize instead of normal rewards
        if (won && eventFightCredits > 0)
        {
            RunState.credits += eventFightCredits;
            RunState.lastCreditsEarned = eventFightCredits;
            ShowMessage($"The crowd erupts! +{eventFightCredits} credits");
        }
        eventFightCredits = 0;
    }

    // ---------------- Fights started by events ----------------

    int eventFightCredits;

    // Underground Fight Ring: an elite fight right here, no normal rewards, +credits if you win.
    public void StartEventEliteFight(int winCredits)
    {
        StartCoroutine(EventEliteFight(winCredits));
    }

    IEnumerator EventEliteFight(int winCredits)
    {
        yield return null;                                         // let the event popup close first
        while (InBattle) yield return null;

        var enemies = PickEnemies(NodeType.Elite);
        if (enemies.Count == 0 || !player) yield break;

        Vector3 fwd = player.forward; fwd.y = 0; fwd.Normalize();
        var go = new GameObject("EventFight");
        go.transform.SetParent(roomRoot, true);
        go.transform.SetPositionAndRotation(new Vector3(player.position.x, 0, player.position.z) + fwd * 2.5f,
                                            Quaternion.LookRotation(-fwd));
        var enc = go.AddComponent<EnemyEncounter>();
        enc.Setup(enemies, 0f);                                   // range 0 = never starts by itself

        eventFightCredits = winCredits;
        RunState.noRewardsNextFight = true;
        RoomCleared = false;
        if (BattleTransition.Instance) BattleTransition.Instance.BeginBattle(enc);
    }

    bool InBattle => BattleTransition.Instance && BattleTransition.Instance.Busy;

    void Update()
    {
        // M = map (not during a fight)
        if (mapView)
        {
            if (InBattle || floorDone) { if (mapView.IsOpen) mapView.Hide(); }
            else if (Keyboard.current != null && Keyboard.current.mKey.wasPressedThisFrame) mapView.Toggle(Current);
        }

        // Boss beaten and back from the reward screen -> floor cleared
        if (!floorDone && Current != null && Current.type == NodeType.Boss && RoomCleared && !InBattle && !moving)
            ShowFloorCleared();

        if (messageText && messageText.text != "" && Time.time > messageHideAt) messageText.text = "";
        UpdateHud();
    }

    // ---------------- Moving between rooms ----------------

    // Called by DungeonDoor when the player walks through it.
    public void TryEnter(MapNode node)
    {
        if (moving || floorDone || InBattle) return;
        if (!RoomCleared) { ShowMessage("Defeat the enemy first!"); return; }
        if (!Current.next.Contains(node.id)) return;
        GameAudio.Play(GameAudio.Door);
        StartCoroutine(MoveTo(node));
    }

    IEnumerator MoveTo(MapNode node)
    {
        moving = true;
        yield return Fade(1f);
        EnterRoom(node);
        yield return new WaitForSeconds(0.15f);
        yield return Fade(0f);
        moving = false;
    }

    void EnterRoom(MapNode node)
    {
        Current = node;
        node.visited = true;
        RoomCleared = !node.IsCombat;
        ApplyDifficulty(node);

        // Nanite Leak "go around": this room gives no rewards
        RunState.noRewardsThisRoom = RunState.noRewardsNextRoom;
        RunState.noRewardsNextRoom = false;

        if (roomRoot) Destroy(roomRoot.gameObject);

        var prefab = roomLibrary ? roomLibrary.Pick(node.type) : null;
        Vector3 spawnPos = new Vector3(0, 1, 1.5f);
        Quaternion spawnRot = Quaternion.identity;

        if (prefab)
        {
            roomRoot = Instantiate(prefab.gameObject, Vector3.zero, Quaternion.identity).transform;
            roomRoot.name = $"Room_{node.type}_{node.layer + 1} ({prefab.name})";
            var room = roomRoot.GetComponent<RoomPrefab>();
            FillPrefabRoom(node, room);
            if (room.playerSpawn) { spawnPos = room.playerSpawn.position; spawnRot = room.playerSpawn.rotation; }
        }
        else
        {
            roomRoot = new GameObject($"Room_{node.type}_{node.layer + 1}").transform;
            BuildRoom(node);
        }

        TeleportPlayer(spawnPos, spawnRot);
        if (mapView) mapView.Refresh(Current);

        // Each shop room keeps its own stock for the whole run
        if (node.type == NodeType.Shop)
        {
            var shop = FindFirstObjectByType<ShopManager>(FindObjectsInactive.Include);
            if (shop) shop.SetShopKey($"Floor1_Room{node.id}");
            ShowMessage("Shop - walk up to the counter and press E");
        }
        else if (node.type == NodeType.Boss && RunState.bossIntel)
        {
            var group = Pick(NodeType.Boss);   // (only used for the message; the fight picks its own)
            if (group != null && group.enemies.Count > 0 && group.enemies[0])
            {
                var names = new List<string>();
                foreach (var m in group.enemies[0].moves) names.Add(m.moveName);
                ShowMessage("Boss intel: " + string.Join(" -> ", names));
            }
        }
        else if (node.type == NodeType.Rest)
            ShowMessage("Rest room - coming soon. Pick a door to continue.");
        // Event rooms: the room's EventRoomTrigger opens a random event popup.
    }

    // Deeper rooms = stronger enemies. Read by BattleManager when the fight starts.
    void ApplyDifficulty(MapNode node)
    {
        if (!battle) return;
        int depth = node.type == NodeType.Boss ? 0 : Mathf.Max(0, node.layer);
        float scale = 1f + scalingPerRoom * depth;
        battle.EnemyHpMultiplier = enemyHpMultiplier * scale;
        battle.EnemyDamageMultiplier = enemyDamageMultiplier * scale;
    }

    // ---------------- Room from a prefab ----------------

    void FillPrefabRoom(MapNode node, RoomPrefab room)
    {
        // Enemy on the Enemy Spawn (only in fight rooms)
        if (node.IsCombat)
        {
            var enemies = PickEnemies(node.type);
            if (enemies.Count > 0)
            {
                Transform at = room.enemySpawn ? room.enemySpawn : roomRoot;
                SpawnEncounter(enemies, at.position, at.rotation);
            }
            else RoomCleared = true;
        }

        // One door per next room, centred on the available slots. Unused slots are walled off.
        var nexts = Map.Next(node);
        var slots = room.doorSlots ?? new Transform[0];
        if (slots.Length < nexts.Count)
            Debug.LogWarning($"{room.name} has {slots.Length} door slots but needs {nexts.Count}. Add more Door Slots.", room);

        int first = Mathf.Max(0, (slots.Length - nexts.Count) / 2);
        for (int i = 0; i < slots.Length; i++)
        {
            if (!slots[i]) continue;
            int n = i - first;
            if (n >= 0 && n < nexts.Count) BuildDoor(nexts[n], slots[i].position, slots[i].rotation);
            else Box("BlockedDoor", slots[i].position + Vector3.up * 1.3f, new Vector3(1.8f, 2.6f, 0.2f), wallMat, slots[i].rotation);
        }
    }

    void TeleportPlayer(Vector3 pos, Quaternion rot)
    {
        if (!player) return;
        var cc = player.GetComponent<CharacterController>();
        if (cc) cc.enabled = false;                       // CharacterController ignores position changes while on
        player.SetPositionAndRotation(pos, Quaternion.Euler(0, rot.eulerAngles.y, 0));
        if (cc) cc.enabled = true;

        // Look straight ahead again (MouseLook re-reads its angle when re-enabled)
        foreach (var look in player.GetComponentsInChildren<MouseLook>())
        {
            if (look.transform == player) continue;
            look.transform.localRotation = Quaternion.identity;
            if (look.enabled) { look.enabled = false; look.enabled = true; }
        }
    }

    // ---------------- Building a room ----------------

    void BuildRoom(MapNode node)
    {
        float endZ = HallLength + ChamberDepth;
        float side = (ChamberWidth - HallWidth) / 2f;

        // Hallway
        Box("Floor", new Vector3(0, -0.1f, HallLength / 2), new Vector3(HallWidth, 0.2f, HallLength), floorMat);
        Box("WallLeft", new Vector3(-HallWidth / 2 - 0.1f, WallHeight / 2, HallLength / 2), new Vector3(0.2f, WallHeight, HallLength), wallMat);
        Box("WallRight", new Vector3(HallWidth / 2 + 0.1f, WallHeight / 2, HallLength / 2), new Vector3(0.2f, WallHeight, HallLength), wallMat);
        Box("WallBack", new Vector3(0, WallHeight / 2, -0.1f), new Vector3(HallWidth + 0.4f, WallHeight, 0.2f), wallMat);

        // End chamber with the doors
        Box("ChamberFloor", new Vector3(0, -0.1f, HallLength + ChamberDepth / 2), new Vector3(ChamberWidth, 0.2f, ChamberDepth), floorMat);
        Box("ChamberFrontLeft", new Vector3(-(HallWidth / 2 + side / 2), WallHeight / 2, HallLength - 0.1f), new Vector3(side, WallHeight, 0.2f), wallMat);
        Box("ChamberFrontRight", new Vector3(HallWidth / 2 + side / 2, WallHeight / 2, HallLength - 0.1f), new Vector3(side, WallHeight, 0.2f), wallMat);
        Box("ChamberLeft", new Vector3(-ChamberWidth / 2 - 0.1f, WallHeight / 2, HallLength + ChamberDepth / 2), new Vector3(0.2f, WallHeight, ChamberDepth), wallMat);
        Box("ChamberRight", new Vector3(ChamberWidth / 2 + 0.1f, WallHeight / 2, HallLength + ChamberDepth / 2), new Vector3(0.2f, WallHeight, ChamberDepth), wallMat);
        Box("ChamberEnd", new Vector3(0, WallHeight / 2, endZ + 0.1f), new Vector3(ChamberWidth + 0.4f, WallHeight, 0.2f), wallMat);

        var light = new GameObject("RoomLight").AddComponent<Light>();
        light.transform.SetParent(roomRoot, false);
        light.transform.localPosition = new Vector3(0, 3.5f, HallLength * 0.6f);
        light.type = LightType.Point;
        light.range = 22; light.intensity = 1.5f;

        // Enemy in the middle of the hallway (the player has to walk past it)
        if (node.IsCombat)
        {
            var enemies = PickEnemies(node.type);
            if (enemies.Count > 0)
                SpawnEncounter(enemies, new Vector3(0, 0, HallLength * 0.55f), Quaternion.Euler(0, 180, 0));
            else RoomCleared = true;
        }

        if (node.type == NodeType.Start)
            Label(new Vector3(0, 2.6f, 6f), "FLOOR 1\nChoose a door", Color.white, 0.05f);

        // Doors to the next rooms (none in the boss room)
        var nexts = Map.Next(node);
        for (int i = 0; i < nexts.Count; i++)
        {
            float x = (i - (nexts.Count - 1) / 2f) * DoorSpacing;
            BuildDoor(nexts[i], new Vector3(x, 0, endZ), Quaternion.identity);
        }
    }

    // A random group's enemies, cut down to maxEnemiesPerRoom (random ones) unless it's the boss.
    List<EnemyData> PickEnemies(NodeType type)
    {
        var group = Pick(type);
        var enemies = new List<EnemyData>();
        if (group == null) return enemies;
        foreach (var e in group.enemies) if (e) enemies.Add(e);

        if (type != NodeType.Boss)
            while (enemies.Count > Mathf.Max(1, maxEnemiesPerRoom))
                enemies.RemoveAt(Random.Range(0, enemies.Count));
        return enemies;
    }

    EncounterGroup Pick(NodeType type)
    {
        var list = type == NodeType.Boss ? bossEncounters : type == NodeType.Elite ? eliteEncounters : normalEncounters;
        if (list == null || list.Count == 0) list = normalEncounters;
        return list.Count > 0 ? list[Random.Range(0, list.Count)] : null;
    }

    // pos / rot in world space. rot = the way the enemy faces (towards the player).
    void SpawnEncounter(List<EnemyData> enemies, Vector3 pos, Quaternion rot)
    {
        var go = new GameObject("Encounter");
        go.transform.SetParent(roomRoot, true);
        go.transform.SetPositionAndRotation(pos, rot);

        for (int i = 0; i < enemies.Count; i++)
        {
            float x = enemies.Count == 1 ? 0 : (i - (enemies.Count - 1) / 2f) * 1.3f;
            float s = i == 0 ? 1f : 0.75f;
            if (enemies[i] && enemies[i].modelPrefab)
            {
                EnemyModel.Spawn(enemies[i], go.transform, new Vector3(x, 0, 0), s);
                continue;
            }

            // No model yet: a capsule stands in for it.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Model_" + (enemies[i] ? enemies[i].enemyName : "?");
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(x, s, 0);
            body.transform.localScale = Vector3.one * s;
            if (enemyMat) body.GetComponent<Renderer>().sharedMaterial = enemyMat;
        }

        var enc = go.AddComponent<EnemyEncounter>();
        enc.Setup(enemies, engageRange);
    }

    // pos = bottom-centre of the doorway (world). rot forward = out of the room.
    void BuildDoor(MapNode target, Vector3 pos, Quaternion rot)
    {
        Material mat = target.type == NodeType.Boss ? doorBossMat
                     : target.type == NodeType.Elite ? doorEliteMat
                     : target.type == NodeType.Enemy ? doorEnemyMat
                     : (doorOtherMat ? doorOtherMat : doorEnemyMat);
        Vector3 up = Vector3.up, back = rot * Vector3.back;
        Box("Door_" + target.type, pos + up * 1.3f + back * 0.05f, new Vector3(1.6f, 2.6f, 0.1f), mat, rot);

        var trigger = new GameObject("DoorTrigger_" + target.id);
        trigger.transform.SetParent(roomRoot, true);
        trigger.transform.SetPositionAndRotation(pos + up * 1.3f + back * 0.6f, rot);
        var col = trigger.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1.6f, 2.6f, 0.8f);
        var rb = trigger.AddComponent<Rigidbody>();
        rb.isKinematic = true; rb.useGravity = false;
        trigger.AddComponent<DungeonDoor>().Setup(target);

        Label(pos + up * 2.8f + back * 0.2f, DoorLabel(target.type), DoorColor(target.type), 0.035f, rot);
    }

    public static string DoorLabel(NodeType t)
    {
        switch (t)
        {
            case NodeType.Enemy: return "?  ENEMY";
            case NodeType.Elite: return "!  ELITE";
            case NodeType.Boss:  return "BOSS";
            case NodeType.Shop:  return "$  SHOP";
            case NodeType.Event: return "EVENT";
            case NodeType.Rest:  return "REST";
            default:             return "";
        }
    }

    static Color DoorColor(NodeType t) =>
        t == NodeType.Boss ? new Color(0.85f, 0.4f, 1f) :
        t == NodeType.Elite ? new Color(1f, 0.6f, 0.2f) : Color.white;

    // pos / rot in world space (the room root sits at the origin).
    void Box(string name, Vector3 pos, Vector3 scale, Material mat, Quaternion? rot = null)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name;
        b.transform.SetParent(roomRoot, true);
        b.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
        b.transform.localScale = scale;
        if (mat) b.GetComponent<Renderer>().sharedMaterial = mat;
    }

    void Label(Vector3 pos, string text, Color color, float size, Quaternion? rot = null)
    {
        var go = new GameObject("Label");
        go.transform.SetParent(roomRoot, true);
        go.transform.SetPositionAndRotation(pos, rot ?? Quaternion.identity);
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.text = text; tm.color = color;
        tm.fontSize = 64; tm.characterSize = size;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
    }

    // ---------------- UI ----------------

    void UpdateHud()
    {
        if (!hudText || Current == null) return;
        string where = Current.type == NodeType.Start ? "Entrance"
                     : Current.type == NodeType.Boss ? "BOSS"
                     : $"Room {Current.layer + 1} / {layers}";
        hudText.text = $"Floor 1   {where}   Credits {RunState.credits}   M = map   0 = backpack";
    }

    public void ShowMessage(string msg)
    {
        if (!messageText) return;
        messageText.text = msg;
        messageHideAt = Time.time + 2.5f;
    }

    void ShowFloorCleared()
    {
        floorDone = true;
        if (floorClearedPanel) floorClearedPanel.SetActive(true);
        foreach (var b in disableOnFloorCleared) if (b) b.enabled = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    // Floor cleared panel button.
    public void BackToLobby()
    {
        SceneManager.LoadScene(lobbyScene);
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
