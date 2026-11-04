using UnityEngine;
using UnityEngine.EventSystems;

// Lets the player click an enemy to target it.
// Needs: a Collider on the enemy, a Physics Raycaster on the Main Camera, and an EventSystem in the scene.
[RequireComponent(typeof(Collider))]
public class EnemyClickTarget : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] GameObject highlight;      // optional ring / outline under the enemy

    EnemyCombatant enemy;

    void Awake()
    {
        enemy = GetComponentInParent<EnemyCombatant>();
        if (highlight) highlight.SetActive(false);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (enemy && !enemy.IsDead && BattleUI.Instance) BattleUI.Instance.OnEnemyClicked(enemy);
    }

    public void OnPointerEnter(PointerEventData e)
    {
        if (highlight && BattleUI.Instance && BattleUI.Instance.SelectedCard) highlight.SetActive(true);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (highlight) highlight.SetActive(false);
    }
}
