using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Menu: Tools > Roguelike > Build Battle Test Scene
// Makes test cards, items + enemies, the Enemy / Card / dropped-item prefabs, and a hallway scene
// (Assets/Scenes/BattleTest.unity) with the inventory (copied from InventoryTest), battle UI and item rewards.
// Safe to run again: it updates the test assets and rebuilds the scene.
public static class BattleTestBuilder
{
    const string CardDir = "Assets/Data/Cards";
    const string EnemyDir = "Assets/Data/Enemies";
    const string ItemDir = "Assets/Data/Items";
    const string InventoryScenePath = "Assets/Scenes/InventoryTest.unity";
    const string PrefabDir = "Assets/Prefab/Battle";
    const string ScenePath = "Assets/Scenes/BattleTest.unity";

    static Font font;
    static Material red, gray, brown, cyan, blue;

    const string LobbyPath = "Assets/Scenes/Lobby.unity";
    const string DungeonPath = "Assets/Scenes/Dungeon.unity";
    const string CharacterDir = "Assets/Data/Characters";

    [MenuItem("Tools/Roguelike/Build All (Lobby + Dungeon + Battle Test)")]
    public static void BuildAll()
    {
        if (!ConfirmOverwrite("Lobby, Dungeon and BattleTest")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Prepare();
        BuildBattleTestScene(false);
        BuildBattleTestScene(true);
        BuildLobbyScene();
        AddScenesToBuild();
        Debug.Log("Built Lobby, Dungeon and BattleTest. Press Play in the Lobby.");
    }

    [MenuItem("Tools/Roguelike/Build Battle Test Scene")]
    public static void Build()
    {
        if (!ConfirmOverwrite("BattleTest")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Prepare();
        BuildBattleTestScene(false);
        AddScenesToBuild();
    }

    [MenuItem("Tools/Roguelike/Build Dungeon Scene")]
    public static void BuildDungeon()
    {
        if (!ConfirmOverwrite("Dungeon")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Prepare();
        BuildBattleTestScene(true);
        AddScenesToBuild();
    }

    [MenuItem("Tools/Roguelike/Build Lobby Scene")]
    public static void BuildLobby()
    {
        if (!ConfirmOverwrite("Lobby")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Prepare();
        BuildLobbyScene();
        AddScenesToBuild();
    }

    // The scenes are rebuilt from scratch, so anything added to them by hand (shop, events...) is lost.
    static bool ConfirmOverwrite(string scenes)
    {
        return EditorUtility.DisplayDialog("Rebuild scenes?",
            $"This REBUILDS the {scenes} scene(s) from scratch.\n\n" +
            "Anything the team added to them by hand (Shop, events, moved objects...) will be LOST.\n\n" +
            "Commit to git first. Continue?", "Rebuild", "Cancel");
    }

    // Safe: only makes the room folders + example room and refreshes the Room Library. No scene is touched.
    [MenuItem("Tools/Roguelike/Create Example Room (safe)")]
    public static void CreateExampleRoom()
    {
        Prepare();
        BuildRoomTemplate();
        var lib = RoomLibraryUpdater.Refresh(true);
        Selection.activeObject = lib;
        Debug.Log("Example room is in Assets/Prefab/Rooms/Enemy. Drag Assets/Data/RoomLibrary into DungeonManager > Room Library in the Dungeon scene.");
    }

    static void Prepare()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        EnsureFolder("Assets/Data", "Enemies");
        EnsureFolder("Assets/Data", "Characters");
        EnsureFolder("Assets/Prefab", "Battle");
        LoadMaterials();
    }

    // Lobby first (index 0) so a build starts there.
    static void AddScenesToBuild()
    {
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(sc => sc.path == LobbyPath || sc.path == ScenePath || sc.path == DungeonPath);
        list.Insert(0, new EditorBuildSettingsScene(LobbyPath, true));
        list.Insert(1, new EditorBuildSettingsScene(DungeonPath, true));
        list.Insert(2, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // dungeon = false: the straight test hallway with 3 fixed fights (BattleTest)
    // dungeon = true:  the real floor, random map, room by room (Dungeon)
    static void BuildBattleTestScene(bool dungeon)
    {
        // New scene FIRST: opening a scene unloads assets nothing is using yet,
        // which wiped Swing Knife / Throw Knife from the deck when the cards were loaded before it.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        // Copy the inventory Canvas BEFORE loading cards/items (opening/closing scenes unloads unused assets).
        var inventoryCanvas = CopyInventoryCanvas();

        var deck = BuildCards();
        var items = BuildItems();
        var enemies = BuildEnemies();
        var cardPrefab = BuildCardPrefab();
        var enemyPrefab = BuildEnemyPrefab();
        var dropPrefab = BuildDropPrefab();
        BuildScene(deck, items, enemies, cardPrefab, enemyPrefab, dropPrefab, inventoryCanvas, dungeon);

        string path = dungeon ? DungeonPath : ScenePath;
        EditorSceneManager.SaveScene(scene, path);
        AssetDatabase.SaveAssets();
        Debug.Log("Scene built: " + path);
    }

    // =====================================================================
    // Cards
    // =====================================================================

    static List<CardData> BuildCards()
    {
        var swing = AssetDatabase.LoadAssetAtPath<CardData>(CardDir + "/SwingKnife.asset");
        var thrw  = AssetDatabase.LoadAssetAtPath<CardData>(CardDir + "/ThrowKnife.asset");
        var stab  = AssetDatabase.LoadAssetAtPath<CardData>(CardDir + "/DeepStab.asset");

        // Deep Stab: 12 damage + BLEED (old card only had `value`, so give it real effects)
        if (stab && stab.effects.Count == 0)
        {
            stab.effects.Add(Dmg(12));
            stab.effects.Add(Status(StatusType.Bleed, 2, 3, TargetType.SingleEnemy));
            stab.description = "Deal 12 damage. Apply 2 BLEED for 3 turns.";
            EditorUtility.SetDirty(stab);
        }

        var patch = Card("PatchUp", "Patch Up", CardType.Heal, 1, "Restore 4 HP.", Heal(4));
        var raise = Card("RaiseShield", "Raise Shield", CardType.Defense, 1, "Gain 9 SHIELD.", Shield(9));
        var flash = Card("Flashbang", "Flashbang", CardType.Skill, 1, "Apply GLITCHED for 2 turns (enemy deals 25% less).",
                         Status(StatusType.Glitched, 1, 2, TargetType.SingleEnemy));

        var deck = new List<CardData>();
        void Add(CardData c, int n)
        {
            if (!c) { Debug.LogWarning("BattleTestBuilder: a card asset is missing, skipped."); return; }
            for (int i = 0; i < n; i++) deck.Add(c);
        }
        Add(swing, 3); Add(thrw, 1); Add(stab, 1);
        Add(patch, 3); Add(raise, 2); Add(flash, 1);
        return deck;
    }

    static CardData Card(string file, string name, CardType type, int mana, string desc, params CardEffect[] effects)
    {
        var c = LoadOrCreate<CardData>(CardDir + "/" + file + ".asset");
        c.cardName = name; c.type = type; c.manaCost = mana; c.description = desc;
        c.value = effects.Length > 0 ? effects[0].amount : 0;
        c.effects = new List<CardEffect>(effects);
        EditorUtility.SetDirty(c);
        return c;
    }

    // =====================================================================
    // Items: start with Knife + Bandage, rewards = 3 weapons, 3 heal, 3 shield (from the doc)
    // =====================================================================

    // rewards = items that work in battle (go into the ItemDatabase for rewards + shops)
    // inactive = outside-combat items whose systems (chests, rest rooms...) don't exist yet
    struct Items { public ItemData knife, bandage; public List<ItemData> rewards, inactive; }

    static readonly Color WeaponTint = new Color(0.83f, 0.14f, 0.23f);
    static readonly Color HealTint   = new Color(0.25f, 0.7f, 0.35f);
    static readonly Color ShieldTint = new Color(0.45f, 0.3f, 0.8f);

    static Items BuildItems()
    {
        var it = new Items { rewards = new List<ItemData>(), inactive = new List<ItemData>() };
        var patch = AssetDatabase.LoadAssetAtPath<CardData>(CardDir + "/PatchUp.asset");
        var raise = AssetDatabase.LoadAssetAtPath<CardData>(CardDir + "/RaiseShield.asset");
        var flash = AssetDatabase.LoadAssetAtPath<CardData>(CardDir + "/Flashbang.asset");

        // ---- Starting items
        // Nanite Knife (doc) - keeps the existing Knife.asset so its icon and the Knife world prefab still work
        var slice = Card("Slice", "Slice", CardType.Attack, 1, "Deal 3 damage. Apply 2 CORRODE.",
                         Dmg(3), Status(StatusType.Corrode, 2, 1, TargetType.SingleEnemy));
        var throwKnife = Card("ThrowKnife", "Throw Knife", CardType.Attack, 1, "Deal 6 damage. Returns to your hand next turn.", Dmg(6));
        throwKnife.returnToHandNextTurn = true;
        var dissolve = Card("Dissolve", "Dissolve", CardType.Attack, 2, "Deal damage equal to 3x the enemy's CORRODE.",
                            new CardEffect { type = EffectType.DamagePerStatus, target = TargetType.SingleEnemy, status = StatusType.Corrode, amount = 3, hits = 1 });
        it.knife = Item("Knife", "Nanite Knife", 2, 1, WeaponTint, null, E(slice, 2), E(throwKnife, 1), E(dissolve, 1));
        var purge = Card("Purge", "Purge", CardType.Skill, 0, "Remove CORRODE, BURN, GLITCHED and HACKED.", Fx(EffectType.RemoveStatuses, TargetType.Self, 0));
        it.bandage = Item("Bandage", "Bandage", 1, 1, HealTint, null, E(patch, 2), E(purge, 1));

        // ---- Weapons
        var shoot = Gun(Card("Shoot", "Shoot", CardType.Attack, 1, "Deal 5 damage.", Dmg(5)));
        var dshoot = Gun(Card("DoubleShoot", "Double Shoot", CardType.Attack, 2, "Deal 5 damage twice.", Dmg(5, 2)));
        var whip = Gun(Card("PistolWhip", "Pistol Whip", CardType.Attack, 1, "Deal 4 damage. Apply GLITCHED for 2 turns.",
                            Dmg(4), Status(StatusType.Glitched, 1, 2, TargetType.SingleEnemy)));
        it.rewards.Add(Item("Pistol", "Pistol", 2, 1, WeaponTint, null, E(shoot, 2), E(dshoot, 1), E(whip, 1)));

        var zap = Card("Zap", "Zap", CardType.Attack, 1, "Deal 6 damage. Apply OVERLOADED for 2 turns (+25% damage taken).",
                       Dmg(6), Status(StatusType.Overloaded, 1, 2, TargetType.SingleEnemy));
        var discharge = Card("FullDischarge", "Full Discharge", CardType.Attack, 3, "Deal 22 damage.", Dmg(22));
        it.rewards.Add(Item("ShockBaton", "Shock Baton", 3, 1, WeaponTint, null, E(zap, 2), E(discharge, 1)));

        var frag = Card("Frag", "Frag", CardType.Attack, 2, "Deal 9 damage to ALL enemies.", DmgAll(9));
        var incendiary = Card("Incendiary", "Incendiary", CardType.Attack, 2, "Deal 5 damage to ALL enemies. Apply 3 BURN.",
                              DmgAll(5), Status(StatusType.Burn, 3, 3, TargetType.AllEnemies));
        var emp = Card("EMP", "EMP", CardType.Skill, 1, "Apply SHORT-CIRCUIT (skips its next action, not bosses) and remove all SHIELD.",
                       Status(StatusType.ShortCircuit, 1, 2, TargetType.SingleEnemy), Fx(EffectType.RemoveShield, TargetType.SingleEnemy, 0));
        it.rewards.Add(Item("GrenadeBelt", "Grenade Belt", 2, 1, WeaponTint, null, E(frag, 1), E(incendiary, 1), E(flash, 1), E(emp, 1)));

        // ---- Healing
        var adrenaline = Card("AdrenalineShot", "Adrenaline Shot", CardType.Heal, 1,
                              "Restore 10 HP, +1 MANA, draw 2. Become OVERCLOCKED. Exhaust.",
                              Heal(10), Fx(EffectType.GainMana, TargetType.Self, 1), Fx(EffectType.Draw, TargetType.Self, 2),
                              Status(StatusType.Overclocked, 1, 2, TargetType.Self));
        adrenaline.exhaust = true;
        it.rewards.Add(Item("StimPack", "Stim Pack", 1, 1, HealTint, null, E(adrenaline, 1)));

        var nanobots = Card("Nanobots", "Nanobots", CardType.Heal, 1, "Restore 3 HP at the end of each turn for 3 turns.",
                            Status(StatusType.Regen, 3, 3, TargetType.Self));
        var repair = Card("Repair", "Repair", CardType.Heal, 2, "Restore 12 HP. Remove CORRODE, BURN, GLITCHED and HACKED.",
                          Heal(12), Fx(EffectType.RemoveStatuses, TargetType.Self, 0));
        it.rewards.Add(Item("NanoInjector", "Nano-Injector", 2, 1, HealTint, null, E(nanobots, 1), E(repair, 1)));

        var medBeam = Card("MedBeam", "Med Beam", CardType.Heal, 1, "Restore 8 HP.", Heal(8));
        var fullRestore = Card("FullRestore", "Full Restore", CardType.Heal, 3, "Restore 40 HP. Exhaust.", Heal(40));
        fullRestore.exhaust = true;
        it.rewards.Add(Item("MedPod", "Med Pod", 2, 2, HealTint, null, E(medBeam, 2), E(fullRestore, 1)));

        // ---- Shield
        var plate = Card("Plate", "Plate", CardType.Defense, 0, "Gain 3 SHIELD.", Shield(3));
        it.rewards.Add(Item("NanoPlating", "Nano-Plating", 2, 1, ShieldTint, null, E(plate, 3)));

        var harden = Card("Harden", "Harden", CardType.Defense, 1, "Gain 6 SHIELD.", Shield(6));
        it.rewards.Add(Item("KineticVest", "Kinetic Vest", 2, 1, ShieldTint,
                            new[] { new ItemPassive { type = PassiveType.BonusDF, amount = 10 } }, E(harden, 2)));

        var bash = Card("ShieldBash", "Shield Bash", CardType.Defense, 2, "Gain 12 SHIELD. Deal 8 damage. Apply GLITCHED for 2 turns.",
                        Shield(12), Dmg(8), Status(StatusType.Glitched, 1, 2, TargetType.SingleEnemy));
        var bulwark = Card("Bulwark", "Bulwark", CardType.Defense, 3, "Gain 35 SHIELD.", Shield(35));
        it.rewards.Add(Item("RiotShield", "Riot Shield", 2, 2, ShieldTint, null, E(raise, 2), E(bash, 1), E(bulwark, 1)));

        BuildMoreItems(it);
        if (!it.knife) Debug.LogWarning("BattleTestBuilder: Assets/Data/Items/Knife.asset not found.");
        return it;
    }

    // =====================================================================
    // The rest of the doc's items (ATTACK / DEFENSE / HEALING / SUPPORT / OUTSIDE COMBAT + "Deciding")
    // =====================================================================

    static void BuildMoreItems(Items it)
    {
        // ---- Curse cards (doc CURSE CARDS)
        var jammed = Card("Jammed", "Jammed", CardType.Curse, 1, "CURSE. Does nothing. Pay 1 MANA to exhaust it.");
        jammed.exhaust = true;
        var systemError = Curse("SystemError", "System Error", "CURSE. Unplayable. When drawn, lose 1 MANA this turn.", CurseTrigger.OnDrawLoseMana, 1);
        var rejection = Curse("Rejection", "Rejection", "CURSE. Unplayable. If still in hand at end of turn, lose 4 HP.", CurseTrigger.EndOfTurnInHandLoseHP, 4);
        var staticCurse = Curse("Static", "Static", "CURSE. Unplayable. Takes up a hand slot.", CurseTrigger.None, 0);
        var tracker = Curse("Tracker", "Tracker", "CURSE. Unplayable. When drawn, all enemies gain 5 SHIELD.", CurseTrigger.OnDrawEnemiesGainShield, 5);
        var flash = AssetDatabase.LoadAssetAtPath<CardData>(CardDir + "/Flashbang.asset");

        // ---- ATTACK ITEMS
        var flick = Card("Flick", "Flick", CardType.Attack, 0, "Deal 2 damage. Apply 1 CORRODE.",
                         Dmg(2), Status(StatusType.Corrode, 1, 1, TargetType.SingleEnemy));
        it.rewards.Add(Item("MicroBlades", "Micro-Blades", 1, 1, WeaponTint, null, E(flick, 3)));

        var eslash = Card("EnergySlash", "Slash", CardType.Attack, 1, "Deal 7 damage.", Dmg(7));
        var cuts = Card("ThousandCuts", "Thousand Cuts", CardType.Attack, 3, "Deal 3 damage for every attack card played this fight.",
                        Fx(EffectType.DamagePerAttackPlayed, TargetType.SingleEnemy, 3));
        it.rewards.Add(Item("EnergySword", "Energy Sword", 3, 1, WeaponTint, null, E(eslash, 2), E(cuts, 1)));

        var spray = Gun(Card("Spray", "Spray", CardType.Attack, 1, "Deal 4 damage three times.", Dmg(4, 3)));
        var hackRounds = Gun(Card("HackRounds", "Hack Rounds", CardType.Attack, 1, "Deal 4 damage three times. Apply HACKED.",
                                  Dmg(4, 3), Status(StatusType.Hacked, 1, 2, TargetType.SingleEnemy)));
        it.rewards.Add(Item("OverclockedSMG", "Overclocked SMG", 3, 1, WeaponTint, null, E(spray, 2), E(hackRounds, 1), E(jammed, 1)));

        var deploy = Card("Deploy", "Deploy", CardType.Attack, 1, "Drone deals 4 damage to a random enemy at the end of each turn for 3 turns.",
                          Status(StatusType.DroneStrike, 4, 3, TargetType.Self));
        var droneLaser = Card("DroneLaser", "Laser", CardType.Attack, 1, "Deal 5 damage. Apply 2 BURN.",
                              Dmg(5), Status(StatusType.Burn, 2, 3, TargetType.SingleEnemy));
        var selfDestruct = Card("SelfDestruct", "Self-Destruct", CardType.Attack, 2, "Deal 18 damage.", Dmg(18));
        it.rewards.Add(Item("AttackDrone", "Attack Drone", 3, 1, WeaponTint, null, E(deploy, 1), E(droneLaser, 1), E(selfDestruct, 1)));

        var blast = Gun(Card("Blast", "Blast", CardType.Attack, 2, "Deal 14 damage to ALL enemies.", DmgAll(14)));
        var pointBlank = Gun(Card("PointBlank", "Point Blank", CardType.Attack, 2, "Deal 18 damage.", Dmg(18)));
        var pump = Card("Pump", "Pump", CardType.Skill, 0, "Draw 2 cards.", Fx(EffectType.Draw, TargetType.Self, 2));
        it.rewards.Add(Item("Shotgun", "Shotgun", 2, 2, WeaponTint, null, E(blast, 1), E(pointBlank, 1), E(pump, 1)));

        var railShot = Gun(Card("RailShot", "Rail Shot", CardType.Attack, 3, "Deal 35 damage. Ignores SHIELD.",
                                new CardEffect { type = EffectType.Damage, target = TargetType.SingleEnemy, amount = 35, hits = 1, ignoresShield = true }));
        it.rewards.Add(Item("Railgun", "Railgun", 3, 2, WeaponTint, null, E(railShot, 1)));

        // ---- DEFENSE ITEMS
        var decoy = Card("PerfectDecoy", "Perfect Decoy", CardType.Defense, 2, "The enemy's next attack misses completely.",
                         Status(StatusType.Decoy, 1, 2, TargetType.Self));
        it.rewards.Add(Item("HoloDecoy", "Holo Decoy", 1, 1, ShieldTint, null, E(decoy, 1)));

        var fortress = Card("Fortress", "Fortress", CardType.Defense, 2, "Gain 20 SHIELD.", Shield(20));
        it.rewards.Add(Item("OverclockedExoSuit", "Overclocked Exo-Suit", 3, 2, ShieldTint,
                            new[] { new ItemPassive { type = PassiveType.BonusDF, amount = 30 } }, E(fortress, 2), E(systemError, 1)));

        // ---- HEALING ITEMS
        it.rewards.Add(Item("BlackMarketImplant", "Black-Market Implant", 2, 1, HealTint,
                            new[] { new ItemPassive { type = PassiveType.HealPerTurn, amount = 5 } }, E(rejection, 2)));

        // Med Pod (made earlier): add "Outside combat: +15 HP after each battle"
        var medPod = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/MedPod.asset");
        if (medPod && !medPod.passives.Exists(p => p.type == PassiveType.HealAfterBattle))
        {
            medPod.passives.Add(new ItemPassive { type = PassiveType.HealAfterBattle, amount = 15 });
            EditorUtility.SetDirty(medPod);
        }

        // ---- SUPPORT / UTILITY ITEMS
        var smoke = Card("SmokeBomb", "Smoke Bomb", CardType.Skill, 1, "Become CLOAKED for 2 turns (attacks have 50% to miss).",
                         Status(StatusType.Cloaked, 1, 2, TargetType.Self));
        it.rewards.Add(Item("SmokeGrenades", "Smoke Grenades", 1, 1, ShieldTint, null, E(smoke, 1)));

        var overclock = Card("Overclock", "Overclock", CardType.Skill, 1, "Become OVERCLOCKED: +3 attack damage, lose 2 HP each turn. 2 turns.",
                             Status(StatusType.Overclocked, 1, 2, TargetType.Self));
        it.rewards.Add(Item("NeuralBooster", "Neural Booster", 1, 1, SupportTint,
                            new[] { new ItemPassive { type = PassiveType.BonusManaPerTurn, amount = 1 } }, E(overclock, 1), E(staticCurse, 2)));

        var quickReload = Card("QuickReload", "Quick Reload", CardType.Skill, 0, "Draw 1 card.", Fx(EffectType.Draw, TargetType.Self, 1));
        it.rewards.Add(Item("AmmoBelt", "Ammo Belt", 2, 1, SupportTint,
                            new[] { new ItemPassive { type = PassiveType.GunDamage, amount = 2 } }, E(quickReload, 2)));

        var hack = Card("Hack", "Hack", CardType.Skill, 1, "Apply HACKED: its next action hits a random target.",
                        Status(StatusType.Hacked, 1, 2, TargetType.SingleEnemy));
        var glitchSpike = Card("GlitchSpike", "Glitch Spike", CardType.Skill, 1, "Apply GLITCHED for 2 turns.",
                               Status(StatusType.Glitched, 1, 2, TargetType.SingleEnemy));
        var bootUp = Card("BootUp", "Boot Up", CardType.Skill, 0, "Draw 2 cards.", Fx(EffectType.Draw, TargetType.Self, 2));
        var crash = Card("SystemCrash", "System Crash", CardType.Skill, 2, "Apply SHORT-CIRCUIT (not bosses).",
                         Status(StatusType.ShortCircuit, 1, 2, TargetType.SingleEnemy));
        it.rewards.Add(Item("Cyberdeck", "Cyberdeck", 3, 1, SupportTint, null, E(hack, 1), E(glitchSpike, 1), E(bootUp, 1), E(crash, 1)));

        var overrideCard = Card("Override", "Override", CardType.Skill, 3, "Take an extra turn. Exhaust.", Fx(EffectType.ExtraTurn, TargetType.Self, 0));
        overrideCard.exhaust = true;
        it.rewards.Add(Item("TacticalAICore", "Tactical AI Core", 2, 2, SupportTint,
                            new[] { new ItemPassive { type = PassiveType.ExtraDrawFirstTurn, amount = 2 } }, E(overrideCard, 1)));

        // ---- From "Deciding"
        var uplink = Item("MapUplink", "Map Uplink", 1, 1, SupportTint,
                          new[] { new ItemPassive { type = PassiveType.RevealMap, amount = 1 } });
        uplink.description = "Shows each floor's full layout on the map (M).";
        it.rewards.Add(uplink);

        var chip = Item("StolenCreditChip", "Stolen Credit Chip", 1, 1, SupportTint,
                        new[] { new ItemPassive { type = PassiveType.CreditBonusPercent, amount = 50 } }, E(tracker, 1));
        chip.description = "+50% money from all sources.";
        it.rewards.Add(chip);

        var spoofer = AssetDatabase.LoadAssetAtPath<ItemData>(ItemDir + "/ShopSpoofer.asset");   // made by the team
        if (spoofer) it.rewards.Add(spoofer);

        // ---- Outside combat: made, but their systems don't exist yet -> not in rewards / shops
        var lockPick = Item("LockPick", "Lock Pick", 1, 1, OutsideTint, null);
        lockPick.description = "Opens locked chests and doors, 3 uses. (Chests not made yet.)";
        it.inactive.Add(lockPick);
        var hook = Item("GrapplingHook", "Grappling Hook", 2, 1, OutsideTint, null);
        hook.description = "Skip one room per floor, or reach hidden rooms. (Not made yet.)";
        it.inactive.Add(hook);
        var bench = Item("PortableWorkbench", "Portable Workbench", 2, 2, OutsideTint, null);
        bench.description = "At rest rooms, upgrade one item: its cards get +3 DAMAGE or +3 SHIELD. (Rest rooms not made yet.)";
        it.inactive.Add(bench);
        var repairKit = Item("FieldRepairKit", "Field Repair Kit", 2, 1, OutsideTint,
                             new[] { new ItemPassive { type = PassiveType.RestHealBonusPercent, amount = 30 } });
        repairKit.description = "Rest rooms heal 30% more. Once per floor, +20 HP anywhere. (Rest rooms not made yet.)";
        it.inactive.Add(repairKit);

        foreach (var i in it.rewards) if (i) EditorUtility.SetDirty(i);
        foreach (var i in it.inactive) if (i) EditorUtility.SetDirty(i);
    }

    static readonly Color SupportTint = new Color(0.2f, 0.65f, 0.75f);
    static readonly Color OutsideTint = new Color(0.6f, 0.55f, 0.35f);

    static CardData Curse(string file, string name, string desc, CurseTrigger trigger, int amount)
    {
        var c = Card(file, name, CardType.Curse, 0, desc);
        c.unplayable = true;
        c.curseTrigger = trigger;
        c.curseAmount = amount;
        EditorUtility.SetDirty(c);
        return c;
    }

    // Resources/ItemDatabase: every working item (rewards + shops read it at runtime).
    static ItemDatabase BuildItemDatabase(Items it)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var db = LoadOrCreate<ItemDatabase>("Assets/Resources/ItemDatabase.asset");
        db.items = new List<ItemData>();
        foreach (var i in it.rewards) if (i && !db.items.Contains(i)) db.items.Add(i);
        EditorUtility.SetDirty(db);
        return db;
    }

    // Safe: only creates / updates card + item assets and the ItemDatabase. No scene is touched.
    [MenuItem("Tools/Roguelike/Create or Update All Items (safe)")]
    public static void CreateAllItems()
    {
        Prepare();
        BuildCards();
        var items = BuildItems();
        var db = BuildItemDatabase(items);
        AssetDatabase.SaveAssets();
        Selection.activeObject = db;
        Debug.Log($"Items updated: {db.items.Count} in rewards/shops (Resources/ItemDatabase), {items.inactive.Count} outside-combat items made but not used yet.");
    }

    static ItemData Item(string file, string name, int w, int h, Color tint, ItemPassive[] passives, params CardEntry[] cards)
    {
        string path = ItemDir + "/" + file + ".asset";
        bool isNew = !AssetDatabase.LoadAssetAtPath<ItemData>(path);
        var item = LoadOrCreate<ItemData>(path);
        if (isNew) item.price = 30 + 20 * w * h;          // new items only, so the team's prices are kept
        item.itemName = name;
        item.size = new Vector2Int(w, h);
        item.tint = tint;
        item.cards = new List<CardEntry>(cards);
        item.passives = passives == null ? new List<ItemPassive>() : new List<ItemPassive>(passives);
        EditorUtility.SetDirty(item);
        return item;
    }

    static CardEntry E(CardData card, int count) => new CardEntry { card = card, count = count };

    static CardData Gun(CardData c)
    {
        c.isGun = true;           // Ammo Belt passive adds damage to these
        EditorUtility.SetDirty(c);
        return c;
    }

    // =====================================================================
    // Enemies (from the doc). Moves are used in order and loop.
    // =====================================================================

    // Balance pass: normal 34-42 HP / 6-9 dmg, elites 60-65, boss = Warden Mech (doc) 150 HP.
    // DungeonManager scales them up a little each room deeper.
    struct Enemies { public EnemyData rogue, drone, enforcer, netrunner, warden; }

    static Enemies BuildEnemies()
    {
        var e = new Enemies();

        e.rogue = Enemy("Rogue", "Rogue", EnemyRank.Normal, 42, 12, 20,
            Move("Slash", IntentType.Attack, Dmg(6)),
            Move("Shoot Gun", IntentType.Attack, Dmg(9)),
            Move("Smoke Bomb", IntentType.Defend, Status(StatusType.Cloaked, 1, 2, TargetType.Self)));

        e.drone = Enemy("SecurityDrone", "Security Drone", EnemyRank.Normal, 34, 10, 16,
            Move("Laser", IntentType.Attack, Dmg(7)),
            Move("Self-Repair", IntentType.Heal, Heal(8)),
            Move("Laser", IntentType.Attack, Dmg(7)));

        e.enforcer = Enemy("StreetEnforcer", "Street Enforcer", EnemyRank.Elite, 65, 25, 35,
            Move("Punch", IntentType.Attack, Dmg(9)),
            Move("Wind Up", IntentType.Defend, Shield(12)),
            Move("Smash", IntentType.Attack, Dmg(16), Status(StatusType.Glitched, 1, 2, TargetType.SingleEnemy)));

        e.netrunner = Enemy("Netrunner", "Netrunner", EnemyRank.Elite, 60, 25, 35,
            Move("Data Spike", IntentType.Attack, Dmg(8)),
            Move("Firewall", IntentType.Defend, Shield(12)),
            Move("Glitch Burst", IntentType.Attack, Dmg(10), Status(StatusType.Glitched, 1, 2, TargetType.SingleEnemy)));

        // Doc: Minigun 3x4, Charging -> Missile Barrage 20 + BURN 3, Repair Protocol +15.
        // (The "summon 2 drones at 50%" part needs smarter AI than the simple move loop, left out for now.)
        e.warden = Enemy("WardenMech", "Biomech Warlord", EnemyRank.Boss, 150, 60, 80,
            Move("Minigun", IntentType.Attack, Dmg(3, 4)),
            Move("Charging", IntentType.Special),
            Move("Missile Barrage", IntentType.Attack, Dmg(20), Status(StatusType.Burn, 3, 3, TargetType.SingleEnemy)),
            Move("Repair Protocol", IntentType.Heal, Heal(15)));

        return e;
    }

    static EnemyData Enemy(string file, string name, EnemyRank rank, int hp, int minC, int maxC, params EnemyMove[] moves)
    {
        var d = LoadOrCreate<EnemyData>(EnemyDir + "/" + file + ".asset");
        d.enemyName = name; d.rank = rank; d.maxHP = hp; d.minCredits = minC; d.maxCredits = maxC;
        d.moves = new List<EnemyMove>(moves);
        EditorUtility.SetDirty(d);
        return d;
    }

    static EnemyMove Move(string name, IntentType intent, params CardEffect[] effects) =>
        new EnemyMove { moveName = name, intent = intent, effects = new List<CardEffect>(effects) };

    // Effect helpers. For enemies, "SingleEnemy" means the player.
    static CardEffect Dmg(int a, int hits = 1) => new CardEffect { type = EffectType.Damage, target = TargetType.SingleEnemy, amount = a, hits = hits };
    static CardEffect DmgAll(int a) => new CardEffect { type = EffectType.Damage, target = TargetType.AllEnemies, amount = a, hits = 1 };
    static CardEffect Fx(EffectType type, TargetType target, int amount) => new CardEffect { type = type, target = target, amount = amount, hits = 1 };
    static CardEffect Shield(int a) => new CardEffect { type = EffectType.Shield, target = TargetType.Self, amount = a, hits = 1 };
    static CardEffect Heal(int a) => new CardEffect { type = EffectType.Heal, target = TargetType.Self, amount = a, hits = 1 };
    static CardEffect Status(StatusType s, int amount, int turns, TargetType t) =>
        new CardEffect { type = EffectType.ApplyStatus, target = t, status = s, amount = amount, duration = turns, hits = 1 };

    // =====================================================================
    // Prefabs
    // =====================================================================

    static CardView BuildCardPrefab()
    {
        var root = new GameObject("CardView", typeof(RectTransform));
        root.layer = 5;
        ((RectTransform)root.transform).sizeDelta = new Vector2(160, 220);
        var bg = root.AddComponent<Image>();
        var le = root.AddComponent<LayoutElement>();
        le.preferredWidth = 160; le.preferredHeight = 220;

        var selected = Img("SelectedBar", root.transform, new Color(1f, 0.85f, 0.2f));
        Place(selected.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(0, 10));

        var cost = Txt("Cost", root.transform, "1", 34, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(cost.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(6, -4), new Vector2(40, 44));

        var nameT = Txt("Name", root.transform, "Card", 20, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(nameT.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -48), new Vector2(-12, 40));

        var desc = Txt("Description", root.transform, "Deal 5 damage.", 17, TextAnchor.UpperCenter, Color.white);
        Place(desc.rectTransform, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(-20, -110));

        var view = root.AddComponent<CardView>();
        var so = new SerializedObject(view);
        so.FindProperty("background").objectReferenceValue = bg;
        so.FindProperty("nameText").objectReferenceValue = nameT;
        so.FindProperty("costText").objectReferenceValue = cost;
        so.FindProperty("descriptionText").objectReferenceValue = desc;
        so.FindProperty("selectedOutline").objectReferenceValue = selected.gameObject;
        so.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/CardView.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<CardView>();
    }

    static EnemyCombatant BuildEnemyPrefab()
    {
        var root = new GameObject("BattleEnemy");
        var col = root.AddComponent<CapsuleCollider>();     // for clicking
        col.center = new Vector3(0, 1, 0); col.height = 2; col.radius = 0.5f;

        var body = Capsule("PlaceholderModel", root.transform, new Vector3(0, 1, 0), Vector3.one);

        var enemy = root.AddComponent<EnemyCombatant>();
        root.AddComponent<EnemyClickTarget>();
        var eso = new SerializedObject(enemy);
        eso.FindProperty("placeholderModel").objectReferenceValue = body;
        eso.ApplyModifiedPropertiesWithoutUndo();

        // World-space HUD above the head
        var hudGo = new GameObject("HUD", typeof(RectTransform), typeof(Canvas));
        hudGo.transform.SetParent(root.transform, false);
        hudGo.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
        var hudRt = (RectTransform)hudGo.transform;
        hudRt.sizeDelta = new Vector2(240, 150);
        hudRt.localPosition = new Vector3(0, 2.5f, 0);
        hudRt.localScale = Vector3.one * 0.005f;          // 240px = 1.2 m wide, fits side by side

        var intent = Txt("Intent", hudGo.transform, "Attack 5", 30, TextAnchor.MiddleCenter, new Color(1f, 0.8f, 0.3f), FontStyle.Bold);
        Place(intent.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, 0), new Vector2(0, 36));
        var nameT = Txt("Name", hudGo.transform, "Enemy", 26, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(nameT.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -36), new Vector2(0, 32));

        var bar = HpBar(hudGo.transform, out var fill, out var hpText);
        Place(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(0, 30));

        var shieldGroup = Img("Shield", hudGo.transform, new Color(0.3f, 0.5f, 1f));
        Place(shieldGroup.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(1, 1), new Vector2(-4, -70), new Vector2(50, 30));
        var shieldText = Txt("Value", shieldGroup.transform, "0", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Stretch(shieldText.rectTransform);

        var status = Txt("Status", hudGo.transform, "", 20, TextAnchor.UpperCenter, new Color(0.8f, 1f, 0.8f));
        Place(status.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -104), new Vector2(0, 40));

        var hud = hudGo.AddComponent<CombatantHUD>();
        WireHUD(hud, null, nameT, fill, hpText, shieldGroup.gameObject, shieldText, status, intent, true);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/BattleEnemy.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<EnemyCombatant>();
    }

    // =====================================================================
    // Scene
    // =====================================================================

    static void BuildScene(List<CardData> deck, Items items, Enemies en, CardView cardPrefab, EnemyCombatant enemyPrefab,
                           WorldItemPickup dropPrefab, GameObject inventoryCanvas, bool dungeon)
    {
        // Light
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.2f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Hallway (4 wide, 44 long, along +Z) - the Dungeon builds its rooms at runtime instead
        if (!dungeon)
        {
            var hall = new GameObject("Hallway").transform;
            Box("Floor", hall, new Vector3(0, -0.1f, 20), new Vector3(4, 0.2f, 44), brown);
            Box("WallLeft", hall, new Vector3(-2.1f, 2, 20), new Vector3(0.2f, 4, 44), gray);
            Box("WallRight", hall, new Vector3(2.1f, 2, 20), new Vector3(0.2f, 4, 44), gray);
            Box("WallBack", hall, new Vector3(0, 2, -2.1f), new Vector3(4.4f, 4, 0.2f), gray);
            Box("WallEnd", hall, new Vector3(0, 2, 42.1f), new Vector3(4.4f, 4, 0.2f), gray);
        }

        // Player (first person)
        var player = new GameObject("Player") { tag = "Player" };
        player.transform.position = new Vector3(0, 1, dungeon ? 1.5f : 0);
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2; cc.radius = 0.4f; cc.center = Vector3.zero;
        var movement = player.AddComponent<PlayerMovement>();
        var pc = player.AddComponent<PlayerCombatant>();
        var pso = new SerializedObject(pc);
        pso.FindProperty("maxHP").intValue = 135;      // doc: Character 1 HP 135 DF 25
        pso.FindProperty("baseDefense").intValue = 25;
        pso.ApplyModifiedPropertiesWithoutUndo();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0, 0.7f, 0);
        var cam = camGo.AddComponent<Camera>();
        cam.nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();
        camGo.AddComponent<PhysicsRaycaster>();          // needed to click enemies

        // Mouse look: player turns left/right, camera looks up/down
        var lookX = player.AddComponent<MouseLook>();
        lookX.axes = MouseLook.RotationAxes.MouseX;
        var lookY = camGo.AddComponent<MouseLook>();
        lookY.axes = MouseLook.RotationAxes.MouseY;

        // Enemies standing in the hallway
        if (!dungeon)
        {
            Encounter("Encounter_Rogue", 10, new[] { en.rogue });
            Encounter("Encounter_StreetEnforcer", 21, new[] { en.enforcer });
            Encounter("Encounter_Rogue+Drone", 33, new[] { en.rogue, en.drone });
        }

        // Battle arena (moved in front of the player every fight)
        var arena = new GameObject("BattleArena").transform;
        var spawnCenter = Point("SpawnCenter", arena, new Vector3(0, 0, 0));
        var spawnLeft   = Point("SpawnLeft",   arena, new Vector3(-1.4f, 0, -0.8f));
        var spawnRight  = Point("SpawnRight",  arena, new Vector3(1.4f, 0, -0.8f));
        var spawns = new Object[] { spawnCenter, spawnLeft, spawnRight };

        // Battle manager
        var bmGo = new GameObject("BattleManager");
        var bm = bmGo.AddComponent<BattleManager>();
        var dm = bmGo.AddComponent<DeckManager>();
        var bso = new SerializedObject(bm);
        bso.FindProperty("player").objectReferenceValue = pc;
        bso.FindProperty("deck").objectReferenceValue = dm;
        bso.FindProperty("enemyPrefab").objectReferenceValue = enemyPrefab;
        SetArray(bso.FindProperty("defaultSpawnPoints"), spawns);
        SetArray(bso.FindProperty("testDeck"), deck.ToArray());     // only used if the backpack is empty
        bso.ApplyModifiedPropertiesWithoutUndo();

        // Inventory (copied from InventoryTest): hook it to this player, add starting items
        InventoryGrid grid = null;
        InventoryToggle toggle = null;
        GameObject inGameHud = null;
        if (inventoryCanvas)
        {
            grid = inventoryCanvas.GetComponentInChildren<InventoryGrid>(true);
            toggle = inventoryCanvas.GetComponent<InventoryToggle>();
            if (toggle)
            {
                var iso = new SerializedObject(toggle);
                SetArray(iso.FindProperty("disableWhileOpen"), new Object[] { movement, lookX, lookY });
                iso.FindProperty("lockCursorWhenClosed").boolValue = true;     // mouse look needs a locked cursor
                inGameHud = iso.FindProperty("hudWhenClosed").objectReferenceValue as GameObject;
                iso.ApplyModifiedPropertiesWithoutUndo();
            }

            var start = inventoryCanvas.AddComponent<StartingItems>();
            var sso = new SerializedObject(start);
            sso.FindProperty("inventory").objectReferenceValue = grid;
            SetArray(sso.FindProperty("items"), new Object[] { items.knife, items.bandage });
            sso.ApplyModifiedPropertiesWithoutUndo();

            var bso2 = new SerializedObject(bm);
            bso2.FindProperty("inventory").objectReferenceValue = grid;     // battle deck = backpack
            bso2.ApplyModifiedPropertiesWithoutUndo();
        }

        // UI
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

        var canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.layer = 5;
        canvasGo.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        canvasGo.GetComponent<Canvas>().sortingOrder = 10;        // above the inventory canvas, so the fade covers it
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        var canvas = canvasGo.transform;

        // Exploration HUD
        var explore = Txt("ExplorationHUD", canvas, "W = walk forward   Mouse = look   I = backpack   Walk up to an enemy to fight", 28, TextAnchor.MiddleCenter, Color.white);
        Place(explore.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(1200, 50));

        var battleUIGo = BuildBattleUI(canvas, bm, dm, pc, cardPrefab, items.rewards, dropPrefab, player.transform, grid);

        // Fade screen (last = drawn on top)
        var fade = Img("FadeScreen", canvas, Color.black);
        Stretch(fade.rectTransform);
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0; fadeGroup.blocksRaycasts = false;

        // Transition
        var tGo = new GameObject("BattleTransition");
        var bt = tGo.AddComponent<BattleTransition>();
        var tso = new SerializedObject(bt);
        tso.FindProperty("battle").objectReferenceValue = bm;
        tso.FindProperty("player").objectReferenceValue = player.transform;
        tso.FindProperty("playerCamera").objectReferenceValue = camGo.transform;
        var disable = new List<Object> { movement, lookX, lookY };
        if (toggle) disable.Add(toggle);                                  // no opening the backpack mid-fight
        SetArray(tso.FindProperty("disableDuringBattle"), disable.ToArray());
        var exploreUI = new List<Object> { explore.gameObject };
        if (inGameHud) exploreUI.Add(inGameHud);
        SetArray(tso.FindProperty("explorationUI"), exploreUI.ToArray());
        tso.FindProperty("inventoryToggle").objectReferenceValue = toggle;
        tso.FindProperty("battleUI").objectReferenceValue = battleUIGo;
        tso.FindProperty("fadeScreen").objectReferenceValue = fadeGroup;
        tso.FindProperty("battleArena").objectReferenceValue = arena;
        SetArray(tso.FindProperty("spawnPoints"), spawns);
        tso.FindProperty("battleDistance").floatValue = 4.5f;
        tso.ApplyModifiedPropertiesWithoutUndo();

        if (!dungeon)
        {
            // End of the hallway: portal back to the Lobby
            Portal("Portal_ToLobby", new Vector3(0, 0, 39.5f), "Back to Lobby", "Lobby", false, false, fadeGroup, null);
        }
        else
        {
            BuildDungeonObjects(canvas, explore, fade.transform, fadeGroup, bm, player.transform, en,
                                new Object[] { movement, lookX, lookY, toggle });
        }

        battleUIGo.SetActive(false);
    }

    static GameObject BuildBattleUI(Transform canvas, BattleManager bm, DeckManager dm, PlayerCombatant pc, CardView cardPrefab,
                                    List<ItemData> rewardPool, WorldItemPickup dropPrefab, Transform player, InventoryGrid grid)
    {
        var root = new GameObject("BattleUI", typeof(RectTransform));
        root.layer = 5;
        root.transform.SetParent(canvas, false);
        Stretch((RectTransform)root.transform);
        var t = root.transform;

        // Log (top-left)
        var log = Txt("Log", t, "", 22, TextAnchor.UpperLeft, new Color(0.85f, 0.9f, 1f));
        Place(log.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -20), new Vector2(700, 200));

        // Player HUD (bottom-left)
        var panel = Img("PlayerHUD", t, new Color(0, 0, 0, 0.6f));
        Place(panel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(20, 20), new Vector2(360, 120));
        var pName = Txt("Name", panel.transform, "Player", 24, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        Place(pName.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, 1), new Vector2(12, -6), new Vector2(-24, 30));
        var bar = HpBar(panel.transform, out var fill, out var hpText);
        Place(bar, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(-24, 30));
        var shieldGroup = Img("Shield", panel.transform, new Color(0.3f, 0.5f, 1f));
        Place(shieldGroup.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1), new Vector2(-12, -6), new Vector2(70, 30));
        var shieldText = Txt("Value", shieldGroup.transform, "0", 22, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Stretch(shieldText.rectTransform);
        var status = Txt("Status", panel.transform, "", 18, TextAnchor.UpperLeft, new Color(0.8f, 1f, 0.8f));
        Place(status.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 0), new Vector2(12, 6), new Vector2(-24, 40));
        var hud = panel.gameObject.AddComponent<CombatantHUD>();
        WireHUD(hud, pc, pName, fill, hpText, shieldGroup.gameObject, shieldText, status, null, false);

        // Mana + draw pile (above player HUD)
        var manaLabel = Txt("ManaLabel", t, "MANA", 22, TextAnchor.MiddleLeft, new Color(0.5f, 0.8f, 1f), FontStyle.Bold);
        Place(manaLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 190), new Vector2(120, 30));
        var mana = Txt("Mana", t, "3/3", 48, TextAnchor.MiddleLeft, new Color(0.5f, 0.8f, 1f), FontStyle.Bold);
        Place(mana.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(24, 140), new Vector2(160, 56));
        var drawLabel = Txt("DrawLabel", t, "Draw pile", 20, TextAnchor.MiddleLeft, Color.white);
        Place(drawLabel.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 190), new Vector2(140, 30));
        var draw = Txt("DrawCount", t, "0", 36, TextAnchor.MiddleLeft, Color.white, FontStyle.Bold);
        Place(draw.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(0, 0), new Vector2(200, 140), new Vector2(140, 56));

