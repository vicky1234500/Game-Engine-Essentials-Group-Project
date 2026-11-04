using System.Text;
using UnityEngine;
using UnityEngine.UI;

// The "Choose your character" panel in the Lobby.
// Each slot shows a character; locked or empty slots show "Coming soon".
// While open: player movement + mouse look are off and the cursor is free.
public class CharacterSelectUI : MonoBehaviour
{
    [SerializeField] CharacterData[] characters;      // one per slot (empty = placeholder slot)

    [Header("UI")]
    [SerializeField] GameObject panel;
    [SerializeField] Button[] slotButtons;
    [SerializeField] Text[] slotTexts;
    [SerializeField] Image[] slotPortraits;
    [SerializeField] Button closeButton;
    [SerializeField] Text selectedLabel;              // HUD: "Character: Character 1"

    [Header("Turned off while the panel is open")]
    [SerializeField] Behaviour[] disableWhileOpen;    // PlayerMovement, MouseLook x2

    public bool IsOpen => panel && panel.activeSelf;

    void Start()
    {
        if (closeButton) closeButton.onClick.AddListener(Close);
        for (int i = 0; i < slotButtons.Length; i++)
        {
            int index = i;
            slotButtons[i].onClick.AddListener(() => Select(index));
        }

        // Nothing picked yet -> default to the first unlocked character, so the portal always works.
        if (!RunState.selectedCharacter)
            foreach (var c in characters)
                if (c && c.unlocked) { RunState.selectedCharacter = c; RunState.SetStartingCredits(); break; }

        RefreshSlots();
        SetOpen(false);
    }

    public void Open() => SetOpen(true);
    public void Close() => SetOpen(false);

    void SetOpen(bool open)
    {
        if (panel) panel.SetActive(open);
        foreach (var b in disableWhileOpen) if (b) b.enabled = !open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (open) RefreshSlots();
        UpdateLabel();
    }

    void Select(int index)
    {
        var c = index < characters.Length ? characters[index] : null;
        if (!c || !c.unlocked) return;
        RunState.selectedCharacter = c;
        RunState.SetStartingCredits();                 // each character starts with its own money
        RefreshSlots();
        Close();
    }

    void RefreshSlots()
    {
        for (int i = 0; i < slotButtons.Length; i++)
        {
            var c = i < characters.Length ? characters[i] : null;
            bool usable = c && c.unlocked;
            slotButtons[i].interactable = usable;

            if (i < slotTexts.Length && slotTexts[i])
                slotTexts[i].text = usable ? Describe(c, c == RunState.selectedCharacter) : "<b>???</b>\n\nComing soon";

            if (i < slotPortraits.Length && slotPortraits[i])
            {
                slotPortraits[i].sprite = usable ? c.portrait : null;
                slotPortraits[i].color = usable ? c.color : new Color(0.25f, 0.25f, 0.25f);
            }
        }
        UpdateLabel();
    }

    static string Describe(CharacterData c, bool selected)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{c.characterName}</b>{(selected ? "  (selected)" : "")}\n\n");
        sb.Append($"HP {c.maxHP}    DF {c.baseDefense}    Mana {c.maxMana}\n");
        sb.Append($"Credits {c.startingCredits}\n\n");
        sb.Append("Starts with:\n");
        foreach (var item in c.startingItems)
            if (item) sb.Append($"- {item.itemName}\n");
        if (!string.IsNullOrEmpty(c.description)) sb.Append($"\n{c.description}");
        return sb.ToString();
    }

    void UpdateLabel()
    {
        if (selectedLabel)
            selectedLabel.text = "Character: " + (RunState.selectedCharacter ? RunState.selectedCharacter.characterName : "none");
    }
}
