using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// 튜토리얼 코드. 안내 문구, 화살표, 탭 표시, 완료 패널을 보여준다.
// 카메라가 움직인 뒤 위치를 맞추도록 실행 순서를 늦춘다.
[DefaultExecutionOrder(100)]
public class TutorialUI : MonoBehaviour
{
    [Header("연결")]
    [SerializeField] private Canvas canvas;
    [Tooltip("비워 두면 Camera.main")]
    [SerializeField] private Camera worldCamera;

    [Header("상단 안내 문구")]
    [SerializeField] private GameObject messageRoot;
    [SerializeField] private TextMeshProUGUI messageText;

    [Header("바구니 화살표")]
    [Tooltip("바구니 수만큼 복제해서 쓴다.")]
    [SerializeField] private RectTransform arrowTemplate;
    [Tooltip("바구니 윗면에서 화살표까지의 높이 (월드 단위)")]
    [SerializeField] private float arrowWorldOffset = 6f;
    [SerializeField] private float bobAmplitude = 25f;
    [SerializeField] private float bobSpeed = 5f;

    [Header("탭 표시")]
    [SerializeField] private RectTransform tapMarker;
    [Tooltip("공 생성 높이에서 아래로 내려 표시할 거리 (월드 단위)")]
    [SerializeField] private float tapWorldDrop = 4f;
    [SerializeField] private float tapPulseScale = 0.25f;
    [SerializeField] private float tapPulseSpeed = 6f;
    [Tooltip("좌우로 오가는 범위 (0 = 가운데 고정)")]
    [SerializeField, Range(0f, 1f)] private float tapSweepRange = 0.6f;
    [SerializeField] private float tapSweepSpeed = 1.2f;

    [Header("전체 보기 버튼 안내")]
    [Tooltip("Canvas/Btnpanel/Main")]
    [SerializeField] private RectTransform overviewButton;
    [Tooltip("회전시켜 두면 그 방향으로 가리킨다.")]
    [SerializeField] private RectTransform overviewArrow;
    [Tooltip("버튼에서 화살표까지의 거리 (1080x1920 기준)")]
    [SerializeField] private Vector2 overviewArrowOffset = new Vector2(0f, 150f);

    [Header("완료")]
    [SerializeField] private GameObject completePanel;
    [SerializeField] private Button startButton;

    private RectTransform canvasRect;
    private readonly List<RectTransform> arrows = new List<RectTransform>();
    private readonly List<Basket> arrowTargets = new List<Basket>();
    private Basket tapBasket;
    private bool overviewHint;
    private string currentMessage;

    private void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        canvasRect = canvas.GetComponent<RectTransform>();
        if (worldCamera == null) worldCamera = Camera.main;

        // 안내 그래픽이 클릭을 막지 않도록 버튼 외에는 Raycast Target을 끈다.
        foreach (Graphic g in GetComponentsInChildren<Graphic>(true))
        {
            if (g.GetComponentInParent<Selectable>(true) == null)
                g.raycastTarget = false;
        }

