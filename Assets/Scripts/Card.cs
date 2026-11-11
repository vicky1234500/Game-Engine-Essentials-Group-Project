using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using TMPro;

public class Card : MonoBehaviour
{
    public CardData cardData;

    [Header("Card Information")]
    public Transform modelHolder;
    public TMP_Text cardName;
    public TMP_Text cardType;
    public TMP_Text manaCost;
    public TMP_Text value;
    public TMP_Text description;
    public TMP_Text cardNum;

    [Header("Model")]
    public Transform models;

    CardViewBase battleView;
    CardInstance lastInstance;
    CardData lastData;
    int lastDamageBonus;
    bool displayInitialized;

    // Matches direct damage text, but not:
    // "Deal 3 damage for every attack card..."
    static readonly Regex DirectDamageText = new Regex(
        @"\bDeal\s+\d+\s+damage\b(?!\s+for\s+every)",
        RegexOptions.IgnoreCase);

    static readonly Regex StatusDamageText = new Regex(
        @"Deal damage equal to \d+x the enemy's [A-Za-z_]+\.",
        RegexOptions.IgnoreCase);

    static readonly Regex AttackCountDamageText = new Regex(
        @"Deal \d+ damage for every attack card played this fight\.",
        RegexOptions.IgnoreCase);

    public void Setup(
        CardData data,
        GameObject sharedModelPrefab,
        int count)
    {
        cardData = data;
        displayInitialized = false;
        battleView = GetComponentInParent<CardViewBase>();

        if (!cardData)
        {
            Debug.LogError("CardData is null!", this);
            return;
        }

        if (cardName)
            cardName.text = cardData.cardName;

        if (cardType)
            cardType.text = cardData.type.ToString();

        if (manaCost)
            manaCost.text = cardData.manaCost.ToString();

        if (cardNum)
            cardNum.text = count.ToString();

        RefreshBattleText();

        if (!models)
        {
            Debug.LogError("Card Models is not assigned!", this);
            return;
        }

        if (sharedModelPrefab)
        {
            GameObject spawnedModel =
                Instantiate(sharedModelPrefab, models);

            spawnedModel.transform.localPosition = Vector3.zero;
            spawnedModel.transform.localRotation = Quaternion.identity;
            spawnedModel.transform.localScale = Vector3.one;

            NormalizeAndFitModel(spawnedModel);
        }
    }

    void LateUpdate()
    {
        if (!cardData)
            return;

        RefreshBattleText();
    }

    void RefreshBattleText()
    {
        if (!battleView)
            battleView = GetComponentInParent<CardViewBase>();

        CardInstance instance = battleView ? battleView.Card : null;

        // Inventory/shop previews have no CardInstance.
        // They continue showing the original asset description.
        if (instance != null && instance.data != cardData)
            instance = null;

        int bonus = instance != null ? instance.bonusDamage : 0;

        if (displayInitialized
            && instance == lastInstance
            && cardData == lastData
            && bonus == lastDamageBonus)
        {
            return;
        }

        displayInitialized = true;
        lastInstance = instance;
        lastData = cardData;
        lastDamageBonus = bonus;

        if (description)
            description.text = BuildDescription(cardData, bonus);

        if (value)
            value.text = BuildValue(cardData, bonus);
    }

    static string BuildValue(CardData data, int bonus)
    {
        List<CardEffect> effects = data.GetEffects();

        // Formula-based attacks do not have a fixed damage number.
        foreach (CardEffect effect in effects)
        {
            if (effect == null)
                continue;

            if (effect.type == EffectType.DamagePerStatus
                || effect.type == EffectType.DamagePerAttackPlayed)
            {
                return "X";
            }
        }

        // Read actual damage effects instead of the legacy value field.
        List<string> amounts = new List<string>();

        foreach (CardEffect effect in effects)
        {
            if (effect == null || effect.type != EffectType.Damage)
                continue;

            int amount = Mathf.Max(0, effect.amount + bonus);

            amounts.Add(effect.hits > 1
                ? $"{amount} x{effect.hits}"
                : amount.ToString());
        }

        return amounts.Count > 0
            ? string.Join(" / ", amounts)
            : data.value.ToString();
    }

    static string BuildDescription(CardData data, int bonus)
    {
        string text = data.description ?? "";

        if (bonus == 0)
            return text;

        List<CardEffect> directEffects = new List<CardEffect>();
        List<string> unmatchedDamage = new List<string>();

        foreach (CardEffect effect in data.GetEffects())
        {
            if (effect == null)
                continue;

            if (effect.type == EffectType.Damage)
            {
                directEffects.Add(effect);
            }
            else if (effect.type == EffectType.DamagePerStatus)
            {
                string formula =
                    $"Deal damage equal to "
                    + $"({effect.amount} x target's "
                    + $"{effect.status.ToString().ToUpperInvariant()})"
                    + SignedBonus(bonus)
                    + ". Minimum 0. Requires that status.";

                if (StatusDamageText.IsMatch(text))
                    text = StatusDamageText.Replace(text, formula, 1);
                else
                    unmatchedDamage.Add(formula);
            }
            else if (effect.type == EffectType.DamagePerAttackPlayed)
            {
                string formula =
                    $"Deal damage equal to "
                    + $"({effect.amount} x attack cards played this fight)"
                    + SignedBonus(bonus)
                    + ". Minimum 0.";

                if (AttackCountDamageText.IsMatch(text))
                    text = AttackCountDamageText.Replace(text, formula, 1);
                else
                    unmatchedDamage.Add(formula);
            }
        }

        int index = 0;

        text = DirectDamageText.Replace(text, match =>
        {
            if (index >= directEffects.Count)
                return match.Value;

            CardEffect effect = directEffects[index++];
            int amount = Mathf.Max(0, effect.amount + bonus);

            // Preserve the rest of the sentence:
            // hit count, all-enemy targeting, status text, etc.
            return $"Deal {amount} damage";
        });

        // Future cards may use a different description format.
        // Show their calculated attack separately rather than
        // replacing unrelated numbers in the description.
        while (index < directEffects.Count)
        {
            CardEffect effect = directEffects[index++];
            int amount = Mathf.Max(0, effect.amount + bonus);

            string line = $"Modified direct damage: {amount}";

            if (effect.hits > 1)
                line += $" x {effect.hits} hits";

            line += $" ({effect.target}).";
            unmatchedDamage.Add(line);
        }

        if (unmatchedDamage.Count > 0)
        {
            text += "\n" + string.Join("\n", unmatchedDamage);
        }

        return text;
    }

    static string SignedBonus(int amount)
    {
        return amount >= 0
            ? $" + {amount}"
            : $" - {Mathf.Abs(amount)}";
    }

    void NormalizeAndFitModel(GameObject modelObj)
    {
        Renderer[] renderers =
            modelObj.GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
            return;

        Bounds combinedBounds = renderers[0].bounds;

        foreach (Renderer renderer in renderers)
            combinedBounds.Encapsulate(renderer.bounds);

        Vector3 localCenter =
            modelObj.transform.InverseTransformPoint(combinedBounds.center);

        modelObj.transform.localPosition -= localCenter;

        float maxDimension = Mathf.Max(
            combinedBounds.size.x,
            Mathf.Max(combinedBounds.size.y, combinedBounds.size.z));

        if (maxDimension > 0f)
            modelObj.transform.localScale *= 1f / maxDimension;
    }
}