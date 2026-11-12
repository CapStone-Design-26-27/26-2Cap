using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpawnManager : Singleton<SpawnManager>
{
    public static event Action<Transform> OnAimStart;
    public static event Action OnAimEnd;
    public static event Action<int> OnNextBallChanged;

    // 공이 합쳐져 새 공이 생겼을 때 새 공과 레벨을 알린다.
    public static event Action<GameObject, int> OnBallMerged;

    public Queue<int> nextBallQueue = new Queue<int>();

    // 바닥에 떨어진 공들의 레벨. 다음 공보다 먼저, 떨어진 순서대로 다시 던지게 한다.

    // 공(콜라이더)별 소속 바구니. 소속이 다른 공끼리는 충돌과 합성을 하지 않는다.
    private readonly Dictionary<Collider2D, Basket> ballOwners = new Dictionary<Collider2D, Basket>();
    private readonly List<Collider2D> deadBuffer = new List<Collider2D>();

    [SerializeField] private AudioClip mergeClip;

    [Header("스폰 위치")]
    [SerializeField] private float spawnXOffset = 0.5f;
    [Tooltip("바구니에 쌓인 공 위로 얼마나 띄워서 생성할지")]
    [SerializeField] private float stackClearance = 2f;

    public Camera currentCamera;
    public bool canSpawn = true;

    // 튜토리얼 코드: true면 공을 조준하거나 던질 수 없다.
    [HideInInspector] public bool inputLocked = false;

    private int lastMergeFrame = -1;
    private GameObject previewBall;
    private int currentLevel;
    public bool HasPreview => previewBall != null;
    internal bool IsDropSettled
    {
        get
        {
            if (!canSpawn || HasPreview) return false;
            foreach (var collider in ballOwners.Keys)
            {
                if (collider == null || !collider.gameObject.activeInHierarchy) continue;
                var body = collider.attachedRigidbody;
                if (body != null && (body.velocity.sqrMagnitude > .25f || Mathf.Abs(body.angularVelocity) > 10f))
                    return false;
            }
            return true;
        }
    }
    public int RemainingBallCount => nextBallQueue.Count;

    public List<int> GetRemainingLevels()
    {
        return new List<int>(nextBallQueue);
    }

    private void Start()
    {
        if (currentCamera == null)
            currentCamera = Camera.main;

        NotifyNextBallChanged();
    }

    private void Update()
    {
        if (GameManager.Inst.gameOver || GameManager.Inst.gamePaused || !canSpawn || inputLocked)
        {
            CancelAim();
            return;
        }

        // 초기 공이 자리 잡는 중이거나 스테이지를 클리어한 뒤에는 던질 수 없다.
        if (StageManager.Inst != null && !StageManager.Inst.AcceptsInput)
        {
            CancelAim();
            return;
        }

        // 공은 카메라가 집중 중인 바구니에만 던질 수 있다.
        Basket basket = GameManager.Inst.FocusedBasket;
        if (basket == null)
            return;

        if (Input.GetMouseButtonDown(0))
        {
            // 바구니를 선택한 그 클릭으로는 공을 만들지 않는다.
            if (CameraFocusController.LastFocusChangeFrame == Time.frameCount)
                return;

            if (IsPointerOverUI())
                return;

            if (CalculateSpawnPosition(basket, true, out Vector2 spawnPos))
                CreatePreviewBall(spawnPos);
        }
        else if (Input.GetMouseButton(0))
        {
            // 바구니가 오르내리므로 누르고 있는 동안 높이도 계속 갱신한다.
            if (previewBall != null && CalculateSpawnPosition(basket, false, out Vector2 dragPos))
                previewBall.transform.position = dragPos;
        }
        else if (Input.GetMouseButtonUp(0))
        {
            if (previewBall != null)
                DropPreviewBall();
        }
    }

    private static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;
        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);
        return EventSystem.current.IsPointerOverGameObject();
    }

    private bool CalculateSpawnPosition(Basket basket, bool isInitialClick, out Vector2 pos)
    {
        pos = Vector2.zero;

        Transform leftPoint = basket.SpawnLeftPoint;
        Transform rightPoint = basket.SpawnRightPoint;

        if (leftPoint == null || rightPoint == null)
            return false;

        float mouseX = currentCamera.ScreenToWorldPoint(Input.mousePosition).x;

        float leftX = leftPoint.position.x;
        float rightX = rightPoint.position.x;

        float minX = Mathf.Min(leftX, rightX) + spawnXOffset;
        float maxX = Mathf.Max(leftX, rightX) - spawnXOffset;

        // 최초 클릭 시 범위 밖이면 생성하지 않음
        if (isInitialClick && (mouseX < minX || mouseX > maxX))
            return false;

        float spawnX = Mathf.Clamp(mouseX, minX, maxX);

        // 기본은 스폰 포인트 높이, 공이 그보다 높이 쌓였으면 쌓인 공 위
        float spawnY = Mathf.Max(leftPoint.position.y, rightPoint.position.y);

        float ballTop = basket.GetHighestBallTop();
        if (ballTop > float.MinValue)
            spawnY = Mathf.Max(spawnY, ballTop + stackClearance);

        pos = new Vector2(spawnX, spawnY);
        return true;
    }

    private void CreatePreviewBall(Vector2 spawnPos)
    {
        if (HasPreview || RemainingBallCount == 0) return;
        // 조준에서는 소비하지 않는다. 취소/저장 시에도 수량과 순서가 유지된다.
        currentLevel = nextBallQueue.Peek();

        previewBall = Instantiate(
            GameManager.Inst.ballList[currentLevel],
            spawnPos,
            Quaternion.identity);

        Rigidbody2D rb = previewBall.GetComponent<Rigidbody2D>();
        if (rb != null)
            rb.isKinematic = true;

        Collider2D col = previewBall.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = false;

        OnAimStart?.Invoke(previewBall.transform);
    }

    private void DropPreviewBall()
    {
        if (previewBall == null) return;
        StageManager.Inst?.CaptureUndo(previewBall);
        nextBallQueue.Dequeue();
        canSpawn = false;

        OnAimEnd?.Invoke();

        SetupBallProperties(previewBall, currentLevel, true);

        previewBall = null;
        NotifyNextBallChanged();
    }

    public void SpawnMergedBall(int level, Vector2 pos, Basket owner = null)
    {
        GameObject newCircle = Instantiate(
            GameManager.Inst.ballList[level],
            pos,
            Quaternion.identity);

        SetupBallProperties(newCircle, level, false, owner);
        OnBallMerged?.Invoke(newCircle, level);

        if (mergeClip != null && lastMergeFrame != Time.frameCount)
        {
            AudioSource.PlayClipAtPoint(mergeClip, currentCamera.transform.position);
            lastMergeFrame = Time.frameCount;
        }
    }

    // 공의 소속 바구니를 정한다. owner가 주어지면 그 바구니,
    // 없으면 던진 공은 집중 바구니, 그 외의 공은 위치로 정한다.
    public void SetupBallProperties(GameObject ball, int level, bool isDroppedByPlayer, Basket owner = null)
    {
        Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.mass = GameManager.Inst.kgList[level];
        }

        Collider2D col = ball.GetComponent<Collider2D>();
        if (col != null)
            col.enabled = true;

        BallBehaviour bb = ball.GetComponent<BallBehaviour>();
        if (bb == null)
            bb = ball.AddComponent<BallBehaviour>();

        bb.level = level;
        bb.isDroppedByPlayer = isDroppedByPlayer;

        ball.name = string.Format("Circle (Level: {0})", level);

        if (owner == null)
            owner = isDroppedByPlayer ? GameManager.Inst.FocusedBasket : GuessOwner(ball.transform.position);

        bb.owner = owner;

        if (col == null || owner == null)
            return;

        // 소속이 아닌 바구니의 콜라이더와는 충돌 무시
        foreach (Basket b in ScaleSystem.Inst.Baskets)
        {
            if (b == owner) continue;
            foreach (Collider2D c in b.GetComponentsInChildren<Collider2D>(true))
                Physics2D.IgnoreCollision(col, c, true);
        }

        // 소속이 다른 공과는 충돌(과 합성)을 무시. 파괴된 공은 목록에서 정리한다.
        deadBuffer.Clear();
        foreach (KeyValuePair<Collider2D, Basket> pair in ballOwners)
        {
            if (pair.Key == null) { deadBuffer.Add(pair.Key); continue; }
            if (pair.Value != owner)
                Physics2D.IgnoreCollision(col, pair.Key, true);
        }
        foreach (Collider2D dead in deadBuffer)
            ballOwners.Remove(dead);

        ballOwners[col] = owner;
    }

    // 위치를 센서 영역에 포함하는 바구니, 없으면 가장 가까운 바구니를 돌려준다.
    private Basket GuessOwner(Vector2 pos)
    {
        Basket below = null;
        float belowTop = float.MinValue;
        Basket nearest = null;
        float best = float.MaxValue;

        foreach (Basket b in ScaleSystem.Inst.Baskets)
        {
            if (b == null) continue;
            if (InSensor(b, pos)) return b;

            float d = ((Vector2)b.transform.position - pos).sqrMagnitude;
            if (d < best) { best = d; nearest = b; }

            if (b.Sensor == null) continue;
            Bounds area = b.Sensor.GetComponent<Collider2D>().bounds;
            if (pos.x < area.min.x || pos.x > area.max.x || pos.y < area.max.y) continue;
            if (area.max.y > belowTop) { belowTop = area.max.y; below = b; }
        }
        return below != null ? below : nearest;
    }

    private bool InSensor(Basket b, Vector2 pos)
    {
        return b != null && b.Sensor != null && b.Sensor.GetComponent<Collider2D>().OverlapPoint(pos);
    }

    // 조준 중인 공을 없애고 조준을 끝낸다.
    public void CancelAim()
    {
        if (previewBall != null)
        {
            Destroy(previewBall);
            previewBall = null;
            OnAimEnd?.Invoke();
        }
    }

    // 새 스테이지에서는 이전 스테이지의 지급 대기열을 비운다.
    public void ResetForStage()
    {
        CancelAim();
        nextBallQueue.Clear();
        ballOwners.Clear();
        canSpawn = true;
        NotifyNextBallChanged();
    }

    public void OnBallLanded()
    {
        canSpawn = true;
    }

    // UIManager에서 사용하는 함수
    public int getNextBall()
    {
        if (nextBallQueue.Count > 0)
            return nextBallQueue.Peek();

        return -1;
    }

    public void NotifyNextBallChanged()
    {
        OnNextBallChanged?.Invoke(getNextBall());
    }

    public void RestoreQueue(List<int> nextBalls)
    {
        nextBallQueue = new Queue<int>(nextBalls);
        NotifyNextBallChanged();
    }
}
