using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BackpackTailorEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button payButton;
    [SerializeField] Button tradeButton;
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
            && payButton
            && tradeButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "BackpackTailorEvent: assign all Event UI fields.",
                this
            );

            if (payButton) payButton.interactable = false;
            if (tradeButton) tradeButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        closed = false;
        eventPanel.SetActive(true);

        titleText.text = "Backpack Tailor";

        descriptionText.text =
            "A street vendor examines your gear rig.\n"
            + "\"Extra pockets, reinforced straps. "
            + "Pay in credits, or give me something worth salvaging.\"";

        statusText.text = "";

        payButton.gameObject.SetActive(true);
        tradeButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        // Shell only. Backpack expansion is not connected yet.
        payButton.interactable = false;
        tradeButton.interactable = false;

        leaveButton.interactable = true;
        leaveButton.onClick.AddListener(Leave);

        SetLabel(
            payButton,
            "Pay 50 credits - Add 2 backpack tiles"
        );

        SetLabel(
            tradeButton,
            "Trade an item of 3+ tiles - Add 3 backpack tiles"
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