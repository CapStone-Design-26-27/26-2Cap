using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class SpawnManager : Singleton<SpawnManager>
{
    public static event Action<Transform> OnAimStart;
    public static event Action OnAimEnd;
    public static event Action<int> OnNextBallChanged;

    public Queue<int> nextBallQueue = new Queue<int>();

    // 바닥에 떨어진 공들의 레벨. 다음 공보다 먼저, 떨어진 순서대로 다시 던지게 한다.
    private readonly Queue<int> retryQueue = new Queue<int>();

    [SerializeField] private AudioClip mergeClip;

    [Header("스폰 레벨 설정")]
    [SerializeField] private int minSpawnLevel = 0;
    [SerializeField] private int baseMaxSpawnLevel = 3;

    [Header("스폰 위치")]
    [SerializeField] private float spawnXOffset = 0.5f;
    [Tooltip("바구니에 쌓인 공 위로 얼마나 띄워서 생성할지")]
    [SerializeField] private float stackClearance = 2f;

    public Camera currentCamera;
    public bool canSpawn = true;

    private int lastMergeFrame = -1;
    private GameObject previewBall;
    private int currentLevel;

    private int CurrentMaxSpawnLevel => baseMaxSpawnLevel + GameManager.Inst.currentRound;

    private void Start()
    {
        if (currentCamera == null)
            currentCamera = Camera.main;

        if (nextBallQueue.Count == 0)
            EnqueueRandomBall();

        NotifyNextBallChanged();
    }

    private void Update()
    {
        if (GameManager.Inst.gameOver || !canSpawn)
            return;

        // 초기 공이 자리 잡는 중이거나 스테이지를 클리어한 뒤에는 던질 수 없다.
        if (StageManager.Inst != null && !StageManager.Inst.AcceptsInput)
            return;

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
        if (nextBallQueue.Count == 0)
            EnqueueRandomBall();

        currentLevel = retryQueue.Count > 0
            ? retryQueue.Dequeue()
            : nextBallQueue.Dequeue();

        if (nextBallQueue.Count == 0)
            EnqueueRandomBall();

        NotifyNextBallChanged();

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
        canSpawn = false;

        OnAimEnd?.Invoke();

        SetupBallProperties(previewBall, currentLevel, true);

        previewBall = null;
    }

    public void SpawnMergedBall(int level, Vector2 pos)
    {
        GameObject newCircle = Instantiate(
            GameManager.Inst.ballList[level],
            pos,
            Quaternion.identity);

        SetupBallProperties(newCircle, level, false);

        if (mergeClip != null && lastMergeFrame != Time.frameCount)
        {
            AudioSource.PlayClipAtPoint(mergeClip, currentCamera.transform.position);
            lastMergeFrame = Time.frameCount;
        }
    }

    public void SetupBallProperties(GameObject ball, int level, bool isDroppedByPlayer)
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

        // 공은 자기 바구니 외 다른 바구니의 콜라이더와 충돌하지 않는다.
        Basket owner = GameManager.Inst.FocusedBasket;
        foreach (Basket b in ScaleSystem.Inst.Baskets)
            if (b.Sensor != null && b.Sensor.GetComponent<Collider2D>().OverlapPoint(ball.transform.position)) { owner = b; break; }
        if (col != null && owner != null)
            foreach (Basket b in ScaleSystem.Inst.Baskets)
                if (b != owner)
                    foreach (Collider2D c in b.GetComponentsInChildren<Collider2D>(true))
                        Physics2D.IgnoreCollision(col, c, true);
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

    // 새 스테이지 시작 시 재투척 대기열과 투척 상태를 초기화한다. 다음 공 대기열은 유지한다.
    public void ResetForStage()
    {
        CancelAim();
        retryQueue.Clear();
        canSpawn = true;
        NotifyNextBallChanged();
    }

    public void OnBallLanded()
    {
        canSpawn = true;
    }

    // 바닥에 닿은 공을 같은 레벨로 다시 던지도록 대기열에 넣는다.
    // 방금 던진 공이 바로 떨어진 경우에만 다음 투척을 허용하고,
    // 쌓여 있던 공이 쏟아진 경우에는 지금 떨어지는 공의 착지를 계속 기다린다.
    public void OnBallHitGroundAndRetry(int level, GameObject ball, bool wasPlayersBall)
    {
        retryQueue.Enqueue(level);

        if (wasPlayersBall)
            canSpawn = true;

        Destroy(ball);

        NotifyNextBallChanged();
    }

    // UIManager에서 사용하는 함수
    public int getNextBall()
    {
        if (retryQueue.Count > 0)
            return retryQueue.Peek();

        if (nextBallQueue.Count > 0)
            return nextBallQueue.Peek();

        return -1;
    }

    public void NotifyNextBallChanged()
    {
        OnNextBallChanged?.Invoke(getNextBall());
    }

    private void EnqueueRandomBall()
    {
        nextBallQueue.Enqueue(UnityEngine.Random.Range(minSpawnLevel, CurrentMaxSpawnLevel + 1));
    }

    public List<int> GetRetryLevels()
    {
        return new List<int>(retryQueue);
    }

    public void RestoreQueues(List<int> nextBalls, List<int> retryBalls)
    {
        nextBallQueue = new Queue<int>(nextBalls ?? new List<int>());
        retryQueue.Clear();
        if (retryBalls != null)
            foreach (int level in retryBalls) retryQueue.Enqueue(level);

        if (nextBallQueue.Count == 0)
            EnqueueRandomBall();

        NotifyNextBallChanged();
    }
}