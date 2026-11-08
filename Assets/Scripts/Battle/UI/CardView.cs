using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// One card in the hand. Make a UI prefab with: background Image, name, cost, description texts.
public class CardView : CardViewBase, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Image background;
    [SerializeField] Image itemIcon;            // icon of the item this card came from
    [SerializeField] Text nameText;
    [SerializeField] Text costText;
    [SerializeField] Text descriptionText;
    [SerializeField] GameObject selectedOutline;

    [Header("Colours by type")]
    [SerializeField] Color attackColor = new Color(0.83f, 0.14f, 0.23f);
    [SerializeField] Color defenseColor = new Color(0.45f, 0.3f, 0.8f);
    [SerializeField] Color healColor = new Color(0.25f, 0.7f, 0.35f);
    [SerializeField] Color skillColor = new Color(0.22f, 0.54f, 0.87f);
    [SerializeField] Color curseColor = new Color(0.2f, 0.2f, 0.2f);
    [SerializeField] float hoverRaise = 30f;

    public void Setup(CardInstance card)
    {
        Card = card;
        var d = card.data;
        if (nameText) nameText.text = d.cardName;
        if (costText) costText.text = d.unplayable ? "-" : d.manaCost.ToString();
        if (descriptionText) descriptionText.text = d.description;
        if (itemIcon)
        {
            itemIcon.enabled = card.sourceItem && card.sourceItem.icon;
            if (itemIcon.enabled) itemIcon.sprite = card.sourceItem.icon;
        }
        if (background) background.color = ColorFor(d.type);
        SetSelected(false);
    }

    Color ColorFor(CardType t)
    {
        switch (t)
        {
            case CardType.Attack:  return attackColor;
            case CardType.Defense: return defenseColor;
            case CardType.Heal:    return healColor;
            case CardType.Curse:   return curseColor;
            default:               return skillColor;
        }
    }

    public override void SetSelected(bool selected)
    {
        if (selectedOutline) selectedOutline.SetActive(selected);
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (BattleUI.Instance) BattleUI.Instance.OnCardClicked(this);
    }

    // TODO: drag-to-play instead of click, and play animation "cards into real stuff".
    public void OnPointerEnter(PointerEventData e) => transform.localPosition += Vector3.up * hoverRaise;
    public void OnPointerExit(PointerEventData e) => transform.localPosition -= Vector3.up * hoverRaise;
}
