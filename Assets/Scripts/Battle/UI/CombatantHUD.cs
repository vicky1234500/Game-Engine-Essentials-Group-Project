using System.Text;
using UnityEngine;
using UnityEngine.UI;

// HP bar, shield, statuses and (for enemies) the intent of the next move.
// Player: put on a fixed panel in the Canvas and assign the PlayerCombatant.
// Enemy: put on a World Space canvas that is a child of the enemy prefab (it finds the enemy itself).
public class CombatantHUD : MonoBehaviour
{
    [SerializeField] Combatant target;
    [SerializeField] Text nameText;
    [SerializeField] RectTransform hpFill;    // the coloured bar inside the HP bar (anchored left, stretched)
    [SerializeField] Text hpText;
    [SerializeField] GameObject shieldGroup;
    [SerializeField] Text shieldText;
    [SerializeField] Text statusText;       // simple text for now, TODO: status icons
    [SerializeField] Text intentText;       // enemy only, e.g. "Attack 6"
    [SerializeField] bool faceCamera = true;

    Camera cam;

    void Start()
    {
        cam = Camera.main;
        if (!target) target = GetComponentInParent<Combatant>();
        if (target) target.OnChanged += Refresh;
        Refresh();
    }

    void OnDestroy()
    {
        if (target) target.OnChanged -= Refresh;
    }

    void LateUpdate()
    {
        if (faceCamera && cam && GetComponentInParent<EnemyCombatant>())
            transform.rotation = cam.transform.rotation;
    }

    void Refresh()
    {
        if (!target) return;

        if (nameText) nameText.text = target.DisplayName;
        if (hpFill)
        {
            float pct = target.MaxHP > 0 ? Mathf.Clamp01((float)target.HP / target.MaxHP) : 0f;
            hpFill.anchorMin = new Vector2(0, 0);
            hpFill.anchorMax = new Vector2(pct, 1);
            hpFill.offsetMin = hpFill.offsetMax = Vector2.zero;
        }
        if (hpText) hpText.text = target.Defense > 0 ? $"{target.HP}/{target.MaxHP}   DF {target.Defense}" : $"{target.HP}/{target.MaxHP}";
        if (shieldGroup) shieldGroup.SetActive(target.Shield > 0);
        if (shieldText) shieldText.text = target.Shield.ToString();

        if (statusText)
        {
            var sb = new StringBuilder();
            foreach (var s in target.Statuses.All)
                sb.Append($"{s.type} {s.amount} ({s.turnsLeft})  ");
            statusText.text = sb.ToString();
        }

        if (intentText)
        {
            var enemy = target as EnemyCombatant;
            intentText.text = enemy && enemy.NextMove != null ? DescribeIntent(enemy.NextMove, enemy) : "";
        }

        if (target.IsDead && target is EnemyCombatant) gameObject.SetActive(false);
    }

    // e.g. "Attack 9", "Attack 3x4", "Wind Up". Damage includes the difficulty multiplier.
    static string DescribeIntent(EnemyMove m, EnemyCombatant enemy)
    {
        if (m.intent == IntentType.Attack)
        {
            int total = 0, hits = 1;
            foreach (var e in m.effects)
                if (e.type == EffectType.Damage) { total = e.amount; hits = e.hits; }
            total = Mathf.RoundToInt(total * (enemy ? enemy.DamageMultiplier : 1f));
            return hits > 1 ? $"Attack {total}x{hits}" : $"Attack {total}";
        }
        return string.IsNullOrEmpty(m.moveName) ? m.intent.ToString() : m.moveName;
    }
}
