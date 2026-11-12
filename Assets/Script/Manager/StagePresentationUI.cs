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
    [SerializeField] private GameObject result;
    [SerializeField] private TMP_Text caption;
    [SerializeField] private Button action;
    [SerializeField] private TMP_Text actionLabel;
    [SerializeField] private Button home;
    public static bool IsActive { get; private set; }
    private Canvas canvas;
    private RectTransform progressTrack;
    private Image progressTrackImage;
    private Image progressFillImage;
    private float meterScale;
    private float displayedProgress;
    private bool wasShowingClear;

    private void OnEnable() => IsActive = true;
    private void OnDisable() => IsActive = false;

    private void Awake()
    {
        canvas = GetComponentInParent<Canvas>();
        progressTrack = (RectTransform)progressFill.parent;
        progressTrackImage = progressTrack.GetComponent<Image>();
        progressFillImage = progressFill.GetComponent<Image>();
        action.onClick.AddListener(() =>
        {
            var stage = StageManager.Inst;
            if (stage == null) return;
            if (stage.IsCleared) stage.NextStage();
            else RequestRestart();
        });
        home.onClick.AddListener(() => StageManager.Inst?.GoHome());
        undo.onClick.AddListener(() => StageManager.Inst?.UndoLastDrop());
        retry.onClick.AddListener(RequestRestart);
    }

    private void RequestRestart()
    {
        var stage = StageManager.Inst;
        if (stage != null && stage.IsStageReady && !stage.IsCleared) stage.RetryStage();
    }

    private void UpdateMeter(bool visible, bool clearMode, float target, bool reset)
    {
        progressTrack.gameObject.SetActive(visible);
        if (!visible || reset || (clearMode && !wasShowingClear)) displayedProgress = 0f;
        wasShowingClear = clearMode;
        if (!visible) return;

        float scale = Mathf.Max(canvas.scaleFactor, .01f);
        if (!Mathf.Approximately(scale, meterScale))
        {
            meterScale = scale;
            float height = 13f / scale;
            float statusBottom = status.rectTransform.anchoredPosition.y + status.rectTransform.rect.yMin;
            progressTrack.anchoredPosition = new Vector2(0f, statusBottom - 8f / scale);
            progressTrack.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            progressTrackImage.pixelsPerUnitMultiplier = progressTrackImage.sprite.rect.height / height;
            progressFillImage.pixelsPerUnitMultiplier = progressFillImage.sprite.rect.height / height;
        }

        // At a constant target, this covers about 95% of the change in 0.3 seconds.
        target = Mathf.Clamp01(target);
        displayedProgress = Mathf.Lerp(displayedProgress, target, 1f - Mathf.Exp(-10f * Time.unscaledDeltaTime));
        if (Mathf.Abs(displayedProgress - target) < .002f) displayedProgress = target;
        progressFill.gameObject.SetActive(displayedProgress > 0f);
        progressFill.anchorMax = new Vector2(displayedProgress, 1f);
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
        stageTitle.text = $"STAGE {stage.Stage}";
        status.text = !stage.IsStageReady ? "LOADING" :
            stage.IsSettling ? "CLEAR 0%" :
            stage.IsCleared ? "CLEAR 100%" :
            checking ? $"CLEAR {Mathf.FloorToInt(progress * 100f)}%" :
            $"{(basket == null ? "BALANCE" : "BASKET")} {Mathf.FloorToInt(balance * 100f + .0001f)}%";
        UpdateMeter(showMeter, stage.IsSettling || checking || stage.IsCleared,
            stage.IsSettling ? 0f : stage.IsCleared ? 1f : progress, stage.IsSettling);

        bool showResult = stage.IsCleared || failed;
        undo.interactable = stage.CanUndo;
        undoBadge.color = undo.interactable ? new Color32(255, 216, 77, 255) : new Color32(164, 174, 187, 255);
        undoIcon.color = undo.interactable ? Color.white : new Color32(224, 230, 238, 209);
        undoCount.text = stage.FreeUndoCount.ToString();
        retry.interactable = stage.IsStageReady && !stage.IsCleared;
        if (retry.interactable && Input.GetKeyDown(KeyCode.R))
        {
            RequestRestart();
            return;
        }
        result.SetActive(showResult);
        if (!showResult) return;
        caption.text = stage.IsCleared ? (stage.HasNextStage ? "STAGE CLEAR" : "CAMPAIGN COMPLETE") :
            (stage.IsAttemptFailed ? "TRY AGAIN" : "GAME OVER");
        action.gameObject.SetActive(!stage.IsCleared || stage.HasNextStage);
        actionLabel.text = stage.IsCleared ? "NEXT" : "RETRY";
        home.gameObject.SetActive(failed);
    }
}
