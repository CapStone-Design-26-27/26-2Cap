using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class PlaySettings : MonoBehaviour
{
    [SerializeField] private GameObject overlay;
    [SerializeField] private GameObject settingsButton;
    [SerializeField] private TMP_Text musicLabel;
    [SerializeField] private TMP_Text effectsLabel;
    [SerializeField] private TMP_Text vibrationLabel;
    [SerializeField] private AudioSource music;

    internal static bool VibrationEnabled => PlayerPrefs.GetInt("VibrationEnabled", 1) != 0;
    internal static bool EffectsEnabled => PlayerPrefs.GetInt("SoundEffects", 1) != 0;

    private GameManager pausedGame;
    private bool previousPause;
    private float previousTimeScale;

    private void Start() => RefreshAudio();

    private void Update()
    {
        var stage = StageManager.Inst;
        bool countingDown = stage != null && stage.IsStageReady && stage.IsSettling &&
            !stage.IsCleared && GameManager.Inst != null && !GameManager.Inst.gameOver;
        if (settingsButton != null && settingsButton.activeSelf == countingDown)
            settingsButton.SetActive(!countingDown);
    }

    public void ToggleSettings()
    {
        if (overlay == null) return;
        if (overlay.activeSelf)
        {
            Resume();
            return;
        }
        if (GameManager.Inst == null || StageManager.Inst?.IsSettling == true) return;
        pausedGame = GameManager.Inst;
        previousPause = pausedGame.gamePaused;
        previousTimeScale = Time.timeScale;
        pausedGame.gamePaused = true;
        Time.timeScale = 0f;
        SpawnManager.Inst?.CancelAim();
        overlay.SetActive(true);
    }

    public void Resume()
    {
        if (overlay != null) overlay.SetActive(false);
        RestorePause();
    }

    public void GoHome()
    {
        Resume();
        if (StageManager.Inst != null) SaveManager.Inst?.SaveGame();
        SceneFlow.LoadMain();
    }

    public void ReplayTutorial()
    {
        Resume();
        if (StageManager.Inst != null) SaveManager.Inst?.SaveGame();
        SceneFlow.LoadTutorial();
    }

    public void ToggleMusic()
    {
        Toggle("MusicEnabled");
        RefreshAudio();
    }

    public void ToggleEffects()
    {
        Toggle("SoundEffects");
        if (effectsLabel != null) effectsLabel.text = Label("SFX", EffectsEnabled);
    }

    public void ToggleVibration()
    {
        Toggle("VibrationEnabled");
        if (vibrationLabel != null) vibrationLabel.text = Label("VIBRATION", VibrationEnabled);
    }

    private void RefreshAudio()
    {
        bool enabled = PlayerPrefs.GetInt("MusicEnabled", 1) != 0;
        if (musicLabel != null) musicLabel.text = Label("BGM", enabled);
        effectsLabel.text = Label("SFX", EffectsEnabled);
        vibrationLabel.text = Label("VIBRATION", VibrationEnabled);
        if (music == null) return;
        if (enabled && !music.isPlaying) music.Play();
        else if (!enabled) music.Stop();
    }

    private static void Toggle(string key)
    {
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 1) != 0 ? 0 : 1);
        PlayerPrefs.Save();
    }

    private static string Label(string name, bool enabled) => name + (enabled ? "  ON" : "  OFF");

    private void OnDisable() => RestorePause();

    private void RestorePause()
    {
        if (ReferenceEquals(pausedGame, null)) return;
        if (pausedGame != null) pausedGame.gamePaused = previousPause;
        Time.timeScale = previousTimeScale;
        pausedGame = null;
    }
}
