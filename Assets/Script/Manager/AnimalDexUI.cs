using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 동물 도감. 열기 버튼과 반투명 창을 생성하고, 1번부터 마지막 동물까지 인게임 크기 비율로 표시한다.
// 캔버스 전체를 덮는(stretch) 오브젝트에 붙인다. 창이 열려 있는 동안 뒤쪽 입력을 막고 게임을 멈춘다.
public class AnimalDexUI : MonoBehaviour
{
    [Header("데이터")]
    [Tooltip("GameManager가 없을 때 동물 프리팹 목록을 읽는 카탈로그")]
    [SerializeField] private GameContentCatalog gameContent;
    [Tooltip("비어 있으면 같은 캔버스의 TMP 글꼴")]
    [SerializeField] private TMP_FontAsset font;

    [Header("열기 버튼")]
    [SerializeField] private Vector2 buttonAnchor = new Vector2(0f, 1f);
    [SerializeField] private Vector2 buttonPosition = new Vector2(110f, -110f);
    [SerializeField] private Vector2 buttonSize = new Vector2(140f, 140f);
    [Tooltip("버튼 배경 스프라이트와 색을 복사할 이미지")]
    [SerializeField] private Image buttonStyleSource;
    [SerializeField] private string buttonLabel = "도감";
    [SerializeField] private Color buttonLabelColor = new Color32(60, 66, 78, 255);
    [SerializeField] private float buttonLabelSize = 40f;

