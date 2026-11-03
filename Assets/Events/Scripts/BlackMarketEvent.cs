using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BlackMarketEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button tradeButton;
    [SerializeField] Button buyButton;
    [SerializeField] Button leaveButton;

    [Header("Connect to the map later")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    bool ready;
    bool closed;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && tradeButton
            && buyButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "BlackMarketEvent: assign all Event UI fields.",
                this
            );

            if (tradeButton) tradeButton.interactable = false;
            if (buyButton) buyButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        closed = false;

        eventPanel.SetActive(true);

        titleText.text = "Black Market Dealer";

        descriptionText.text =
            "Behind a curtain of tangled cables, a dealer "
            + "displays unregistered equipment.\n"
            + "\"Upgrades, no questions asked.\"";

        // No placeholder money or status values.
        statusText.text = "";

        tradeButton.gameObject.SetActive(true);
        buyButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        // Shell only: trading and buying are not connected yet.
        tradeButton.interactable = false;
        buyButton.interactable = false;

        leaveButton.interactable = true;
        leaveButton.onClick.AddListener(Leave);

        SetLabel(
            tradeButton,
            "Trade an item - Same category, 1-2 tiles larger"
        );

        SetLabel(
            buyButton,
            "Buy a random cursed item - Half price"
        );

        SetLabel(leaveButton, "Leave");
    }

    public void Leave()
    {
        if (!ready || closed)
            return;

        closed = true;
        eventPanel.SetActive(false);

        // Without a map callback, this reveals the test scene.
        onReturnToMap.Invoke();
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}