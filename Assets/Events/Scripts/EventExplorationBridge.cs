using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

[DefaultExecutionOrder(-1000)]
public class EventExplorationBridge : MonoBehaviour
{
    [Header("Exactly 12 different event prefabs")]
    [SerializeField] GameObject[] eventPrefabs = new GameObject[12];

    [Header("Dungeon references")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;
    [SerializeField] BattleManager battleManager;

    [Header("Testing")]
    [SerializeField] bool enableF8Test = true;
    [SerializeField] bool repeatAfterAllEvents = true;

    const BindingFlags Fields =
        BindingFlags.Instance | BindingFlags.Public |
        BindingFlags.NonPublic;

    readonly List<int> remainingEvents = new List<int>();
    readonly List<PausedComponent> pausedComponents =
        new List<PausedComponent>();

    // Keep completed event controllers alive for ongoing effects.
    readonly List<AnonymousDeliveryEvent> deliveryControllers =
        new List<AnonymousDeliveryEvent>();

    GameObject activeEvent;
    Transform runtimeEvents;

    bool ready;
    bool eventOpen;
    bool gameOver;

    int cycle;
    int battleNumber;

    float previousTimeScale;
    CursorLockMode previousCursorLock;
    bool previousCursorVisible;

    public bool IsEventOpen => eventOpen;

    class PausedComponent
    {
        public Behaviour component;
        public bool wasEnabled;
        public bool isMouseLook;
        public Quaternion savedRotation;
    }

    void Start()
    {
        if (!inventory || !inventoryToggle)
        {
            Debug.LogError(
                "EventBridge: assign Inventory and Inventory Toggle.",
                this
            );
            return;
        }

        if (eventPrefabs == null || eventPrefabs.Length != 12)
        {
            Debug.LogError(
                "EventBridge: Event Prefabs must contain exactly 12 entries.",
                this
            );
            return;
        }

        var uniquePrefabs = new HashSet<GameObject>();

        foreach (GameObject prefab in eventPrefabs)
        {
            if (!prefab)
            {
                Debug.LogError(
                    "EventBridge: an Event Prefabs entry is empty.",
                    this
                );
                return;
            }

            if (!uniquePrefabs.Add(prefab))
            {
                Debug.LogError(
                    "EventBridge: the same prefab was assigned twice.",
                    this
                );
                return;
            }

            if (prefab.activeSelf)
            {
                Debug.LogError(
                    $"EventBridge: prefab root must be inactive: {prefab.name}",
                    this
                );
                return;
            }
        }

        GameObject container = new GameObject("RuntimeEvents");
        container.transform.SetParent(transform, false);
        runtimeEvents = container.transform;

        if (battleManager)
            battleManager.OnBattleEnded += HandleBattleEnded;
        else
            Debug.LogWarning(
                "EventBridge: assign Battle Manager to count real "
                + "battle victories for packages.",
                this
            );

        ready = true;
    }

    void Update()
    {
        if (!ready) return;

        if (eventOpen)
            KeepExplorationPaused();

        if (enableF8Test
            && Keyboard.current != null
            && Keyboard.current.f8Key.wasPressedThisFrame)
        {
            OpenRandomEvent();
        }
    }

    void LateUpdate()
    {
        if (eventOpen)
            KeepExplorationPaused();
    }

    // Future event rooms can call this same method.
    public void OpenRandomEvent()
    {
        TryOpenRandomEvent();
    }

    // Returns false if opening was blocked.
    // A future room trigger should only mark itself used on success.
    public bool TryOpenRandomEvent()
    {
        if (!ready || eventOpen || gameOver)
            return false;

        if (BattleTransition.Instance
            && BattleTransition.Instance.Busy)
        {
            Debug.Log("Finish the battle before opening an event.", this);
            return false;
        }

        if (inventoryToggle.IsOpen)
        {
            Debug.Log("Close the backpack before opening an event.", this);
            return false;
        }

        if (Time.timeScale == 0f)
            return false;

        if (remainingEvents.Count == 0)
        {
            if (cycle > 0 && !repeatAfterAllEvents)
            {
                Debug.Log("All 12 events have already been encountered.", this);
                return false;
            }

            FillAndShuffle();
        }

        int last = remainingEvents.Count - 1;
        int prefabIndex = remainingEvents[last];

        // Prefabs must be inactive, so their scripts do not initialize
        // until the Dungeon references have been assigned.
        GameObject instance = Instantiate(
            eventPrefabs[prefabIndex],
            runtimeEvents
        );

        instance.name =
            eventPrefabs[prefabIndex].name + "_Cycle" + cycle;

        if (!ConnectEvent(instance))
        {
            Destroy(instance);
            return false;
        }

        previousTimeScale = Time.timeScale;
        previousCursorLock = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        activeEvent = instance;
        eventOpen = true;

        pausedComponents.Clear();
        PauseComponents<PlayerMovement>();
        PauseComponents<MouseLook>();
        PauseComponents<EnemyEncounter>();
        PauseComponents<DungeonManager>();
        PauseComponents<WorldItemPickup>();

        // InventoryToggle remains available so events that require
        // selecting or rearranging items can use the real backpack.
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        remainingEvents.RemoveAt(last);

        instance.SetActive(true);

        Debug.Log(
            $"Event cycle {cycle}, encounter "
            + $"{12 - remainingEvents.Count}/12: "
            + eventPrefabs[prefabIndex].name,
            this
        );

        return true;
    }

    void FillAndShuffle()
    {
        remainingEvents.Clear();

        for (int i = 0; i < eventPrefabs.Length; i++)
            remainingEvents.Add(i);

        for (int i = remainingEvents.Count - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);

            int value = remainingEvents[i];
            remainingEvents[i] = remainingEvents[j];
            remainingEvents[j] = value;
        }

        cycle++;
    }

