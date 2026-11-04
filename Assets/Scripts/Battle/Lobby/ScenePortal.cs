using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Walk into it to load another scene (Lobby -> BattleTest, end of the hallway -> Lobby).
// Needs a Collider with Is Trigger checked. The scene must be in File > Build Profiles (Scene List).
[RequireComponent(typeof(Collider))]
public class ScenePortal : MonoBehaviour
{
    [SerializeField] string sceneName = "BattleTest";
    [Tooltip("Lobby portal: starts a fresh run (credits back to 0).")]
    [SerializeField] bool startsNewRun = true;
    [SerializeField] bool requireCharacter = true;
    [SerializeField] string playerTag = "Player";

    [Header("Optional")]
    [SerializeField] CanvasGroup fadeScreen;          // black full-screen image
    [SerializeField] float fadeTime = 0.4f;
    [SerializeField] Text messageText;                // "Choose a character first"
    [SerializeField] float spinSpeed = 40f;           // slowly spins the portal visual
    [SerializeField] Transform visual;

    bool loading;

    void Update()
    {
        if (visual) visual.Rotate(0f, 0f, spinSpeed * Time.deltaTime, Space.Self);
    }

    void OnTriggerEnter(Collider other)
    {
        if (loading || !other.CompareTag(playerTag)) return;

        if (requireCharacter && !RunState.selectedCharacter)
        {
            if (messageText) messageText.text = "Choose a character at the terminal first (press E)";
            return;
        }

        loading = true;
        if (startsNewRun) RunState.StartNewRun();
        StartCoroutine(Load());
    }

    IEnumerator Load()
    {
        if (fadeScreen)
        {
            fadeScreen.blocksRaycasts = true;
            for (float t = 0; t < fadeTime; t += Time.unscaledDeltaTime)
            {
                fadeScreen.alpha = t / fadeTime;
                yield return null;
            }
            fadeScreen.alpha = 1;
        }
        SceneManager.LoadScene(sceneName);
    }
}
