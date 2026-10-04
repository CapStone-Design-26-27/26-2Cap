using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

// 카메라 한 대로 전체 보기와 바구니 집중 보기를 전환한다.
// 전체 보기에서는 저울 전체가 화면에 들어오게 맞추고, 바구니를 클릭하면 그 바구니를 따라가며 확대한다.
// 집중 중인 바구니가 많이 기울수록 단계적으로 축소해 주변이 보이게 한다.
[RequireComponent(typeof(Camera))]
public class CameraFocusController : MonoBehaviour
{
    public static event Action<Basket> OnFocusChanged;

    // 포커스를 바꾼 프레임. 같은 클릭으로 공이 생성되지 않도록 SpawnManager가 확인한다.
    public static int LastFocusChangeFrame { get; private set; } = -1;

    [Header("전체 보기")]
    [SerializeField] private float overviewPadding = 3f;
    [SerializeField] private float minOverviewSize = 20f;

    [Header("바구니 집중 (기울기에 따라 줌아웃)")]
    [SerializeField] private float focusSize = 35f;
    [SerializeField] private float midSize = 40f;
    [SerializeField] private float maxSize = 45f;
    [SerializeField] private float tiltThreshold1 = 15f;
    [SerializeField] private float tiltThreshold2 = 30f;

    [Header("이동 속도")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float zoomSpeed = 3f;

    [Header("비활성 연출 (집중 중이 아닌 바구니, 저울대, 그 안의 공)")]
    [Range(0f, 1f)]
    [SerializeField] private float dimAlpha = 0.25f;
    [Range(0f, 1f)]
    [SerializeField] private float dimBrightness = 0.4f;
    [SerializeField] private float dimFadeSpeed = 6f;

    private Camera cam;
    private readonly List<Renderer> sceneRenderers = new List<Renderer>();

    private readonly List<SpriteRenderer> dimSprites = new List<SpriteRenderer>();
    private readonly List<Color> dimOriginals = new List<Color>();
    private readonly List<float> dimT = new List<float>();
    private readonly Dictionary<SpriteRenderer, Color> ballOriginals = new Dictionary<SpriteRenderer, Color>();
    private readonly HashSet<SpriteRenderer> dimmedBalls = new HashSet<SpriteRenderer>();

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        SpawnManager.Inst.currentCamera = cam;

        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
        {
            sceneRenderers.AddRange(node.GetComponentsInChildren<Renderer>());

            foreach (SpriteRenderer sr in node.GetComponentsInChildren<SpriteRenderer>(true))
            {
                dimSprites.Add(sr);
                dimOriginals.Add(sr.color);
                dimT.Add(1f);
            }
        }

        GetTarget(out Vector2 center, out float size);
        transform.position = new Vector3(center.x, center.y, transform.position.z);
        cam.orthographicSize = size;

        OnFocusChanged?.Invoke(GameManager.Inst.FocusedBasket);
    }

    private void Update()
    {
        if (GameManager.Inst.gameOver) return;

        if (GameManager.Inst.FocusedBasket == null &&
            Input.GetMouseButtonDown(0) &&
            !IsPointerOverUI())
        {
            TryFocusAtPointer();
        }
    }

    private void LateUpdate()
    {
        GetTarget(out Vector2 center, out float size);

        float moveT = 1f - Mathf.Exp(-moveSpeed * Time.deltaTime);
        float zoomT = 1f - Mathf.Exp(-zoomSpeed * Time.deltaTime);

        Vector3 targetPos = new Vector3(center.x, center.y, transform.position.z);
        transform.position = Vector3.Lerp(transform.position, targetPos, moveT);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, size, zoomT);

        UpdateDim();
    }

    public void Focus(Basket basket)
    {
        if (GameManager.Inst.FocusedBasket == basket) return;

        GameManager.Inst.FocusedBasket = basket;
        LastFocusChangeFrame = Time.frameCount;
        OnFocusChanged?.Invoke(basket);
    }

    // 전체 보기 버튼의 OnClick에 연결
    public void ShowOverview() => Focus(null);

    private void TryFocusAtPointer()
    {
        Vector2 world = cam.ScreenToWorldPoint(Input.mousePosition);

        // 클릭 지점에 공이 겹쳐 있을 수 있으므로 겹친 콜라이더를 모두 확인
        foreach (Collider2D hit in Physics2D.OverlapPointAll(world))
        {
            Basket basket = hit.GetComponentInParent<Basket>();
            if (basket != null)
            {
                Focus(basket);
                return;
            }
        }
    }

    private void GetTarget(out Vector2 center, out float size)
    {
        Basket focused = GameManager.Inst.FocusedBasket;

        if (focused != null)
        {
            center = focused.GetVisualBounds().center;

            float tilt = Mathf.Abs(focused.CurrentAngle);
            if (tilt >= tiltThreshold2) size = maxSize;
            else if (tilt >= tiltThreshold1) size = midSize;
            else size = focusSize;
            return;
        }

        bool first = true;
        Bounds b = new Bounds(transform.position, Vector3.zero);
        foreach (Renderer r in sceneRenderers)
        {
            if (r == null || !r.enabled) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }

        center = b.center;
        float fitHeight = Mathf.Max(b.extents.y, b.extents.x / cam.aspect);
        size = Mathf.Max(fitHeight + overviewPadding, minOverviewSize);
    }

    // 전체 보기이거나 집중 중인 바구니(의 하위 스프라이트)는 선명하게, 그 외에는 서서히 어둡게 한다.
    private void UpdateDim()
    {
        Basket focused = GameManager.Inst.FocusedBasket;
        float step = 1f - Mathf.Exp(-dimFadeSpeed * Time.deltaTime);

        for (int i = 0; i < dimSprites.Count; i++)
        {
            if (dimSprites[i] == null) continue;

            bool active = focused == null || dimSprites[i].GetComponentInParent<Basket>() == focused;
            dimT[i] = Mathf.Lerp(dimT[i], active ? 1f : 0f, step);

            Color o = dimOriginals[i];
            float b = Mathf.Lerp(dimBrightness, 1f, dimT[i]);
            dimSprites[i].color = new Color(o.r * b, o.g * b, o.b * b, o.a * Mathf.Lerp(dimAlpha, 1f, dimT[i]));
        }

        // 집중 중이 아닌 바구니 안에 이미 들어 있는 공만 같이 어둡게 한다.
        // 공의 소속은 위치가 아니라 센서가 실제로 센 공으로 판단한다. (던지는 공은 제외됨)
        dimmedBalls.Clear();

        if (focused != null)
        {
            foreach (Basket basket in ScaleSystem.Inst.Baskets)
            {
                if (basket == focused || basket.Sensor == null) continue;

                foreach (Rigidbody2D rb in basket.Sensor.Balls)
                {
                    SpriteRenderer sr = rb.GetComponent<SpriteRenderer>();
                    if (sr != null) dimmedBalls.Add(sr);
                }
            }
        }

        foreach (BallBehaviour ball in FindObjectsOfType<BallBehaviour>())
        {
            SpriteRenderer sr = ball.GetComponent<SpriteRenderer>();
            if (sr == null) continue;

            if (!ballOriginals.ContainsKey(sr))
                ballOriginals[sr] = sr.color;

            bool dimmed = dimmedBalls.Contains(sr);
            Color o = ballOriginals[sr];
            float b = dimmed ? dimBrightness : 1f;
            Color target = new Color(o.r * b, o.g * b, o.b * b, o.a * (dimmed ? dimAlpha : 1f));
            sr.color = Color.Lerp(sr.color, target, step);
        }
    }

    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }
}