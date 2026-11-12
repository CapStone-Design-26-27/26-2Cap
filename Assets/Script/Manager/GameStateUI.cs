using TMPro;
using UnityEngine;

// Aset/UI/GameStateUI 프리팹의 표시만 관리한다. 입력 정책은 StageManager가 관리한다.
public class GameStateUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI stateText;
    private int shownSeconds;
    private float popStarted;

    private void Update()
    {
        StageManager stage = StageManager.Inst;
        GameManager game = GameManager.Inst;
        bool visible = stage != null && game != null && !game.gameOver &&
            stage.IsStageReady && !stage.IsCleared && stage.IsSettling;
        panel.SetActive(visible);
        if (!visible)
        {
            shownSeconds = 0;
            return;
        }
        if (shownSeconds != stage.CountdownSeconds)
        {
            shownSeconds = stage.CountdownSeconds;
            stateText.SetText("{0}", shownSeconds);
            popStarted = Time.unscaledTime;
        }
        float progress = Mathf.Clamp01((Time.unscaledTime - popStarted) / .22f);
        stateText.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.24f, 1f, Mathf.SmoothStep(0f, 1f, progress));
    }
}
