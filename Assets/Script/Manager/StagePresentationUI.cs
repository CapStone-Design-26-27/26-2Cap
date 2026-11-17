using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StagePresentationUI : MonoBehaviour
{
    [SerializeField] private TMP_Text status;
    [SerializeField] private TMP_Text stageTitle;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private Button undo;
    [SerializeField] private TMP_Text undoCount;
    [SerializeField] private Image undoBadge;
    [SerializeField] private Image undoIcon;
    [SerializeField] private Button retry;
    [SerializeField] private Button action;
    private RectTransform progressTrack;
    private float displayedProgress;
    private bool wasShowingClear;


    private void Awake()
    {
        if (progressFill != null) progressTrack = progressFill.parent as RectTransform;
        if (action != null) action.onClick.AddListener(() =>
        {
            var stage = StageManager.Inst;
            if (stage != null && stage.IsCleared && stage.HasNextStage) stage.NextStage();
        });
        if (undo != null) undo.onClick.AddListener(() => StageManager.Inst?.UndoLastDrop());
        if (retry != null) retry.onClick.AddListener(RequestRestart);
    }

    private void RequestRestart()
    {
        var stage = StageManager.Inst;
        if (stage != null && stage.IsStageReady && !stage.IsCleared && GameManager.Inst?.gamePaused == false) stage.RetryStage();
    }

    private void UpdateMeter(bool visible, bool clearMode, float target, bool reset)
    {
        if (progressTrack == null || progressFill == null) return;
        progressTrack.gameObject.SetActive(visible);
        if (!visible || reset || (clearMode && !wasShowingClear)) displayedProgress = 0f;
        wasShowingClear = clearMode;
        if (!visible) return;

        // At a constant target, this covers about 95% of the change in 0.3 seconds.
        target = Mathf.Clamp01(target);
        displayedProgress = Mathf.Lerp(displayedProgress, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        if (Mathf.Abs(displayedProgress - target) < .002f) displayedProgress = target;
        progressFill.gameObject.SetActive(displayedProgress > 0f);
        progressFill.anchorMax = new Vector2(displayedProgress, progressFill.anchorMax.y);
    }

    private void Update()
    {
        var stage = StageManager.Inst;
        var game = GameManager.Inst;
        if (stage == null || game == null) return;
        var basket = game.FocusedBasket;
        float balance = basket == null ? stage.OverallBalance : stage.GetBasketBalance(basket);
        bool failed = stage.IsAttemptFailed || game.gameOver;
        bool checking = stage.IsCheckingClear && !failed;
        bool showMeter = stage.IsStageReady && !failed;
        float progress = checking ? stage.HoldProgress : balance;
        if (stageTitle != null) stageTitle.text = $"STAGE {stage.Stage}";
        if (status != null) status.text = !stage.IsStageReady ? "LOADING" :
            stage.IsSettling ? "CLEAR 0%" :
            stage.IsCleared ? (stage.HasNextStage ? "CLEAR 100%" : "CAMPAIGN COMPLETE") :
            failed ? (stage.IsAttemptFailed ? "TRY AGAIN" : "GAME OVER") :
            checking ? $"CLEAR {Mathf.FloorToInt(progress * 100f)}%" :
            $"{(basket == null ? "BALANCE" : "BASKET")} {Mathf.FloorToInt(balance * 100f + .0001f)}%";
        UpdateMeter(showMeter, stage.IsSettling || checking || stage.IsCleared,
            stage.IsSettling ? 0f : stage.IsCleared ? 1f : progress, stage.IsSettling);

        if (undo != null) undo.interactable = stage.CanUndo;
        if (undoBadge != null) undoBadge.color = stage.CanUndo ? new Color32(255, 216, 77, 255) : new Color32(164, 174, 187, 255);
        if (undoIcon != null) undoIcon.color = stage.CanUndo ? Color.white : new Color32(224, 230, 238, 209);
        if (undoCount != null) undoCount.text = stage.FreeUndoCount < 0 ? string.Empty : stage.FreeUndoCount.ToString();
        if (undoBadge != null) undoBadge.transform.parent.gameObject.SetActive(stage.FreeUndoCount >= 0);
        if (retry != null) retry.interactable = stage.IsStageReady && !stage.IsCleared;
        if (retry != null && retry.interactable && Input.GetKeyDown(KeyCode.R))
        {
            RequestRestart();
            return;
        }
        if (action != null) action.gameObject.SetActive(stage.IsCleared && stage.HasNextStage);
    }
}
