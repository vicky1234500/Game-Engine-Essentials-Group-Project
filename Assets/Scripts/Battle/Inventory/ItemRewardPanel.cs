using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// After winning a fight: "Choose 1 of 3 items".
// - Fits in the backpack -> added straight away.
// - Backpack full        -> dropped on the floor in front of the player as a world pickup.
//                           (WorldItemPickup shows "Storage full" and picks it up once there's space.)
// Put on the Victory panel. It rolls new choices every time the panel is shown.
public class ItemRewardPanel : MonoBehaviour
{
    [SerializeField] InventoryGrid inventory;                 // found automatically if empty
    [SerializeField] List<ItemData> rewardPool = new List<ItemData>();
    [SerializeField] int choices = 3;

    [Header("UI")]
    [SerializeField] Button[] choiceButtons;
    [SerializeField] Text[] choiceTexts;
    [SerializeField] Button skipButton;
    [SerializeField] Text messageText;

    [Header("Backpack full -> drop on the floor")]
    [SerializeField] WorldItemPickup dropPrefab;
    [SerializeField] Transform player;
    [SerializeField] float dropDistance = 1.5f;

    [SerializeField] float delayAfterPick = 1.2f;              // show the message, then go back to exploring

    readonly List<ItemData> rolled = new List<ItemData>();
    bool picked;

    void OnEnable()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryGrid>(FindObjectsInactive.Include);
        picked = false;
        if (messageText) messageText.text = "";
        Roll();
    }

    void Roll()
    {
        rolled.Clear();
        var pool = new List<ItemData>(rewardPool);
        pool.RemoveAll(i => i == null);
        for (int i = 0; i < choices && pool.Count > 0; i++)
        {
            int r = Random.Range(0, pool.Count);
            rolled.Add(pool[r]);
            pool.RemoveAt(r);
        }

        for (int i = 0; i < choiceButtons.Length; i++)
        {
            bool used = i < rolled.Count;
            choiceButtons[i].gameObject.SetActive(used);
            choiceButtons[i].onClick.RemoveAllListeners();
            choiceButtons[i].interactable = true;
            if (!used) continue;

            int index = i;                                     // copy for the lambda
            choiceButtons[i].onClick.AddListener(() => Choose(index));
            if (i < choiceTexts.Length && choiceTexts[i]) choiceTexts[i].text = Describe(rolled[i]);
            var img = choiceButtons[i].targetGraphic as Image;
            if (img) { var c = rolled[i].tint; c.a = 0.85f; img.color = c; }
        }

        if (skipButton) skipButton.interactable = true;
    }

    static string Describe(ItemData item)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{item.itemName}</b>  ({item.size.x}x{item.size.y})\n");
        foreach (var e in item.cards)
            if (e.card) sb.Append($"{e.count}x {e.card.cardName}   ");
        if (item.passives != null)
            foreach (var p in item.passives)
                sb.Append($"\nPassive: {p.type} +{p.amount}");
        return sb.ToString();
    }

    void Choose(int index)
    {
        if (picked) return;
        picked = true;
        var item = rolled[index];

        if (inventory && inventory.TryAddItem(item))
        {
            Say($"{item.itemName} added to your backpack!");
        }
        else
        {
            Drop(item);
            Say($"Backpack full! {item.itemName} was dropped in front of you.\nPress 0, trash something, then walk over it.");
        }

        foreach (var b in choiceButtons) b.interactable = false;
        if (skipButton) skipButton.interactable = false;
        StartCoroutine(BackToExploring(delayAfterPick));
    }

    void Drop(ItemData item)
    {
        if (!dropPrefab || !player) { Debug.LogWarning("ItemRewardPanel: no drop prefab/player, reward lost."); return; }

        Vector3 forward = player.forward; forward.y = 0; forward.Normalize();
        Vector3 pos = player.position + forward * dropDistance;
        var pickup = Instantiate(dropPrefab, pos, Quaternion.identity);
        pickup.name = "Dropped_" + item.itemName;
        pickup.SetItem(item);

        var r = pickup.GetComponentInChildren<Renderer>();
        if (r) r.material.color = item.tint;
    }

    void Say(string msg)
    {
        if (messageText) messageText.text = msg;
    }

    IEnumerator BackToExploring(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (BattleTransition.Instance) BattleTransition.Instance.ReturnToExploration();
    }
}