    [Header("창")]
    [SerializeField] private string title = "동물 도감";
    [SerializeField] private string subtitle = "같은 동물 둘이 만나면 다음 동물이 돼요";
    [SerializeField] private Color dimColor = new Color(0f, 0f, 0f, 0.45f);
    [Tooltip("창 배경 (9-slice)")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Color panelColor = new Color(1f, 1f, 1f, 0.92f);
    [Tooltip("동물 목록 영역 배경 (9-slice)")]
    [SerializeField] private Sprite contentSprite;
    [SerializeField] private Sprite closeSprite;
    [Tooltip("동물 번호 배경")]
    [SerializeField] private Sprite badgeSprite;
    [Tooltip("제목, 설명 글자색")]
    [SerializeField] private Color textColor = new Color(0.4f, 0.25f, 0.1f, 1f);
    [Tooltip("동물 번호, 무게 글자색")]
    [SerializeField] private Color labelColor = new Color(0.4f, 0.25f, 0.1f, 1f);
    [Tooltip("닫기 버튼 글자색")]
    [SerializeField] private Color closeLabelColor = new Color(0.81960785f, 0.5568628f, 0.32941177f, 1f);
    [Tooltip("창 앵커 (minX, minY, maxX, maxY)")]
    [SerializeField] private Vector4 panelAnchors = new Vector4(0.05f, 0.1f, 0.95f, 0.88f);

    [Header("동물 배치")]
    [Tooltip("가장 큰 동물의 표시 크기(px)")]
    [SerializeField] private float largestAnimalSize = 328f;
    [Tooltip("동물 한 칸의 최소 폭(px)")]
    [SerializeField] private float minCellWidth = 110f;
    [Tooltip("이 번호의 동물부터 새 줄에 배치")]
    [SerializeField] private int[] rowBreaks = { 6, 9 };
    [SerializeField] private float cellGap = 24f;
    [SerializeField] private float rowGap = 36f;
    [SerializeField] private float labelHeight = 70f;
    [Tooltip("동물 번호 원 크기(px)")]
    [SerializeField] private float badgeSize = 60f;
    [Tooltip("번호 아래에 무게(kg) 표시")]
    [SerializeField] private bool showWeight = false;

    [Header("스테이지")]
    [Tooltip("창이 열려 있는 동안 게임 일시정지")]
    [SerializeField] private bool pauseGame = true;

    private const int UILayer = 5;

    private GameObject openButton;
    private GameObject overlay;
    private RectTransform viewport;
    private RectTransform content;
    private ScrollRect scroll;
    private float builtWidth = -1f;
    private const float WeightLineHeight = 34f;

    private float EntryLabelHeight => showWeight ? labelHeight + WeightLineHeight : labelHeight;

    private GameManager pausedGame;
    private bool previousPause;
    private float previousTimeScale = 1f;

    public bool IsOpen => overlay != null && overlay.activeSelf;

    private void Awake()
    {
#if UNITY_EDITOR
        if (gameContent == null)
            gameContent = UnityEditor.AssetDatabase.LoadAssetAtPath<GameContentCatalog>(PuzzlePaths.ContentCatalogAsset);
#endif
        if (font == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            TMP_Text sample = canvas != null ? canvas.GetComponentInChildren<TMP_Text>(true) : null;
            if (sample != null) font = sample.font;
        }

        BuildButton();
        BuildOverlay();
        overlay.SetActive(false);
    }

    private void Update()
    {
        // 다른 UI가 게임을 멈춘 동안 열기 버튼을 숨긴다.
        if (openButton != null)
        {
            bool pausedByOther = GameManager.Inst != null && GameManager.Inst.gamePaused && !IsOpen;
            if (openButton.activeSelf == pausedByOther) openButton.SetActive(!pausedByOther);
        }

        if (IsOpen && Input.GetKeyDown(KeyCode.Escape)) Close();
    }

    private void OnDisable() => RestorePause();

    // ─── 열기 / 닫기 ───

    public void Open()
    {
        if (IsOpen) return;

        if (pauseGame && GameManager.Inst != null)
        {
            pausedGame = GameManager.Inst;
            previousPause = pausedGame.gamePaused;
            previousTimeScale = Time.timeScale;
            pausedGame.gamePaused = true;
            Time.timeScale = 0f;
            SpawnManager.Inst?.CancelAim();
        }
        BallQueueUI.Inst?.CollapseInventory();

        overlay.SetActive(true);
        Canvas.ForceUpdateCanvases();
        RebuildIfNeeded();
        scroll.verticalNormalizedPosition = 1f;
    }

    public void Close()
    {
        if (overlay != null) overlay.SetActive(false);
        RestorePause();
    }

    private void RestorePause()
    {
        if (ReferenceEquals(pausedGame, null)) return;
        if (pausedGame != null) pausedGame.gamePaused = previousPause;
        Time.timeScale = previousTimeScale;
        pausedGame = null;
    }

    // ─── 데이터 ───

    private List<GameObject> GetAnimals()
    {
        if (GameManager.Inst != null && GameManager.Inst.ballList.Count > 0) return GameManager.Inst.ballList;
        return gameContent != null ? gameContent.balls : new List<GameObject>();
    }

    private static IReadOnlyList<float> GetWeights()
    {
        if (GameManager.Inst != null && GameManager.Inst.kgList.Count > 0) return GameManager.Inst.kgList;
        return GameManager.DefaultBallWeights;
    }

    // 프리팹의 월드 크기 (스프라이트 크기 x 스케일)
    private static Vector2 WorldSize(GameObject prefab, out Sprite sprite)
    {
        sprite = null;
        if (prefab == null) return Vector2.zero;
        SpriteRenderer renderer = prefab.GetComponent<SpriteRenderer>();
        if (renderer == null || renderer.sprite == null) return Vector2.zero;
        sprite = renderer.sprite;
        Vector3 scale = prefab.transform.localScale;
        Vector2 bounds = sprite.bounds.size;
        return new Vector2(bounds.x * Mathf.Abs(scale.x), bounds.y * Mathf.Abs(scale.y));
    }

    // ─── 배치 ───

    private void RebuildIfNeeded()
    {
        float width = viewport.rect.width;
        if (width <= 0f || Mathf.Approximately(width, builtWidth)) return;
        builtWidth = width;

        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);

        List<GameObject> animals = GetAnimals();
        IReadOnlyList<float> weights = GetWeights();
        int count = animals.Count;
        var sprites = new Sprite[count];
        var sizes = new Vector2[count];
        float largest = 0f;
        for (int i = 0; i < count; i++)
        {
            Vector2 world = WorldSize(animals[i], out sprites[i]);
            sizes[i] = world;
            largest = Mathf.Max(largest, world.x, world.y);
        }
        if (largest <= 0f) return;

        // 모든 동물에 공통 배율 적용. 가장 큰 동물이 한 줄 폭을 넘지 않도록 제한한다.
        float pixelsPerUnit = Mathf.Min(largestAnimalSize, width - cellGap * 2f) / largest;
        for (int i = 0; i < count; i++) sizes[i] *= pixelsPerUnit;

        // 줄 단위 배치. rowBreaks 번호이거나 폭을 넘으면 다음 줄로 넘기고, 같은 줄은 바닥선을 맞춘다.
        var rows = new List<List<int>>();
        var current = new List<int>();
        float rowWidth = 0f;
        for (int i = 0; i < count; i++)
        {
            float cell = Mathf.Max(sizes[i].x, minCellWidth);
            float needed = current.Count == 0 ? cell : rowWidth + cellGap + cell;
            bool forceBreak = rowBreaks != null && System.Array.IndexOf(rowBreaks, i + 1) >= 0;
            if (current.Count > 0 && (forceBreak || needed > width))
            {
                rows.Add(current);
                current = new List<int>();
                needed = cell;
            }
            current.Add(i);
            rowWidth = needed;
        }
        if (current.Count > 0) rows.Add(current);

        float y = -rowGap * 0.5f;
        foreach (List<int> row in rows)
        {
            float rowHeight = 0f, total = 0f;
            foreach (int i in row)
            {
                rowHeight = Mathf.Max(rowHeight, sizes[i].y);
                total += Mathf.Max(sizes[i].x, minCellWidth);
            }
            total += cellGap * (row.Count - 1);

            float x = (width - total) * 0.5f;
            float baseline = y - rowHeight;
            foreach (int i in row)
            {
                float cell = Mathf.Max(sizes[i].x, minCellWidth);
                float weight = i < weights.Count ? weights[i] : 0f;
                CreateEntry(i, sprites[i], sizes[i], new Vector2(x + cell * 0.5f, baseline), weight);
                x += cell + cellGap;
            }
            y = baseline - EntryLabelHeight - rowGap;
        }

        content.sizeDelta = new Vector2(content.sizeDelta.x, -y);
    }