    bool ConnectEvent(GameObject instance)
    {
        MonoBehaviour controller = null;
        FieldInfo returnField = null;

        // Compatibility adapter for our existing event scripts.
        // They all expose the same serialized exit callback name.
        foreach (MonoBehaviour script in
            instance.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!script) continue;

            FieldInfo field =
                script.GetType().GetField("onReturnToMap", Fields);

            if (field == null || field.FieldType != typeof(UnityEvent))
                continue;

            if (controller)
            {
                Debug.LogError(
                    $"{instance.name}: found more than one event controller.",
                    this
                );
                return false;
            }

            controller = script;
            returnField = field;
        }

        if (!controller)
        {
            Debug.LogError(
                $"{instance.name}: event controller was not found.",
                this
            );
            return false;
        }

        Type type = controller.GetType();

        AssignOptionalField(
            controller, type, "inventory", typeof(InventoryGrid), inventory);

        AssignOptionalField(
            controller, type, "inventoryToggle",
            typeof(InventoryToggle), inventoryToggle);

        var returnEvent = new UnityEvent();
        returnEvent.AddListener(() => CloseEventInstance(instance));
        returnField.SetValue(controller, returnEvent);

        FieldInfo deathField = type.GetField("onGameOver", Fields);

        if (deathField != null
            && deathField.FieldType == typeof(UnityEvent))
        {
            var deathEvent = new UnityEvent();
            deathEvent.AddListener(() => HandleEventDeath(instance));
            deathField.SetValue(controller, deathEvent);
        }

        AnonymousDeliveryEvent delivery =
            controller as AnonymousDeliveryEvent;

        if (delivery)
            deliveryControllers.Add(delivery);

        return true;
    }

    static void AssignOptionalField(
        MonoBehaviour controller,
        Type type,
        string fieldName,
        Type expectedType,
        object value)
    {
        FieldInfo field = type.GetField(fieldName, Fields);

        if (field != null && field.FieldType == expectedType)
            field.SetValue(controller, value);
    }

    public void CloseEvent()
    {
        CloseEventInstance(activeEvent);
    }

    void CloseEventInstance(GameObject instance)
    {
        if (!eventOpen || gameOver || instance != activeEvent)
            return;

        inventoryToggle.CloseInventory();

        // Hide the UI, but DO NOT deactivate or destroy the controller.
        // Package progress and discard detection can continue running.
        foreach (Canvas canvas in
            instance.GetComponentsInChildren<Canvas>(true))
        {
            canvas.enabled = false;
        }

        activeEvent = null;
        RestoreExploration();
    }

    void HandleEventDeath(GameObject instance)
    {
        if (!eventOpen || instance != activeEvent)
            return;

        gameOver = true;

        // Keep the current Game Over screen and exploration paused.
        // Connect the team's restart/menu UI here later.
        Debug.Log(
            "Event caused Game Over. Random events are now blocked.",
            this
        );
    }

    void HandleBattleEnded(bool won)
    {
        if (!won || gameOver)
            return;

        battleNumber++;
        string battleId = "dungeon-battle-" + battleNumber;

        for (int i = deliveryControllers.Count - 1; i >= 0; i--)
        {
            AnonymousDeliveryEvent delivery = deliveryControllers[i];

            if (!delivery)
            {
                deliveryControllers.RemoveAt(i);
                continue;
            }

            if (delivery.HasActiveDelivery)
                delivery.ReportBattleVictory(battleId);
        }
    }

    void PauseComponents<T>() where T : Behaviour
    {
        foreach (T component in UnityEngine.Object.FindObjectsByType<T>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None))
        {
            pausedComponents.Add(new PausedComponent
            {
                component = component,
                wasEnabled = component.enabled,
                isMouseLook = component is MouseLook,
                savedRotation = component.transform.localRotation
            });

            component.enabled = false;
        }
    }

    void KeepExplorationPaused()
    {
        // Inventory open/close code may re-enable movement components.
        // Keep them paused until this event actually ends.
        foreach (PausedComponent saved in pausedComponents)
        {
            if (!saved.component) continue;

            saved.component.enabled = false;

            if (saved.isMouseLook)
            {
                saved.component.transform.localRotation =
                    saved.savedRotation;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    void RestoreExploration()
    {
        Time.timeScale = previousTimeScale;

        foreach (PausedComponent saved in pausedComponents)
        {
            if (saved.component)
                saved.component.enabled = saved.wasEnabled;
        }

        pausedComponents.Clear();

        Cursor.lockState = previousCursorLock;
        Cursor.visible = previousCursorVisible;

        eventOpen = false;
    }

    void OnDisable()
    {
        if (!eventOpen)
            return;

        if (activeEvent)
        {
            foreach (Canvas canvas in
                activeEvent.GetComponentsInChildren<Canvas>(true))
            {
                canvas.enabled = false;
            }
        }

        RestoreExploration();
    }

    void OnDestroy()
    {
        if (battleManager)
            battleManager.OnBattleEnded -= HandleBattleEnded;
    }
}