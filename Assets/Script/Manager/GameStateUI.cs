using TMPro;
using UnityEngine;

// Aset/UI/GameStateUI 프리팹의 표시만 관리한다. 입력 정책은 StageManager가 관리한다.
public class GameStateUI : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private TextMeshProUGUI stateText;

    private void Update()
    {
        StageManager stage = StageManager.Inst;
        GameManager game = GameManager.Inst;
        bool visible = stage != null && game != null && !game.gameOver &&
            stage.IsStageReady && !stage.IsCleared && (stage.IsSettling || stage.IsCheckingClear);
        panel.SetActive(visible);
        if (!visible) return;
        stateText.text = stage.IsSettling
            ? $"READY\n<size=120>{stage.CountdownSeconds}</size>"
            : $"BALANCE\n<size=96>{stage.HoldProgress * 100f:F0}%</size>\n<size=32>{stage.ClearSecondsRemaining:F1}s</size>";
    }
}
