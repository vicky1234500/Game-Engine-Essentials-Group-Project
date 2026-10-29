using UnityEngine;
using UnityEngine.UI;

// Put this on a legacy UI Text directly under Canvas (must stay active, NOT inside InGame or InventoryMenu).
// Shows messages like "Picked up Knife", then fades out.
[RequireComponent(typeof(Text))]
public class PickupNotifier : MonoBehaviour
{
    public static PickupNotifier Instance { get; private set; }

    [SerializeField] float duration = 2.5f;   // total seconds on screen
    [SerializeField] float fadeTime = 0.5f;   // last part of the duration spent fading out

    Text label;
    Color baseColor;
    float hideAt;

    void Awake()
    {
        Instance = this;
        label = GetComponent<Text>();
        baseColor = label.color;
        label.enabled = false;   // hide the text but keep the script running
    }

    // Call from anywhere: PickupNotifier.Show("Picked up Knife");
    public static void Show(string message)
    {
        if (Instance) Instance.Display(message);
        else Debug.Log(message);   // fallback if there's no notifier in the scene
    }

    void Display(string message)
    {
        label.text = message;
        label.color = baseColor;
        label.enabled = true;
        hideAt = Time.time + duration;   // a new message restarts the timer
    }

    void Update()
    {
        if (!label.enabled) return;

        float timeLeft = hideAt - Time.time;
        if (timeLeft <= 0f)
        {
            label.enabled = false;
            return;
        }

        if (timeLeft < fadeTime)
        {
            Color c = baseColor;
            c.a *= timeLeft / fadeTime;
            label.color = c;
        }
    }
}