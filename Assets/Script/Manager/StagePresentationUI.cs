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

    [Header("바구니 무게 차이 표시")]
    [Tooltip("상태 글자 옆에 붙는 동물 아이콘 크기")]
    [SerializeField, Min(1f)] private float weightIconSize = 34f;
    [Tooltip("무게 차이가 이 값보다 작으면 균형(EVEN)으로 표시한다. 가장 가벼운 동물의 절반 정도.")]
    [SerializeField, Min(0f)] private float balancedWeightThreshold = 0.5f;

    private const float WeightIconGap = 6f;
    private Image weightIcon;
    private int weightIconLevel = -1;
    private Vector4 statusBaseMargin;
    private RectTransform progressTrack;
    private float displayedProgress;
    private bool wasShowingClear;


    private void Awake()
    {
        if (progressFill != null) progressTrack = progressFill.parent as RectTransform;
        if (status != null) statusBaseMargin = status.margin;
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
        // 클리어 조건은 도르래(저울대) 균형뿐이므로 게이지는 바구니를 보고 있어도 전체 균형을 따른다.
        float balance = stage.OverallBalance;
        bool failed = stage.IsAttemptFailed || game.gameOver;
        bool checking = stage.IsCheckingClear && !failed;
        bool showMeter = stage.IsStageReady && !failed;
        float progress = checking ? stage.HoldProgress : balance;
        bool idle = stage.IsStageReady && !stage.IsSettling && !stage.IsCleared && !failed && !checking;
        int iconLevel = -1;
        if (stageTitle != null) stageTitle.text = $"STAGE {stage.Stage}";
        if (status != null) status.text = !stage.IsStageReady ? "LOADING" :
            stage.IsSettling ? "CLEAR 0%" :
            stage.IsCleared ? (stage.HasNextStage ? "CLEAR 100%" : "CAMPAIGN COMPLETE") :
            failed ? (stage.IsAttemptFailed ? "TRY AGAIN" : "GAME OVER") :
            checking ? $"CLEAR {Mathf.FloorToInt(progress * 100f)}%" :
            basket != null && idle ? GetBasketWeightText(stage, basket, out iconLevel) :
            $"BALANCE {Mathf.FloorToInt(balance * 100f + .0001f)}%";
        UpdateWeightIcon(iconLevel);
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

    // 도르래 반대편 대비 무게 차이를 +/- 와 가장 가까운 무게의 동물 아이콘으로 표시한다.
    private string GetBasketWeightText(StageManager stage, Basket basket, out int iconLevel)
    {
        iconLevel = -1;
        if (!stage.TryGetBasketWeightGap(basket, out float gap)) return "BASKET";
        if (Mathf.Abs(gap) < balancedWeightThreshold) return "BASKET <color=#3E9B4F>EVEN</color>";
        iconLevel = NearestAnimalLevel(Mathf.Abs(gap));
        string sign = gap > 0f ? "<color=#D2493A>+</color>" : "<color=#3A7BD2>-</color>";
        return iconLevel < 0 ? $"BASKET {sign}{Mathf.RoundToInt(Mathf.Abs(gap))}" : $"BASKET {sign}";
    }

    // 무게가 2배씩 늘어나므로 로그 기준으로 가장 가까운 동물을 고른다.
    private static int NearestAnimalLevel(float mass)
    {
        var game = GameManager.Inst;
        if (game == null || mass <= 0f) return -1;
        int count = Mathf.Min(game.kgList.Count, game.ballList.Count);
        int best = -1;
        float bestDist = float.MaxValue;
        float logMass = Mathf.Log(mass);
        for (int i = 0; i < count; i++)
        {
            if (game.kgList[i] <= 0f || game.ballList[i] == null) continue;
            float dist = Mathf.Abs(Mathf.Log(game.kgList[i]) - logMass);
            if (dist < bestDist) { bestDist = dist; best = i; }
        }
        return best;
    }

    private void UpdateWeightIcon(int level)
    {
        if (status == null) return;
        bool show = level >= 0 && EnsureWeightIcon();
        if (weightIcon != null && weightIcon.gameObject.activeSelf != show) weightIcon.gameObject.SetActive(show);
        Vector4 margin = show ? statusBaseMargin + new Vector4(0f, 0f, WeightIconGap + weightIconSize, 0f) : statusBaseMargin;
        if (status.margin != margin) status.margin = margin;
        if (!show) return;

        if (level != weightIconLevel)
        {
            var renderer = GameManager.Inst.ballList[level].GetComponent<SpriteRenderer>();
            weightIcon.sprite = renderer != null ? renderer.sprite : null;
            weightIconLevel = level;
        }
        // 글자는 오른쪽 여백만큼 왼쪽으로 밀려 가운데 정렬되므로, 아이콘은 글자 끝 바로 뒤에 둔다.
        float textWidth = status.GetPreferredValues(status.text).x;
        var rect = (RectTransform)weightIcon.transform;
        rect.sizeDelta = Vector2.one * weightIconSize;
        rect.anchoredPosition = new Vector2(textWidth * .5f + WeightIconGap * .5f + (statusBaseMargin.x - statusBaseMargin.z) * .5f, 0f);
    }

    private bool EnsureWeightIcon()
    {
        if (weightIcon != null) return true;
        var go = new GameObject("WeightAnimalIcon", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = status.gameObject.layer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(status.rectTransform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
        weightIcon = go.GetComponent<Image>();
        weightIcon.preserveAspect = true;
        weightIcon.raycastTarget = false;
        return true;
    }
}
