using UnityEngine;
using UnityEngine.SceneManagement;

public class EventSceneExit : MonoBehaviour
{
    [Header("Destination")]
    [SerializeField] string destinationScene = "Dungeon";

    bool loading;

    public void GoToDungeon()
    {
        if (loading)
            return;

        if (!Application.CanStreamedLevelBeLoaded(destinationScene))
        {
            Debug.LogError(
                $"Cannot load scene '{destinationScene}'. "
                + "Check that it is enabled in the build scene list.",
                this
            );
            return;
        }

        loading = true;

        Time.timeScale = 1f;

        SceneManager.LoadScene(
            destinationScene,
            LoadSceneMode.Single
        );
    }
}