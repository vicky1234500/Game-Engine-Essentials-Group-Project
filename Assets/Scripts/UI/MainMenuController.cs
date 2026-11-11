using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public const string SaveKey = "HasSave";
    public const string VolumeKey = "MasterVolume";

    [SerializeField] string firstScene = "Lobby";

    [Header("Buttons")]
    [SerializeField] Button startButton;
    [SerializeField] Button loadButton;
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;
    [SerializeField] Button backButton;

    [Header("Settings panel")]
    [SerializeField] GameObject settingsPanel;
    [SerializeField] Slider volumeSlider;
    [SerializeField] Toggle fullscreenToggle;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void ApplySavedVolume()
    {
        AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 0.8f);
    }

    void Awake()
    {
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        startButton.onClick.AddListener(StartGame);
        loadButton.onClick.AddListener(LoadGame);
        settingsButton.onClick.AddListener(() => settingsPanel.SetActive(true));
        quitButton.onClick.AddListener(QuitGame);
        backButton.onClick.AddListener(CloseSettings);
        volumeSlider.onValueChanged.AddListener(SetVolume);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);

        settingsPanel.SetActive(false);
        loadButton.interactable = PlayerPrefs.HasKey(SaveKey);
    }

    void Start()
    {
        volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(VolumeKey, 0.8f));
        fullscreenToggle.SetIsOnWithoutNotify(Screen.fullScreen);
    }

    void Update()
    {
        if (settingsPanel.activeSelf && Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            CloseSettings();
    }

    public void StartGame()
    {
        RunState.StartNewRun();
        RunState.selectedCharacter = null;
        SceneManager.LoadScene(firstScene);
    }

    public void LoadGame()
    {
        // TODO: restore a saved run here once a save system exists
        SceneManager.LoadScene(firstScene);
    }

    public void CloseSettings()
    {
        PlayerPrefs.Save();
        settingsPanel.SetActive(false);
    }

    public void SetVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat(VolumeKey, value);
    }

    public void SetFullscreen(bool on) { Screen.fullScreen = on; }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}