        if (arrowTemplate != null) arrowTemplate.gameObject.SetActive(false);
        HideAll();
        if (completePanel != null) completePanel.SetActive(false);
    }

    // ─── TutorialManager가 호출 ───

    public void SetMessage(string text)
    {
        if (text == currentMessage) return;
        currentMessage = text;

        bool show = !string.IsNullOrEmpty(text);
        if (messageRoot != null) messageRoot.SetActive(show);
        if (messageText != null) messageText.text = show ? text : "";
    }

    // null이나 빈 목록이면 숨긴다.
    public void SetBasketArrows(IList<Basket> targets)
    {
        arrowTargets.Clear();
        if (targets != null) arrowTargets.AddRange(targets);

        while (arrows.Count < arrowTargets.Count && arrowTemplate != null)
        {
            RectTransform clone = Instantiate(arrowTemplate, arrowTemplate.parent);
            clone.name = "Arrow_" + arrows.Count;
            arrows.Add(clone);
        }

        for (int i = 0; i < arrows.Count; i++)
            arrows[i].gameObject.SetActive(i < arrowTargets.Count);
    }

    public void ShowTap(Basket basket)
    {
        tapBasket = basket;
        if (tapMarker != null) tapMarker.gameObject.SetActive(basket != null);
    }

    public void HideTap() => ShowTap(null);

    public void SetOverviewHint(bool show)
    {
        overviewHint = show;
        if (overviewArrow != null) overviewArrow.gameObject.SetActive(show);
    }

    public void ShowComplete(UnityAction onStart)
    {
        if (completePanel != null) completePanel.SetActive(true);
        if (startButton == null) return;

        startButton.interactable = true;
        startButton.onClick.RemoveAllListeners();
        startButton.onClick.AddListener(() =>
        {
            startButton.interactable = false;
            onStart?.Invoke();
        });
    }

    public void HideAll()
    {
        SetMessage(null);
        SetBasketArrows(null);
        HideTap();
        SetOverviewHint(false);
    }

    // ─── 위치 갱신 ───

    private void LateUpdate()
    {
        float bob = Mathf.Sin(Time.unscaledTime * bobSpeed) * bobAmplitude;

        for (int i = 0; i < arrowTargets.Count && i < arrows.Count; i++)
        {
            Basket b = arrowTargets[i];
            if (b == null) continue;

            Vector3 world = CupTop(b) + Vector3.up * arrowWorldOffset;
            if (WorldToCanvas(world, out Vector2 local))
                SetCanvasPosition(arrows[i], local + Vector2.up * Mathf.Abs(bob));
        }

        if (tapBasket != null && tapMarker != null)
        {
            Vector3 l = tapBasket.SpawnLeftPoint.position;
            Vector3 r = tapBasket.SpawnRightPoint.position;
            float t = 0.5f + 0.5f * tapSweepRange * Mathf.Sin(Time.unscaledTime * tapSweepSpeed);
            Vector3 world = Vector3.Lerp(l, r, t) + Vector3.down * tapWorldDrop;

            if (WorldToCanvas(world, out Vector2 local))
                SetCanvasPosition(tapMarker, local);

            float pulse = 1f + tapPulseScale * (0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * tapPulseSpeed));
            tapMarker.localScale = Vector3.one * pulse;
        }

        if (overviewHint && overviewArrow != null && overviewButton != null)
        {
            Canvas buttonCanvas = overviewButton.GetComponentInParent<Canvas>().rootCanvas;
            Camera buttonCam = buttonCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : buttonCanvas.worldCamera;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(buttonCam, overviewButton.position);

            if (ScreenToCanvas(screen, out Vector2 local))
            {
                // 화살표가 가리키는 방향으로 오르내린다.
                Vector2 dir = overviewArrow.rotation * Vector3.down;
                SetCanvasPosition(overviewArrow, local + overviewArrowOffset - dir * Mathf.Abs(bob));
            }
        }
    }

    // 컵 윗면 가운데
    private static Vector3 CupTop(Basket b)
    {
        if (b.SpawnLeftPoint != null && b.SpawnRightPoint != null)
            return (b.SpawnLeftPoint.position + b.SpawnRightPoint.position) * 0.5f;

        Bounds bounds = b.GetVisualBounds();
        return new Vector3(bounds.center.x, bounds.max.y, 0f);
    }

    private Camera CanvasCamera => canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

    private bool WorldToCanvas(Vector3 world, out Vector2 local)
    {
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        if (screen.z < 0f) { local = default; return false; }
        return ScreenToCanvas(screen, out local);
    }

    private bool ScreenToCanvas(Vector2 screen, out Vector2 local)
    {
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screen, CanvasCamera, out local);
    }

    // 부모와 상관없이 캔버스 기준 위치에 놓는다.
    private void SetCanvasPosition(RectTransform rt, Vector2 canvasLocal)
    {
        rt.position = canvasRect.TransformPoint(canvasLocal);
    }
}