    // pos: 동물 아래쪽 가운데(바닥선) 위치
    private void CreateEntry(int index, Sprite sprite, Vector2 size, Vector2 pos, float weight)
    {
        RectTransform animal = NewRect($"Animal {index + 1}", content);
        SetTopLeft(animal, new Vector2(0.5f, 0f), pos, size);
        Image image = animal.gameObject.AddComponent<Image>();
        image.sprite = sprite;
        image.preserveAspect = true;
        image.raycastTarget = false;

        RectTransform badge = NewRect($"Badge {index + 1}", content);
        SetTopLeft(badge, new Vector2(0.5f, 1f), new Vector2(pos.x, pos.y - 6f), new Vector2(badgeSize, badgeSize));
        if (badgeSprite != null)
        {
            Image badgeImage = badge.gameObject.AddComponent<Image>();
            badgeImage.sprite = badgeSprite;
            badgeImage.raycastTarget = false;
        }
        TextMeshProUGUI number = NewText("Number", badge, (index + 1).ToString(), 34f, labelColor);
        number.fontStyle = FontStyles.Bold;
        Stretch(number.rectTransform);

        if (showWeight && weight > 0f)
        {
            TextMeshProUGUI weightText = NewText($"Weight {index + 1}", content, $"{weight:0.##}kg", 26f, labelColor);
            SetTopLeft(weightText.rectTransform, new Vector2(0.5f, 1f), new Vector2(pos.x, pos.y - labelHeight),
                new Vector2(Mathf.Max(size.x, minCellWidth), WeightLineHeight));
        }
    }

    // content 왼쪽 위 기준 배치
    private static void SetTopLeft(RectTransform rect, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = pivot;
        rect.anchoredPosition = pos;
        rect.sizeDelta = size;
    }

    // ─── UI 생성 ───

    private void BuildButton()
    {
        RectTransform rect = NewRect("AnimalDexButton", transform);
        rect.anchorMin = rect.anchorMax = buttonAnchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = buttonPosition;
        rect.sizeDelta = buttonSize;

        Image image = rect.gameObject.AddComponent<Image>();
        if (buttonStyleSource != null)
        {
            image.sprite = buttonStyleSource.sprite;
            image.type = buttonStyleSource.type;
            image.color = buttonStyleSource.color;
            image.preserveAspect = buttonStyleSource.preserveAspect;
        }
        else
        {
            image.color = new Color32(255, 255, 255, 235);
        }

        Button button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        button.onClick.AddListener(Open);

        TextMeshProUGUI label = NewText("Label", rect, buttonLabel, buttonLabelSize, buttonLabelColor);
        label.fontStyle = FontStyles.Bold;
        Stretch(label.rectTransform);
        openButton = rect.gameObject;
    }

