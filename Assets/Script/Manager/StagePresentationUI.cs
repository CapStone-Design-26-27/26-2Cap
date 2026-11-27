using System.Globalization;
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
    [SerializeField] private TMP_Text actionLabel;

    [Header("바구니 무게 차이 표시")]
    [Tooltip("무게 차이가 이 값보다 작으면 EVEN으로 표시")]
    [SerializeField, Min(0f)] private float balancedWeightThreshold = 0.5f;
    [Tooltip("바구니 줌인 시 글자 최대 크기")]
    [SerializeField, Min(1f)] private float zoomMaxFontSize = 72f;
    [Tooltip("글자 크기 대비 동물 아이콘 크기 배율")]
    [SerializeField, Min(0.1f)] private float zoomIconScale = 1.3f;
    [Tooltip("스테이지 번호, 테두리와 띄울 간격")]
    [SerializeField, Min(0f)] private float zoomPadding = 8f;
    [Tooltip("영역에 맞춘 뒤 글자와 아이콘에 곱하는 배율")]
    [SerializeField, Range(0.1f, 1f)] private float zoomContentScale = 0.91f;
    [Tooltip("+/- 기호 크기 배율")]
    [SerializeField, Min(0.1f)] private float signScale = 1.6f;
    [Tooltip("+/- 기호와 동물 아이콘 사이 간격 (아이콘 크기 대비)")]
    [SerializeField, Min(0f)] private float iconGapRatio = 0.2f;
    [Tooltip("글자 묶음을 가로로 옮기는 값. 음수면 왼쪽")]
    [SerializeField] private float zoomOffsetX = -12f;
    [SerializeField] private Color plusColor = new Color32(220, 161, 45, 255);
    [SerializeField] private Color minusColor = new Color32(220, 161, 45, 255);
    [SerializeField] private Color evenColor = new Color32(220, 161, 45, 255);

    [Header("클리어 표시")]
    [Tooltip("CLEAR % 글자 크기 배율")]
    [SerializeField, Min(0.1f)] private float clearFontScale = 1.1f;

    [Header("남은 공 표시")]
    [Tooltip("균형은 100%지만 넣을 공이 남았을 때 게이지 색")]
    [SerializeField] private Color ballsLeftFillColor = new Color32(176, 184, 196, 255);

    [Header("튜토리얼")]
    [SerializeField] private string tutorialTitle = "TUTORIAL";
    [Tooltip("저울대가 이 각도만큼 기울면 BALANCE 0%")]
    [SerializeField, Min(0.01f)] private float tutorialBeamZeroAngle = 45f;

    private Image weightIcon;
    private int weightIconLevel = -1;
    private Vector4 statusBaseMargin;
    private Vector2 statusBasePos;
    private Vector2 statusBaseSize;
    private float statusBaseFontSize;
    private bool statusBaseWrapping;
    private bool statusZoomed;
    private float signOffsetEm;
    private RectTransform progressTrack;
    private float displayedProgress;
    private bool wasShowingClear;
    private Image progressFillImage;
    private Color progressFillBaseColor = Color.white;


    private void Awake()
    {
        if (progressFill != null)
        {
            progressTrack = progressFill.parent as RectTransform;
            progressFillImage = progressFill.GetComponent<Image>();
            if (progressFillImage != null) progressFillBaseColor = progressFillImage.color;
        }
        if (status != null)
        {
            statusBaseMargin = status.margin;
            statusBasePos = status.rectTransform.anchoredPosition;
            statusBaseSize = status.rectTransform.sizeDelta;
            statusBaseFontSize = status.fontSize;
            statusBaseWrapping = status.enableWordWrapping;
        }
        if (action != null) action.onClick.AddListener(() =>
        {
            var stage = StageManager.Inst;
            if (stage != null && stage.IsCleared && stage.HasNextStage) stage.NextStage();
            else if (stage != null && (stage.IsAttemptFailed || GameManager.Inst?.gameOver == true)) RequestRestart();
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

    // 스테이지나 튜토리얼의 진행 상태
    private struct HeaderState
    {
        public string title;
        public bool ready;
        public bool settling;
        public bool cleared;
        public bool hasNext;
        public bool attemptFailed;
        public bool checking;
        public float hold;
        public float balance;
    }

    private void Update()
    {
        var stage = StageManager.Inst;
        var game = GameManager.Inst;
        if (game == null) return;
        if (stage != null) ShowHeader(GetStageState(stage), game);
        else if (TutorialManager.Inst != null) ShowHeader(GetTutorialState(TutorialManager.Inst), game);
        if (stage == null) return;

        bool failed = stage.IsAttemptFailed || game.gameOver;
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
        if (actionLabel != null) actionLabel.text = failed ? "TRY AGAIN" : "NEXT";
        if (action != null) action.gameObject.SetActive(failed || (stage.IsCleared && stage.HasNextStage));
    }

    private static HeaderState GetStageState(StageManager stage)
    {
        return new HeaderState
        {
            title = $"STAGE {stage.Stage}",
            ready = stage.IsStageReady,
            settling = stage.IsSettling,
            cleared = stage.IsCleared,
            hasNext = stage.HasNextStage,
            attemptFailed = stage.IsAttemptFailed,
            checking = stage.IsCheckingClear,
            hold = stage.HoldProgress,
            balance = stage.OverallBalance,
        };
    }

    private HeaderState GetTutorialState(TutorialManager tutorial)
    {
        return new HeaderState
        {
            title = tutorialTitle,
            ready = ScaleSystem.Inst != null,
            settling = tutorial.CurrentStep == TutorialManager.Step.Settle,
            cleared = tutorial.IsCleared,
            hasNext = true,
            attemptFailed = tutorial.IsFailed,
            checking = tutorial.IsCheckingClear,
            hold = tutorial.HoldProgress,
            balance = GetBeamBalance(tutorialBeamZeroAngle),
        };
    }

    // 모든 저울대의 평균 수평 정도 (0~1)
    private static float GetBeamBalance(float zeroAngle)
    {
        if (ScaleSystem.Inst == null) return 0f;
        float sum = 0f;
        int count = 0;
        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
        {
            if (!(node is ScaleBeam)) continue;
            sum += Mathf.Clamp01(1f - Mathf.Abs(node.CurrentAngle) / Mathf.Max(zeroAngle, 0.01f));
            count++;
        }
        return count > 0 ? sum / count : 0f;
    }

    // 스테이지 번호, 상태 글자, 게이지를 갱신한다.
    private void ShowHeader(HeaderState s, GameManager game)
    {
        var basket = game.FocusedBasket;
        bool failed = s.attemptFailed || game.gameOver;
        bool checking = s.checking && !failed;
        float progress = checking ? s.hold : s.balance;
        bool idle = s.ready && !s.settling && !s.cleared && !failed && !checking;
        // 바구니 줌인 중에는 게이지 없이 무게 차이만 표시
        bool showMeter = s.ready && !failed && !(basket != null && idle);
        int ballsLeft = GetBallsLeft();
        // 균형은 100%지만 아직 넣을 공이 남은 상태
        bool waitingBalls = idle && basket == null && ballsLeft > 0 && Mathf.FloorToInt(s.balance * 100f + .0001f) >= 100;
        int iconLevel = -1;
        if (stageTitle != null) stageTitle.text = s.title;
        if (status != null) status.text = !s.ready ? "LOADING" :
            s.settling ? "CLEAR 0%" :
            s.cleared ? (s.hasNext ? "CLEAR 100%" : "CAMPAIGN COMPLETE") :
            failed ? (s.attemptFailed ? "FAILED" : "GAME OVER") :
            checking ? $"CLEAR {Mathf.FloorToInt(progress * 100f)}%" :
            basket != null && idle ? GetBasketWeightText(basket, out iconLevel) :
            waitingBalls ? $"BALLS LEFT {ballsLeft}" :
            $"BALANCE {Mathf.FloorToInt(s.balance * 100f + .0001f)}%";
        bool clearText = s.ready && !failed && (s.settling || checking || (s.cleared && s.hasNext));
        ApplyStatusLayout(basket != null && idle, iconLevel, clearText);
        UpdateMeter(showMeter, s.settling || checking || s.cleared,
            s.settling ? 0f : s.cleared ? 1f : progress, s.settling);
        if (progressFillImage != null) progressFillImage.color = waitingBalls ? ballsLeftFillColor : progressFillBaseColor;
    }

    // 아직 떨어뜨리지 않은 공 수 (조준 중인 공 포함)
    private static int GetBallsLeft()
    {
        var spawn = SpawnManager.Inst;
        if (spawn == null) return 0;
        return spawn.RemainingBallCount + (spawn.HasPreview ? 1 : 0);
    }

    // 바구니가 매달린 저울대에서 반대편 대비 무게 차이를 구한다.
    private static bool TryGetBasketWeightGap(Basket basket, out float gap)
    {
        gap = 0f;
        if (basket == null || ScaleSystem.Inst == null) return false;
        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
            if (node is ScaleBeam beam && beam.TryGetExcessMass(basket, out gap))
                return true;
        return false;
    }

    // 무게 차이를 +/- 와 동물 아이콘으로 표시
    private string GetBasketWeightText(Basket basket, out int iconLevel)
    {
        iconLevel = -1;
        if (!TryGetBasketWeightGap(basket, out float gap)) return "BASKET";
        if (Mathf.Abs(gap) < balancedWeightThreshold) return $"BASKET <color=#{ColorUtility.ToHtmlStringRGB(evenColor)}>EVEN</color>";
        iconLevel = NearestAnimalLevel(Mathf.Abs(gap));
        int signSize = Mathf.RoundToInt(signScale * 100f);
        string signColor = ColorUtility.ToHtmlStringRGB(gap > 0f ? plusColor : minusColor);
        string offset = signOffsetEm.ToString("0.###", CultureInfo.InvariantCulture);
        string sign = $"<size={signSize}%><voffset={offset}em><color=#{signColor}>{(gap > 0f ? "+" : "-")}</color></voffset></size>";
        return iconLevel < 0 ? $"BASKET {sign}{Mathf.RoundToInt(Mathf.Abs(gap))}" : $"BASKET {sign}";
    }

    // 무게가 가장 가까운 동물 레벨 (로그 기준)
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

    // 바구니 줌인 시 상태 글자와 아이콘을 스테이지 번호 아래 영역에 꽉 차게 키운다
    private void ApplyStatusLayout(bool zoom, int level, bool clearText)
    {
        if (status == null) return;
        var rect = status.rectTransform;
        bool showIcon = zoom && level >= 0 && EnsureWeightIcon();
        if (weightIcon != null && weightIcon.gameObject.activeSelf != showIcon) weightIcon.gameObject.SetActive(showIcon);

        if (!zoom)
        {
            if (statusZoomed)
            {
                rect.anchoredPosition = statusBasePos;
                rect.sizeDelta = statusBaseSize;
                status.margin = statusBaseMargin;
                status.enableWordWrapping = statusBaseWrapping;
                statusZoomed = false;
            }
            float fontSize = statusBaseFontSize * (clearText ? clearFontScale : 1f);
            if (!Mathf.Approximately(status.fontSize, fontSize)) status.fontSize = fontSize;
            return;
        }
        statusZoomed = true;

        var header = rect.parent as RectTransform;
        float top = statusBasePos.y + statusBaseSize.y * (1f - rect.pivot.y);
        if (stageTitle != null)
        {
            var title = stageTitle.rectTransform;
            top = title.anchoredPosition.y - title.rect.height * title.pivot.y;
        }
        top -= zoomPadding;
        float bottom = header != null ? -header.rect.height + zoomPadding : statusBasePos.y - statusBaseSize.y * rect.pivot.y;
        float height = Mathf.Max(top - bottom, 1f);
        // 옮긴 만큼 양쪽 폭을 줄여 반대편 벽에도 닿지 않게 한다
        float width = (header != null ? header.rect.width - zoomPadding * 2f : statusBaseSize.x) - Mathf.Abs(zoomOffsetX) * 2f;
        width = Mathf.Max(width, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(statusBasePos.x + zoomOffsetX, bottom + height * rect.pivot.y);

        // 최대 크기로 실제 글자 폭을 재고, 넘치면 같은 비율로 줄인다
        status.enableWordWrapping = false;
        status.margin = statusBaseMargin;
        status.fontSize = zoomMaxFontSize;
        float textWidth = MeasureStatusWidth();
        float iconSize = showIcon ? zoomMaxFontSize * zoomIconScale : 0f;
        float total = textWidth + (showIcon ? iconSize * (1f + iconGapRatio) : 0f);
        float fit = Mathf.Min(1f, width / Mathf.Max(total, 1f), height / Mathf.Max(iconSize, zoomMaxFontSize * 1.2f)) * zoomContentScale;
        status.fontSize = zoomMaxFontSize * fit;
        iconSize *= fit;
        if (!showIcon)
        {
            AlignStatusGlyphs();
            return;
        }

        // 최종 크기에서 다시 재서 글자+간격+아이콘 묶음을 가운데 정렬
        textWidth = MeasureStatusWidth();
        float iconGap = iconSize * iconGapRatio;
        total = textWidth + iconGap + iconSize;
        status.margin = statusBaseMargin + new Vector4(0f, 0f, iconGap + iconSize, 0f);
        if (level != weightIconLevel)
        {
            var renderer = GameManager.Inst.ballList[level].GetComponent<SpriteRenderer>();
            weightIcon.sprite = renderer != null ? renderer.sprite : null;
            weightIconLevel = level;
        }
        // 아이콘을 글자 끝 바로 뒤에 배치
        var iconRect = (RectTransform)weightIcon.transform;
        iconRect.sizeDelta = Vector2.one * iconSize;
        float iconX = total * .5f - iconSize * .5f + (statusBaseMargin.x - statusBaseMargin.z) * .5f;
        iconRect.anchoredPosition = new Vector2(iconX, 0f);
        AlignStatusGlyphs();
    }

    // BASKET 글자 세로 중심을 영역 가운데(아이콘 높이)에 맞추고, +/- 기호를 같은 높이로 맞춘다
    private void AlignStatusGlyphs()
    {
        status.ForceMeshUpdate();
        var info = status.textInfo;
        float textCenter = float.NaN;
        float signCenter = float.NaN;
        for (int i = 0; i < info.characterCount; i++)
        {
            var c = info.characterInfo[i];
            if (!c.isVisible) continue;
            float center = (c.topLeft.y + c.bottomLeft.y) * .5f;
            if (c.character == '+' || c.character == '-') signCenter = center;
            else if (float.IsNaN(textCenter)) textCenter = center;
        }
        if (float.IsNaN(textCenter)) return;

        if (!float.IsNaN(signCenter))
            signOffsetEm = Mathf.Clamp(signOffsetEm + .5f * (textCenter - signCenter) / Mathf.Max(status.fontSize * signScale, 1f), -1f, 1f);

        Vector4 margin = status.margin;
        margin.y = statusBaseMargin.y + Mathf.Max(0f, textCenter * 2f);
        margin.w = statusBaseMargin.w + Mathf.Max(0f, -textCenter * 2f);
        status.margin = margin;
    }

    // 실제 그려지는 폭과 레이아웃 폭 중 큰 값 (기호 뒤 여백까지 포함)
    private float MeasureStatusWidth()
    {
        status.ForceMeshUpdate();
        float preferred = status.GetPreferredValues(status.text).x - statusBaseMargin.x - statusBaseMargin.z;
        return Mathf.Max(status.textBounds.size.x, preferred);
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