        // Discard + End Turn (bottom-right)
        var discardLabel = Txt("DiscardLabel", t, "Discard", 20, TextAnchor.MiddleRight, Color.white);
        Place(discardLabel.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 70), new Vector2(160, 30));
        var discard = Txt("DiscardCount", t, "0", 36, TextAnchor.MiddleRight, Color.white, FontStyle.Bold);
        Place(discard.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 20), new Vector2(160, 50));
        var endTurn = Btn("EndTurnButton", t, "END TURN", new Color(0.85f, 0.35f, 0.15f));
        Place((RectTransform)endTurn.transform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 130), new Vector2(220, 70));

        // Hand (bottom-centre)
        var handRt = new GameObject("Hand", typeof(RectTransform)).GetComponent<RectTransform>();
        handRt.gameObject.layer = 5;
        handRt.SetParent(t, false);
        Place(handRt, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(1100, 240));
        var hlg = handRt.gameObject.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 10; hlg.childAlignment = TextAnchor.LowerCenter;
        hlg.childControlWidth = true; hlg.childControlHeight = true;
        hlg.childForceExpandWidth = false; hlg.childForceExpandHeight = false;
        var hand = handRt.gameObject.AddComponent<HandView>();
        var hso = new SerializedObject(hand);
        hso.FindProperty("deck").objectReferenceValue = dm;
        hso.FindProperty("cardPrefab").objectReferenceValue = cardPrefab;
        hso.ApplyModifiedPropertiesWithoutUndo();

        // Hint
        var hint = Txt("Hint", t, "Click a card, then click an enemy", 22, TextAnchor.MiddleCenter, new Color(1, 1, 1, 0.7f));
        Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 270), new Vector2(600, 30));

        // BattleUI component (panels need it for their buttons)
        var ui = root.AddComponent<BattleUI>();

        var victory = VictoryPanel(t, ui, rewardPool, dropPrefab, player, grid, out var victoryInfo);
        var defeat = Panel("DefeatPanel", t, "DEFEATED", new Color(0.35f, 0.08f, 0.08f, 0.95f), out _, "Back to Lobby",
                           new UnityAction(ui.BackToLobby));

        var uso = new SerializedObject(ui);
        uso.FindProperty("battle").objectReferenceValue = bm;
        uso.FindProperty("manaText").objectReferenceValue = mana;
        uso.FindProperty("drawPileText").objectReferenceValue = draw;
        uso.FindProperty("discardPileText").objectReferenceValue = discard;
        uso.FindProperty("logText").objectReferenceValue = log;
        uso.FindProperty("endTurnButton").objectReferenceValue = endTurn;
        uso.FindProperty("victoryPanel").objectReferenceValue = victory;
        uso.FindProperty("victoryCreditsText").objectReferenceValue = victoryInfo;
        uso.FindProperty("defeatPanel").objectReferenceValue = defeat;
        uso.ApplyModifiedPropertiesWithoutUndo();

        victory.SetActive(false);
        defeat.SetActive(false);
        return root;
    }

    // Victory: credits + "choose 1 of 3 items" + Skip
    static GameObject VictoryPanel(Transform parent, BattleUI ui, List<ItemData> rewardPool, WorldItemPickup dropPrefab,
                                   Transform player, InventoryGrid grid, out Text info)
    {
        var p = Img("VictoryPanel", parent, new Color(0.08f, 0.2f, 0.12f, 0.96f));
        Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 80), new Vector2(1100, 560));

        var titleT = Txt("Title", p.transform, "VICTORY", 56, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(titleT.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -15), new Vector2(0, 70));
        info = Txt("Credits", p.transform, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));
        Place(info.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -85), new Vector2(0, 40));
        var choose = Txt("ChooseLabel", p.transform, "Choose a reward:", 26, TextAnchor.MiddleCenter, Color.white);
        Place(choose.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(0, 36));

        var buttons = new Button[3];
        var texts = new Text[3];
        for (int i = 0; i < 3; i++)
        {
            var b = Btn("Reward" + (i + 1), p.transform, "", new Color(0.3f, 0.3f, 0.35f));
            Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                  new Vector2((i - 1) * 350, -175), new Vector2(330, 190));
            var label = b.GetComponentInChildren<Text>();
            label.fontSize = 20; label.fontStyle = FontStyle.Normal;
            label.alignment = TextAnchor.MiddleCenter;
            label.rectTransform.offsetMin = new Vector2(10, 10); label.rectTransform.offsetMax = new Vector2(-10, -10);
            buttons[i] = b; texts[i] = label;
        }

        var message = Txt("Message", p.transform, "", 24, TextAnchor.MiddleCenter, new Color(1f, 0.85f, 0.5f));
        Place(message.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 95), new Vector2(-40, 70));

        var skip = Btn("SkipButton", p.transform, "Skip", new Color(0.2f, 0.2f, 0.25f));
        Place((RectTransform)skip.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(220, 60));
        UnityEventTools.AddPersistentListener(skip.onClick, new UnityAction(ui.Continue));

        var reward = p.gameObject.AddComponent<ItemRewardPanel>();
        var so = new SerializedObject(reward);
        so.FindProperty("inventory").objectReferenceValue = grid;
        SetArray(so.FindProperty("rewardPool"), rewardPool.ToArray());
        SetArray(so.FindProperty("choiceButtons"), buttons);
        SetArray(so.FindProperty("choiceTexts"), texts);
        so.FindProperty("skipButton").objectReferenceValue = skip;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("dropPrefab").objectReferenceValue = dropPrefab;
        so.FindProperty("player").objectReferenceValue = player;
        so.ApplyModifiedPropertiesWithoutUndo();
        return p.gameObject;
    }

    static GameObject Panel(string name, Transform parent, string title, Color color, out Text info, string buttonLabel, UnityAction onClick)
    {
        var p = Img(name, parent, color);
        Place(p.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 60), new Vector2(520, 300));
        var titleT = Txt("Title", p.transform, title, 56, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(titleT.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(0, 80));
        info = Txt("Info", p.transform, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f));
        Place(info.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(0, 50));
        var b = Btn("Button", p.transform, buttonLabel, new Color(0.2f, 0.2f, 0.25f));
        Place((RectTransform)b.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 25), new Vector2(240, 64));
        UnityEventTools.AddPersistentListener(b.onClick, onClick);
        return p.gameObject;
    }

    // =====================================================================
    // Inventory copy + dropped item prefab
    // =====================================================================

    // Copies the whole inventory Canvas (InventoryMenu, InGame HUD, PickupText, InventoryToggle) from InventoryTest,
    // so it's exactly the team's version. Returns null if that scene isn't there.
    static GameObject CopyInventoryCanvas()
    {
        if (!System.IO.File.Exists(InventoryScenePath))
        {
            Debug.LogWarning("BattleTestBuilder: " + InventoryScenePath + " not found, no inventory in the test scene.");
            return null;
        }

        var target = SceneManager.GetActiveScene();
        var invScene = EditorSceneManager.OpenScene(InventoryScenePath, OpenSceneMode.Additive);

        GameObject source = null;
        foreach (var root in invScene.GetRootGameObjects())
            if (root.GetComponent<InventoryToggle>()) source = root;

        GameObject copy = null;
        if (source)
        {
            copy = Object.Instantiate(source);
            SceneManager.MoveGameObjectToScene(copy, target);
            copy.name = "InventoryCanvas";
        }
        else Debug.LogWarning("BattleTestBuilder: no object with InventoryToggle in " + InventoryScenePath);

        EditorSceneManager.CloseScene(invScene, true);
        SceneManager.SetActiveScene(target);
        return copy;
    }

    // Floating cube that WorldItemPickup uses when a reward doesn't fit. Coloured with the item's tint at runtime.
    static WorldItemPickup BuildDropPrefab()
    {
        var root = new GameObject("DroppedItem");
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.size = new Vector3(1f, 1.5f, 1f);
        var rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "Visual";
        Object.DestroyImmediate(cube.GetComponent<Collider>());
        cube.transform.SetParent(root.transform, false);
        cube.transform.localScale = Vector3.one * 0.35f;
        if (gray) cube.GetComponent<Renderer>().sharedMaterial = gray;

        root.AddComponent<WorldItemPickup>();
        var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabDir + "/DroppedItem.prefab");
        Object.DestroyImmediate(root);
        return prefab.GetComponent<WorldItemPickup>();
    }

    // =====================================================================
    // Balance pass: Tools > Roguelike > Apply Balance Pass
    //   - stronger enemies (+ Netrunner elite, Warden Mech boss), fewer cards per item, all items + ItemDatabase
    //   - backpack prefab -> 3x3 (upgrades still add rows)
    //   - Dungeon scene: ONLY the DungeonManager's encounter lists + difficulty are changed
    // =====================================================================

    const string InventoryPrefabPath = "Assets/Prefab/Inventory/InventoryCanvas.prefab";

    [MenuItem("Tools/Roguelike/Apply Balance Pass")]
    public static void ApplyBalancePass()
    {
        if (!EditorUtility.DisplayDialog("Apply balance pass?",
                "Updates all enemy, card and item assets, makes the backpack 3x3 (InventoryCanvas prefab), " +
                "and sets the encounters + difficulty on the DungeonManager in the Dungeon scene " +
                "(nothing else in that scene is changed).\n\nCommit to git first. Continue?", "Apply", "Cancel")) return;
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

        Prepare();
        BuildCards();
        var items = BuildItems();
        BuildItemDatabase(items);
        var en = BuildEnemies();
        AssetDatabase.SaveAssets();

        ResizeBackpack(3, 3);

        if (System.IO.File.Exists(DungeonPath))
        {
            var scene = EditorSceneManager.OpenScene(DungeonPath, OpenSceneMode.Single);
            DungeonManager dm = null;
            foreach (var root in scene.GetRootGameObjects())
                if (!dm) dm = root.GetComponentInChildren<DungeonManager>(true);
            if (dm)
            {
                var so = new SerializedObject(dm);
                SetGroups(so.FindProperty("normalEncounters"), new[] { new[] { en.rogue }, new[] { en.drone }, new[] { en.rogue, en.drone } });
                SetGroups(so.FindProperty("eliteEncounters"), new[] { new[] { en.enforcer }, new[] { en.netrunner } });
                SetGroups(so.FindProperty("bossEncounters"), new[] { new[] { en.warden } });
                so.FindProperty("enemyHpMultiplier").floatValue = 1f;
                so.FindProperty("enemyDamageMultiplier").floatValue = 1f;
                so.FindProperty("scalingPerRoom").floatValue = 0.06f;
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            else Debug.LogWarning("Balance pass: no DungeonManager in the Dungeon scene.");
        }
        Debug.Log("Balance pass applied: enemies, items (fewer cards), 3x3 backpack, Dungeon encounters (boss = Warden Mech).");
    }

    // Changes the backpack grid in the InventoryCanvas prefab and fits the background around it.
    static void ResizeBackpack(int cols, int rows)
    {
        if (!AssetDatabase.LoadAssetAtPath<GameObject>(InventoryPrefabPath))
        {
            Debug.LogWarning("Balance pass: " + InventoryPrefabPath + " not found, backpack not resized.");
            return;
        }
        var root = PrefabUtility.LoadPrefabContents(InventoryPrefabPath);
        var grid = root.GetComponentInChildren<InventoryGrid>(true);
        var so = new SerializedObject(grid);
        int oldCols = so.FindProperty("columns").intValue, oldRows = so.FindProperty("rows").intValue;
        var slotGrid = (GridLayoutGroup)so.FindProperty("slotGrid").objectReferenceValue;
        Vector2 step = slotGrid.cellSize + slotGrid.spacing;

        so.FindProperty("columns").intValue = cols;
        so.FindProperty("rows").intValue = rows;
        if (so.FindProperty("maxRows").intValue < rows) so.FindProperty("maxRows").intValue = rows;

        // slots: exactly cols x rows
        slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        slotGrid.constraintCount = cols;
        var slots = slotGrid.transform;
        for (int i = slots.childCount - 1; i >= cols * rows; i--) Object.DestroyImmediate(slots.GetChild(i).gameObject);
        for (int i = slots.childCount; i < cols * rows; i++)
            Object.Instantiate(slots.GetChild(0).gameObject, slots).name = "InventorySlot" + i;

        // background: keep its left + top edge, shrink/grow with the grid, and grow DOWN when a row is bought
        float dx = (cols - oldCols) * step.x, dy = (rows - oldRows) * step.y;
        var bg = FindChild(grid.transform, "InventoryBackground") as RectTransform;
        if (bg)
        {
            bg.sizeDelta += new Vector2(dx, dy);
            bg.anchoredPosition += new Vector2(dx / 2f, -dy / 2f);
            var top = bg.anchoredPosition + new Vector2(0, bg.sizeDelta.y * (1f - bg.pivot.y));
            bg.pivot = new Vector2(bg.pivot.x, 1f);
            bg.anchoredPosition = new Vector2(bg.anchoredPosition.x, top.y);
            var grow = so.FindProperty("growWithRows");
            grow.arraySize = 1;
            grow.GetArrayElementAtIndex(0).objectReferenceValue = bg;
        }
        var title = FindChild(grid.transform, "Title") as RectTransform;
        if (title) title.anchoredPosition += new Vector2(dx / 2f, 0);
        var trash = FindChild(grid.transform, "Trashbin") as RectTransform;
        if (trash) trash.anchoredPosition += new Vector2(dx, 0);

        so.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SaveAsPrefabAsset(root, InventoryPrefabPath);
        PrefabUtility.UnloadPrefabContents(root);
    }

    // Direct child first (InventoryMenu > Title, not the tooltip's Title), then anywhere below.
    static Transform FindChild(Transform t, string name)
    {
        var direct = t.Find(name);
        if (direct) return direct;
        foreach (var c in t.GetComponentsInChildren<Transform>(true))
            if (c.name == name) return c;
        return null;
    }

    // =====================================================================
    // Dungeon: DungeonManager, map screen (M), floor cleared panel
    // =====================================================================

    static void BuildDungeonObjects(Transform canvas, Text hud, Transform fade, CanvasGroup fadeGroup, BattleManager bm,
                                    Transform player, Enemies en, Object[] disableOnClear)
    {
        hud.text = "Floor 1";

        var message = Txt("DungeonMessage", canvas, "", 30, TextAnchor.MiddleCenter, new Color(1f, 0.55f, 0.4f), FontStyle.Bold);
        Place(message.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 140), new Vector2(900, 50));

        // Map screen
        var mapRoot = new GameObject("MapView", typeof(RectTransform));
        mapRoot.layer = 5;
        mapRoot.transform.SetParent(canvas, false);
        Stretch((RectTransform)mapRoot.transform);
        var mapPanel = Img("MapPanel", mapRoot.transform, new Color(0.04f, 0.05f, 0.09f, 0.95f));
        Place(mapPanel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 900));
        var mapTitle = Txt("Title", mapPanel.transform, "FLOOR 1 MAP", 40, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(mapTitle.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(0, 60));
        var legend = Txt("Legend", mapPanel.transform, "?  Enemy     !  Elite     BOSS\nYellow = you are here     Green = next doors     M = close",
                         20, TextAnchor.MiddleCenter, new Color(0.8f, 0.85f, 0.9f));
        Place(legend.rectTransform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0.5f, 0), new Vector2(0, 15), new Vector2(0, 60));
        var content = new GameObject("Content", typeof(RectTransform)).GetComponent<RectTransform>();
        content.gameObject.layer = 5;
        content.SetParent(mapPanel.transform, false);
        Place(content, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 5), new Vector2(600, 680));
        var mapView = mapRoot.AddComponent<MapView>();
        var mso = new SerializedObject(mapView);
        mso.FindProperty("panel").objectReferenceValue = mapPanel.gameObject;
        mso.FindProperty("content").objectReferenceValue = content;
        mso.FindProperty("mapSize").vector2Value = new Vector2(560, 640);
        mso.ApplyModifiedPropertiesWithoutUndo();

        // Manager
        var dmGo = new GameObject("DungeonManager");
        var dm = dmGo.AddComponent<DungeonManager>();

        // Floor cleared panel
        var cleared = Img("FloorClearedPanel", canvas, new Color(0.1f, 0.08f, 0.2f, 0.96f));
        Place(cleared.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 340));
        var ct = Txt("Title", cleared.transform, "FLOOR CLEARED", 56, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(ct.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -25), new Vector2(0, 80));
        var cinfo = Txt("Info", cleared.transform, "You beat the boss!\nMore floors coming later.", 28, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.5f));
        Place(cinfo.rectTransform, new Vector2(0, 0.5f), new Vector2(1, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 0), new Vector2(0, 90));
        var cb = Btn("LobbyButton", cleared.transform, "Back to Lobby", new Color(0.3f, 0.25f, 0.45f));
        Place((RectTransform)cb.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(280, 64));
        UnityEventTools.AddPersistentListener(cb.onClick, new UnityAction(dm.BackToLobby));

        fade.SetAsLastSibling();                    // fade stays on top of the new panels

        var black = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Black.mat");
        var orange = MakeMat("Assets/Material/DoorElite.mat", new Color(1f, 0.55f, 0.15f), true);
        var purple = MakeMat("Assets/Material/DoorBoss.mat", new Color(0.6f, 0.25f, 0.9f), true);

        var so = new SerializedObject(dm);
        so.FindProperty("battle").objectReferenceValue = bm;
        so.FindProperty("player").objectReferenceValue = player;
        SetArray(so.FindProperty("disableOnFloorCleared"), System.Array.FindAll(disableOnClear, o => o != null));
        so.FindProperty("floorMat").objectReferenceValue = brown;
        so.FindProperty("wallMat").objectReferenceValue = gray;
        so.FindProperty("enemyMat").objectReferenceValue = red;
        so.FindProperty("doorEnemyMat").objectReferenceValue = black ? black : gray;
        so.FindProperty("doorEliteMat").objectReferenceValue = orange;
        so.FindProperty("doorBossMat").objectReferenceValue = purple;
        so.FindProperty("doorOtherMat").objectReferenceValue = MakeMat("Assets/Material/DoorOther.mat", new Color(0.3f, 0.6f, 0.9f), true);

        // Room prefabs: make the folders + one template room, then fill the library from the folders
        BuildRoomTemplate();
        so.FindProperty("roomLibrary").objectReferenceValue = RoomLibraryUpdater.Refresh(true);
        so.FindProperty("fadeScreen").objectReferenceValue = fadeGroup;
        so.FindProperty("hudText").objectReferenceValue = hud;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("floorClearedPanel").objectReferenceValue = cleared.gameObject;
        so.FindProperty("mapView").objectReferenceValue = mapView;
        // Encounters: boss is just a normal enemy for now
        SetGroups(so.FindProperty("normalEncounters"), new[] { new[] { en.rogue }, new[] { en.drone }, new[] { en.rogue, en.drone } });
        SetGroups(so.FindProperty("eliteEncounters"), new[] { new[] { en.enforcer }, new[] { en.netrunner } });
        SetGroups(so.FindProperty("bossEncounters"), new[] { new[] { en.warden } });
        so.ApplyModifiedPropertiesWithoutUndo();

        cleared.gameObject.SetActive(false);
    }

    // Example room prefab in Assets/Prefab/Rooms/Enemy, same shape as the old code-built hallway.
    // Only made if it doesn't exist yet, so the team's edits to it are never overwritten.
    // Copy it into the other folders (or make new ones) to make more rooms.
    static void BuildRoomTemplate()
    {
        RoomLibraryUpdater.EnsureFolders();
        const string path = RoomLibraryUpdater.RoomsFolder + "/Enemy/Hallway_Basic.prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(path)) return;

        const float hall = 14f, width = 4f, h = 4f, depth = 4f, chamberW = 9.4f;
        float endZ = hall + depth, side = (chamberW - width) / 2f;

        var root = new GameObject("Hallway_Basic");
        var t = root.transform;
        var geo = new GameObject("Geometry").transform;
        geo.SetParent(t, false);
        Box("Floor", geo, new Vector3(0, -0.1f, hall / 2), new Vector3(width, 0.2f, hall), brown);
        Box("WallLeft", geo, new Vector3(-width / 2 - 0.1f, h / 2, hall / 2), new Vector3(0.2f, h, hall), gray);
        Box("WallRight", geo, new Vector3(width / 2 + 0.1f, h / 2, hall / 2), new Vector3(0.2f, h, hall), gray);
        Box("WallBack", geo, new Vector3(0, h / 2, -0.1f), new Vector3(width + 0.4f, h, 0.2f), gray);
        Box("ChamberFloor", geo, new Vector3(0, -0.1f, hall + depth / 2), new Vector3(chamberW, 0.2f, depth), brown);
        Box("ChamberFrontLeft", geo, new Vector3(-(width / 2 + side / 2), h / 2, hall - 0.1f), new Vector3(side, h, 0.2f), gray);
        Box("ChamberFrontRight", geo, new Vector3(width / 2 + side / 2, h / 2, hall - 0.1f), new Vector3(side, h, 0.2f), gray);
        Box("ChamberLeft", geo, new Vector3(-chamberW / 2 - 0.1f, h / 2, hall + depth / 2), new Vector3(0.2f, h, depth), gray);
        Box("ChamberRight", geo, new Vector3(chamberW / 2 + 0.1f, h / 2, hall + depth / 2), new Vector3(0.2f, h, depth), gray);
        Box("ChamberEnd", geo, new Vector3(0, h / 2, endZ + 0.1f), new Vector3(chamberW + 0.4f, h, 0.2f), gray);

        var light = new GameObject("RoomLight").AddComponent<Light>();
        light.transform.SetParent(t, false);
        light.transform.localPosition = new Vector3(0, 3.5f, hall * 0.6f);
        light.type = LightType.Point;
        light.range = 22; light.intensity = 1.5f;

        var markers = new GameObject("Markers").transform;
        markers.SetParent(t, false);
        var playerSpawn = Point("PlayerSpawn", markers, new Vector3(0, 1, 1.5f));
        var enemySpawn = Point("EnemySpawn", markers, new Vector3(0, 0, hall * 0.55f));
        enemySpawn.localRotation = Quaternion.Euler(0, 180, 0);              // faces the player
        var slots = new Transform[3];
        for (int i = 0; i < 3; i++)
            slots[i] = Point("DoorSlot" + (i + 1), markers, new Vector3((i - 1) * 3f, 0, endZ));

        var room = root.AddComponent<RoomPrefab>();
        room.playerSpawn = playerSpawn;
        room.enemySpawn = enemySpawn;
        room.doorSlots = slots;

        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }

    static void SetGroups(SerializedProperty list, EnemyData[][] groups)
    {
        list.arraySize = groups.Length;
        for (int i = 0; i < groups.Length; i++)
            SetArray(list.GetArrayElementAtIndex(i).FindPropertyRelative("enemies"), groups[i]);
    }

    // =====================================================================
    // Lobby: walk around in first person, pick a character at the terminal, walk into the portal
    // =====================================================================

    static void BuildLobbyScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        BuildCards();
        var items = BuildItems();

        // Character 1 (doc: HP 135, DF 25, starts with Knife + Bandage)
        var char1 = LoadOrCreate<CharacterData>(CharacterDir + "/Character1.asset");
        char1.characterName = "Character 1";
        char1.description = "Placeholder character.";
        char1.maxHP = 135; char1.baseDefense = 25; char1.maxMana = 3;
        char1.startingCredits = 50;
        char1.color = new Color(0.25f, 0.55f, 0.9f);
        char1.unlocked = true;
        char1.startingItems = new List<ItemData>();
        if (items.knife) char1.startingItems.Add(items.knife);
        if (items.bandage) char1.startingItems.Add(items.bandage);
        EditorUtility.SetDirty(char1);

        // Light
        var light = new GameObject("Directional Light").AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.1f;
        light.transform.rotation = Quaternion.Euler(50, -30, 0);

        // Room 14 x 14
        var room = new GameObject("LobbyRoom").transform;
        Box("Floor", room, new Vector3(0, -0.1f, 0), new Vector3(14, 0.2f, 14), brown);
        Box("WallNorth", room, new Vector3(0, 2, 7.1f), new Vector3(14.4f, 4, 0.2f), gray);
        Box("WallSouth", room, new Vector3(0, 2, -7.1f), new Vector3(14.4f, 4, 0.2f), gray);
        Box("WallEast", room, new Vector3(7.1f, 2, 0), new Vector3(0.2f, 4, 14.4f), gray);
        Box("WallWest", room, new Vector3(-7.1f, 2, 0), new Vector3(0.2f, 4, 14.4f), gray);

        // Player
        var player = MakePlayer(new Vector3(0, 1, -5), out var movement, out var lookX, out var lookY);

        // UI
        new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        var canvas = MakeCanvas("Canvas", 0).transform;

        var help = Txt("Help", canvas, "WASD = move   Mouse = look   E at the terminal = choose character   Walk into the portal to start",
                       24, TextAnchor.MiddleCenter, Color.white);
        Place(help.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(1500, 40));
        var selectedLabel = Txt("SelectedCharacter", canvas, "Character: none", 28, TextAnchor.MiddleLeft, new Color(0.6f, 0.85f, 1f), FontStyle.Bold);
        Place(selectedLabel.rectTransform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -70), new Vector2(600, 40));
        var prompt = Txt("Prompt", canvas, "Press E to choose your character", 30, TextAnchor.MiddleCenter, new Color(1f, 0.9f, 0.4f), FontStyle.Bold);
        Place(prompt.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 160), new Vector2(900, 50));
        var message = Txt("PortalMessage", canvas, "", 26, TextAnchor.MiddleCenter, new Color(1f, 0.5f, 0.4f));
        Place(message.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 100), new Vector2(900, 40));

        // Character select panel: 3 slots (1 real, 2 placeholders)
        var panel = Img("CharacterSelectPanel", canvas, new Color(0.05f, 0.07f, 0.12f, 0.96f));
        Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(1200, 640));
        var title = Txt("Title", panel.transform, "CHOOSE YOUR CHARACTER", 48, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Place(title.rectTransform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), new Vector2(0, -15), new Vector2(0, 70));

        var slotButtons = new Button[3];
        var slotTexts = new Text[3];
        var slotPortraits = new Image[3];
        for (int i = 0; i < 3; i++)
        {
            var b = Btn("Slot" + (i + 1), panel.transform, "", new Color(0.16f, 0.18f, 0.25f));
            Place((RectTransform)b.transform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1),
                  new Vector2((i - 1) * 380, -100), new Vector2(360, 420));
            var portrait = Img("Portrait", b.transform, Color.gray);
            portrait.raycastTarget = false;
            Place(portrait.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -15), new Vector2(140, 140));
            var label = b.GetComponentInChildren<Text>();
            label.fontSize = 22; label.fontStyle = FontStyle.Normal; label.alignment = TextAnchor.UpperCenter;
            label.rectTransform.offsetMin = new Vector2(12, 12); label.rectTransform.offsetMax = new Vector2(-12, -170);
            slotButtons[i] = b; slotTexts[i] = label; slotPortraits[i] = portrait;
        }
        var close = Btn("CloseButton", panel.transform, "Close", new Color(0.3f, 0.3f, 0.35f));
        Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 25), new Vector2(220, 60));

        var fade = Img("FadeScreen", canvas, Color.black);
        Stretch(fade.rectTransform);
        var fadeGroup = fade.gameObject.AddComponent<CanvasGroup>();
        fadeGroup.alpha = 0; fadeGroup.blocksRaycasts = false;

        var selectGo = new GameObject("CharacterSelect");
        var select = selectGo.AddComponent<CharacterSelectUI>();
        var cso = new SerializedObject(select);
        SetArray(cso.FindProperty("characters"), new Object[] { char1, null, null });
        cso.FindProperty("panel").objectReferenceValue = panel.gameObject;
        SetArray(cso.FindProperty("slotButtons"), slotButtons);
        SetArray(cso.FindProperty("slotTexts"), slotTexts);
        SetArray(cso.FindProperty("slotPortraits"), slotPortraits);
        cso.FindProperty("closeButton").objectReferenceValue = close;
        cso.FindProperty("selectedLabel").objectReferenceValue = selectedLabel;
        SetArray(cso.FindProperty("disableWhileOpen"), new Object[] { movement, lookX, lookY });
        cso.ApplyModifiedPropertiesWithoutUndo();

        // Character terminal (left side) with a placeholder of the character next to it
        var terminal = new GameObject("CharacterTerminal");
        terminal.transform.position = new Vector3(-4, 0, 1);
        Box("Pedestal", terminal.transform, new Vector3(-4, 0.6f, 1), new Vector3(1f, 1.2f, 0.6f), gray);
        Box("Screen", terminal.transform, new Vector3(-4, 1.5f, 1.2f), new Vector3(0.9f, 0.6f, 0.05f), cyan);
        var dummy = Capsule("CharacterPlaceholder", terminal.transform, new Vector3(-1.4f, 1, 0.5f), Vector3.one);
        dummy.GetComponent<Renderer>().sharedMaterial = blue;
        Label("Label", terminal.transform, new Vector3(0, 2.4f, 0), "Character Select [E]", Color.white);
        var station = terminal.AddComponent<CharacterSelectStation>();
        var sso = new SerializedObject(station);
        sso.FindProperty("selectUI").objectReferenceValue = select;
        sso.FindProperty("promptText").objectReferenceValue = prompt;
        sso.ApplyModifiedPropertiesWithoutUndo();

        // Portal (far wall) -> combat test
        Portal("Portal_ToDungeon", new Vector3(0, 0, 5.5f), "Dungeon", "Dungeon", true, true, fadeGroup, message);
        Portal("Portal_ToCombatTest", new Vector3(4.5f, 0, 4.5f), "Combat Test", "BattleTest", true, true, fadeGroup, message);

        EditorSceneManager.SaveScene(scene, LobbyPath);
        AssetDatabase.SaveAssets();
        Debug.Log("Lobby built: " + LobbyPath);
    }

    static GameObject MakePlayer(Vector3 pos, out PlayerMovement movement, out MouseLook lookX, out MouseLook lookY)
    {
        var player = new GameObject("Player") { tag = "Player" };
        player.transform.position = pos;
        var cc = player.AddComponent<CharacterController>();
        cc.height = 2; cc.radius = 0.4f; cc.center = Vector3.zero;
        movement = player.AddComponent<PlayerMovement>();

        var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localPosition = new Vector3(0, 0.7f, 0);
        camGo.AddComponent<Camera>().nearClipPlane = 0.05f;
        camGo.AddComponent<AudioListener>();

        lookX = player.AddComponent<MouseLook>();
        lookX.axes = MouseLook.RotationAxes.MouseX;
        lookY = camGo.AddComponent<MouseLook>();
        lookY.axes = MouseLook.RotationAxes.MouseY;
        return player;
    }

    static Canvas MakeCanvas(string name, int sortingOrder)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        go.layer = 5;
        var c = go.GetComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        return c;
    }

    // Glowing disc you walk into. Loads `sceneName`.
    static void Portal(string name, Vector3 pos, string labelText, string sceneName, bool newRun, bool requireCharacter,
                       CanvasGroup fade, Text message)
    {
        var root = new GameObject(name);
        root.transform.position = pos;
        var col = root.AddComponent<BoxCollider>();
        col.isTrigger = true;
        col.center = new Vector3(0, 1.5f, 0);
        col.size = new Vector3(2.4f, 3f, 1f);
        var rb = root.AddComponent<Rigidbody>();      // kinematic body so the trigger always fires
        rb.isKinematic = true; rb.useGravity = false;

        var disc = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        disc.name = "Visual";
        Object.DestroyImmediate(disc.GetComponent<Collider>());
        disc.transform.SetParent(root.transform, false);
        disc.transform.localPosition = new Vector3(0, 1.5f, 0);
        disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
        disc.transform.localScale = new Vector3(2.4f, 0.05f, 2.4f);
        if (cyan) disc.GetComponent<Renderer>().sharedMaterial = cyan;

        var glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(root.transform, false);
        glow.transform.localPosition = new Vector3(0, 1.5f, -1f);
        glow.type = LightType.Point;
        glow.color = new Color(0.3f, 0.9f, 1f);
        glow.range = 6; glow.intensity = 3;

        Label("Label", root.transform, new Vector3(0, 3.3f, 0), labelText, new Color(0.5f, 0.95f, 1f));

        var portal = root.AddComponent<ScenePortal>();
        var so = new SerializedObject(portal);
        so.FindProperty("sceneName").stringValue = sceneName;
        so.FindProperty("startsNewRun").boolValue = newRun;
        so.FindProperty("requireCharacter").boolValue = requireCharacter;
        so.FindProperty("fadeScreen").objectReferenceValue = fade;
        so.FindProperty("messageText").objectReferenceValue = message;
        so.FindProperty("visual").objectReferenceValue = disc.transform;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // 3D text floating in the world (readable from the front, i.e. looking along +Z).
    static void Label(string name, Transform parent, Vector3 localPos, string text, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var tm = go.AddComponent<TextMesh>();
        tm.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
        tm.text = text; tm.color = color;
        tm.fontSize = 64; tm.characterSize = 0.04f;
        tm.anchor = TextAnchor.MiddleCenter; tm.alignment = TextAlignment.Center;
        tm.fontStyle = FontStyle.Bold;
    }

    // =====================================================================
    // Small helpers
    // =====================================================================

    static void WireHUD(CombatantHUD hud, Combatant target, Text nameT, RectTransform fill, Text hpText,
                        GameObject shieldGroup, Text shieldText, Text status, Text intent, bool faceCamera)
    {
        var so = new SerializedObject(hud);
        so.FindProperty("target").objectReferenceValue = target;
        so.FindProperty("nameText").objectReferenceValue = nameT;
        so.FindProperty("hpFill").objectReferenceValue = fill;
        so.FindProperty("hpText").objectReferenceValue = hpText;
        so.FindProperty("shieldGroup").objectReferenceValue = shieldGroup;
        so.FindProperty("shieldText").objectReferenceValue = shieldText;
        so.FindProperty("statusText").objectReferenceValue = status;
        so.FindProperty("intentText").objectReferenceValue = intent;
        so.FindProperty("faceCamera").boolValue = faceCamera;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static RectTransform HpBar(Transform parent, out RectTransform fill, out Text hpText)
    {
        var bg = Img("HPBar", parent, new Color(0.15f, 0.15f, 0.15f, 0.9f));
        var f = Img("Fill", bg.transform, new Color(0.85f, 0.2f, 0.25f));
        Stretch(f.rectTransform);
        fill = f.rectTransform;
        hpText = Txt("HPText", bg.transform, "100/100", 20, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Stretch(hpText.rectTransform);
        return bg.rectTransform;
    }

    static void Encounter(string name, float z, EnemyData[] enemies)
    {
        var go = new GameObject(name);
        go.transform.position = new Vector3(0, 0, z);
        go.transform.rotation = Quaternion.Euler(0, 180, 0);   // facing the player
        for (int i = 0; i < enemies.Length; i++)
        {
            float x = enemies.Length == 1 ? 0 : (i == 0 ? -0.7f : 0.7f);
            var s = i == 0 ? Vector3.one : Vector3.one * 0.7f;
            Capsule("Model_" + enemies[i].enemyName, go.transform, new Vector3(x, s.y, 0), s);
        }
        var enc = go.AddComponent<EnemyEncounter>();
        var so = new SerializedObject(enc);
        SetArray(so.FindProperty("enemies"), enemies);
        so.FindProperty("engageRange").floatValue = 3f;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    static GameObject Capsule(string name, Transform parent, Vector3 localPos, Vector3 scale)
    {
        var c = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        c.name = name;
        Object.DestroyImmediate(c.GetComponent<Collider>());
        c.transform.SetParent(parent, false);
        c.transform.localPosition = localPos;
        c.transform.localScale = scale;
        if (red) c.GetComponent<Renderer>().sharedMaterial = red;
        return c;
    }

    static void Box(string name, Transform parent, Vector3 pos, Vector3 scale, Material mat)
    {
        var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
        b.name = name;
        b.transform.SetParent(parent, false);
        b.transform.position = pos;
        b.transform.localScale = scale;
        if (mat) b.GetComponent<Renderer>().sharedMaterial = mat;
    }

    static Transform Point(string name, Transform parent, Vector3 localPos)
    {
        var p = new GameObject(name).transform;
        p.SetParent(parent, false);
        p.localPosition = localPos;
        return p;
    }

    static Text Txt(string name, Transform parent, string text, int size, TextAnchor align, Color color, FontStyle style = FontStyle.Normal)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = font; t.text = text; t.fontSize = size; t.alignment = align; t.color = color; t.fontStyle = style;
        t.horizontalOverflow = HorizontalWrapMode.Wrap;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static Image Img(string name, Transform parent, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5;
        go.transform.SetParent(parent, false);
        var i = go.AddComponent<Image>();
        i.color = color;
        return i;
    }

    static Button Btn(string name, Transform parent, string label, Color color)
    {
        var img = Img(name, parent, color);
        var b = img.gameObject.AddComponent<Button>();
        b.targetGraphic = img;
        var l = Txt("Label", img.transform, label, 28, TextAnchor.MiddleCenter, Color.white, FontStyle.Bold);
        Stretch(l.rectTransform);
        return b;
    }

    static void Place(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = anchorMin; rt.anchorMax = anchorMax; rt.pivot = pivot;
        rt.anchoredPosition = pos; rt.sizeDelta = size;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    static void SetArray(SerializedProperty prop, Object[] values)
    {
        prop.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
            prop.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
    }

    static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(path);
        if (!a)
        {
            a = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(a, path);
        }
        return a;
    }

    static void EnsureFolder(string parent, string child)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + child)) AssetDatabase.CreateFolder(parent, child);
    }

    static void LoadMaterials()
    {
        gray = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Gray.mat");
        brown = AssetDatabase.LoadAssetAtPath<Material>("Assets/Material/Brown.mat");

        cyan = MakeMat("Assets/Material/PortalCyan.mat", new Color(0.2f, 0.85f, 1f), true);
        blue = MakeMat("Assets/Material/CharacterBlue.mat", new Color(0.25f, 0.55f, 0.9f), false);

        const string redPath = "Assets/Material/EnemyRed.mat";
        red = AssetDatabase.LoadAssetAtPath<Material>(redPath);
        if (!red)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            red = new Material(shader);
            red.SetColor("_BaseColor", new Color(0.8f, 0.15f, 0.2f));
            red.color = new Color(0.8f, 0.15f, 0.2f);
            AssetDatabase.CreateAsset(red, redPath);
        }
    }

    static Material MakeMat(string path, Color color, bool glow)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m) return m;
        var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        m = new Material(shader);
        m.SetColor("_BaseColor", color);
        m.color = color;
        if (glow)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", color * 2f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }
        AssetDatabase.CreateAsset(m, path);
        return m;
    }
}