    private void BuildOverlay()
    {
        RectTransform root = NewRect("AnimalDexOverlay", transform);
        Stretch(root);
        overlay = root.gameObject;

        // 화면 전체 반투명 배경. 뒤쪽 입력을 막고, 누르면 창을 닫는다.
        RectTransform backdrop = NewRect("Backdrop", root);
        Stretch(backdrop);
        Image dim = backdrop.gameObject.AddComponent<Image>();
        dim.color = dimColor;
        Button dimButton = backdrop.gameObject.AddComponent<Button>();
        dimButton.transition = Selectable.Transition.None;
        dimButton.onClick.AddListener(Close);

        RectTransform panel = NewRect("Panel", root);
        panel.anchorMin = new Vector2(panelAnchors.x, panelAnchors.y);
        panel.anchorMax = new Vector2(panelAnchors.z, panelAnchors.w);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        Image panelImage = panel.gameObject.AddComponent<Image>();
        SetSliced(panelImage, panelSprite);
        panelImage.color = panelColor;

        TextMeshProUGUI titleText = NewText("Title", panel, title, 83.2f, textColor);
        titleText.fontStyle = FontStyles.Bold;
        AnchorTop(titleText.rectTransform, -36f, 104f, 140f);

        TextMeshProUGUI subText = NewText("Subtitle", panel, subtitle, 41.6f, new Color(textColor.r, textColor.g, textColor.b, 0.85f));
        AnchorTop(subText.rectTransform, -146f, 58f, 40f);

        RectTransform close = NewRect("CloseButton", panel);
        close.anchorMin = close.anchorMax = new Vector2(1f, 1f);
        close.pivot = new Vector2(1f, 1f);
        close.anchoredPosition = new Vector2(-24f, -24f);
        close.sizeDelta = new Vector2(110f, 110f);
        Image closeImage = close.gameObject.AddComponent<Image>();
        closeImage.sprite = closeSprite;
        closeImage.color = closeSprite != null ? Color.white : new Color(1f, 1f, 1f, 0.12f);
        Button closeButton = close.gameObject.AddComponent<Button>();
        closeButton.targetGraphic = closeImage;
        closeButton.onClick.AddListener(Close);
        TextMeshProUGUI closeLabel = NewText("X", close, "X", 56f, closeSprite != null ? closeLabelColor : textColor);
        closeLabel.fontStyle = FontStyles.Bold;
        Stretch(closeLabel.rectTransform);

        // 동물 목록 배경
        RectTransform card = NewRect("ContentBackground", panel);
        card.anchorMin = Vector2.zero;
        card.anchorMax = Vector2.one;
        card.offsetMin = new Vector2(30f, 30f);
        card.offsetMax = new Vector2(-30f, -224f);
        if (contentSprite != null)
        {
            Image cardImage = card.gameObject.AddComponent<Image>();
            SetSliced(cardImage, contentSprite);
            cardImage.raycastTarget = false;
        }

        // 스크롤 영역
        viewport = NewRect("Viewport", card);
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(20f, 20f);
        viewport.offsetMax = new Vector2(-20f, -20f);
        Image viewportImage = viewport.gameObject.AddComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f); // 스크롤 드래그 입력용
        viewport.gameObject.AddComponent<RectMask2D>();

        content = NewRect("Content", viewport);
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        scroll = panel.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;
    }

    private static void AnchorTop(RectTransform rect, float y, float height, float sideInset)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = new Vector2(0f, y);
        rect.sizeDelta = new Vector2(-sideInset * 2f, height);
    }

    private static void SetSliced(Image image, Sprite sprite)
    {
        if (sprite == null) return;
        image.sprite = sprite;
        image.type = sprite.border.sqrMagnitude > 0f ? Image.Type.Sliced : Image.Type.Simple;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
    }

    private static RectTransform NewRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = UILayer;
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    private TextMeshProUGUI NewText(string name, Transform parent, string value, float size, Color color)
    {
        RectTransform rect = NewRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        if (font != null) text.font = font;
        text.text = value;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.Center;
        text.enableWordWrapping = false;
        text.raycastTarget = false;
        return text;
    }
}
