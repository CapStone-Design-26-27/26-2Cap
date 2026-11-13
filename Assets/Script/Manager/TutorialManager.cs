using System.Collections.Generic;
using UnityEngine;

// 정해진 공을 모두 바구니에 넣으면 튜토리얼을 완료한다.
public class TutorialManager : MonoBehaviour
{
    public const string CompletedKey = "TutorialCompleted";

    // 튜토리얼 씬에서만 값이 있다.
    public static TutorialManager Inst { get; private set; }
    public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

    public enum Step { Settle, InspectNext, SelectBasket, DropBall, ReturnOverview, Balance, Failed, Complete }

    [System.Serializable]
    public class BasketPreset
    {
        public Basket basket;

        [Tooltip("미리 넣을 공 레벨. 플레이어 공과 같으면 합쳐지므로 다르게 둔다.")]
        public List<int> levels = new List<int> { 1, 2 };

        [Tooltip("-1 = 왼쪽 벽, 0 = 가운데, 1 = 오른쪽 벽")]
        [Range(-1f, 1f)] public float side = -1f;
    }

    [Header("연결")]
    [SerializeField] private TutorialUI ui;
    [SerializeField] private CameraFocusController cameraFocus;
    [Tooltip("Canvas/Btnpanel/Main (전체 보기 버튼)")]
    [SerializeField] private GameObject overviewButton;

    [Header("공 배치")]
    [SerializeField] private List<BasketPreset> presets = new List<BasketPreset>();
    [Tooltip("플레이어에게 한 번만 지급할 공의 레벨 순서.")]
    [SerializeField] private List<int> ballOrder = new List<int> { 3 };
    [SerializeField, Min(0f)] private float settleTime = 2.5f;

    [Header("평형 판정")]
    [SerializeField, Min(0f)] private float beamTolerance = 4f;
    [SerializeField, Min(0f)] private float basketTolerance = 4f;
    [Tooltip("모든 공이 이 시간 동안 바구니에 안착하면 완료")]
    [SerializeField, Min(0f)] private float holdTime = 1.5f;

    [Header("연출")]
    [Tooltip("줌인이 끝날 때까지 기다리는 시간")]
    [SerializeField, Min(0f)] private float zoomWait = 0.6f;

    [Header("문구")]
    [SerializeField] private string msgSelectBasket = "바구니를 눌러보세요!";
    [SerializeField] private string msgBackToBasket = "균형을 맞춰보세요!";
    [SerializeField] private string msgDropBall = "화면을 눌러 공을 떨어뜨리세요!";
    [SerializeField] private string msgHold = "그대로 유지하세요...";
    [SerializeField] private string msgFailed = "앗! 다시 해볼까요?";
    [SerializeField] private string msgStartGame = "게임 시작";
    [SerializeField] private string msgBackToMain = "메인으로";

    private Step step;
    private float timer;
    private float holdTimer;
    private bool waitingLanding;
    private float expectedWeight;
    private float finishTimer;
    private float imbalanceTimer;
    private readonly HashSet<Rigidbody2D> countedBalls = new HashSet<Rigidbody2D>();
    private bool aiming;
    private bool firstClear;

    public Step CurrentStep => step;
    public int TotalProvidedBalls { get; private set; }

    private void Awake()
    {
        Inst = this;
        firstClear = !IsCompleted;
        BallMergeLock.SetGlobalLocked(false);
        if (cameraFocus == null) cameraFocus = FindObjectOfType<CameraFocusController>();
    }

    private void OnDestroy()
    {
        if (Inst == this) Inst = null;

        // 합성 잠금은 튜토리얼에서만 쓰므로 씬을 나갈 때 끈다.
        BallMergeLock.SetGlobalLocked(false);
    }

    private void OnEnable()
    {
        CameraFocusController.OnFocusChanged += HandleFocusChanged;
        SpawnManager.OnAimStart += HandleAimStart;
        SpawnManager.OnAimEnd += HandleAimEnd;
    }

    private void OnDisable()
    {
        CameraFocusController.OnFocusChanged -= HandleFocusChanged;
        SpawnManager.OnAimStart -= HandleAimStart;
        SpawnManager.OnAimEnd -= HandleAimEnd;
    }

    private void Start()
    {
        SetupBalls();
        EnterSettle();
    }

    private void Update()
    {
        switch (step)
        {
            case Step.Settle:
                timer -= Time.deltaTime;
                if (timer <= 0f) EnterInspectNext();
                break;

            case Step.DropBall:
                if (timer > 0f)
                {
                    timer -= Time.deltaTime;
                    if (timer <= 0f && !waitingLanding && !aiming) ui.ShowTap(GameManager.Inst.FocusedBasket);
                }

                // 던진 공이 착지하면 canSpawn이 다시 true가 된다.
                if (waitingLanding && SpawnManager.Inst.canSpawn)
                    EnterReturnOverview();
                break;

            case Step.Balance:
                UpdateBalance();
                break;
        }
    }

    // 첫 공을 떨어뜨리기 전까지 전체 보기 버튼을 숨긴다.
    private void LateUpdate()
    {
        if (overviewButton == null) return;

        bool hide = step == Step.Settle || step == Step.InspectNext || step == Step.SelectBasket || step == Step.DropBall
                 || step == Step.Failed || step == Step.Complete;
        if (hide && overviewButton.activeSelf) overviewButton.SetActive(false);
    }

    // ─── 단계 전환 ───

    private void EnterSettle()
    {
        step = Step.Settle;
        timer = settleTime;
        SetInput(cameraInput: false, spawnInput: false);
        ui.HideAll();
    }

    private void EnterInspectNext()
    {
        var queue = BallQueueUI.Inst;
        if (queue == null)
        {
            EnterSelectBasket();
            return;
        }

        step = Step.InspectNext;
        queue.CollapseInventory();
        ui.SetNextHint(queue.NextButtonRect);
        ui.SetMessage("NEXT를 눌러보세요!");
    }

    internal void OnDropOrderOpened()
    {
        if (step == Step.InspectNext) EnterSelectBasket();
    }

    private void EnterSelectBasket()
    {
        step = Step.SelectBasket;
        ui.SetNextHint(null);
        SetInput(cameraInput: true, spawnInput: false);

        List<Basket> all = new List<Basket>(ScaleSystem.Inst.Baskets);
        ui.SetBasketArrows(all);
        ui.SetMessage(msgSelectBasket);
    }

    private void EnterDropBall()
    {
        step = Step.DropBall;
        BallQueueUI.Inst?.CollapseInventory();
        timer = zoomWait;
        waitingLanding = false;
        SetInput(cameraInput: false, spawnInput: true);

        ui.SetBasketArrows(null);
        ui.SetMessage(msgDropBall);
    }

    private void EnterReturnOverview()
    {
        if (overviewButton == null || GameManager.Inst.FocusedBasket == null)
        {
            EnterBalance();
            return;
        }

        step = Step.ReturnOverview;
        SetInput(cameraInput: true, spawnInput: false);
        overviewButton.SetActive(true);
        ui.HideTap();
        ui.SetOverviewHint(true);
        ui.SetMessage("MAIN을 눌러보세요!");
    }

    private void EnterBalance()
    {
        step = Step.Balance;
        ui.SetOverviewHint(false);
        holdTimer = 0f;
        waitingLanding = false;
        SetInput(cameraInput: true, spawnInput: true);

        ui.HideTap();
        ui.SetMessage(GameManager.Inst.FocusedBasket == null ? msgBackToBasket : msgDropBall);

        // 숨겨 둔 전체 보기 버튼을 다시 켠다.
        if (overviewButton != null)
            overviewButton.SetActive(GameManager.Inst.FocusedBasket != null);
    }

    private void EnterComplete()
    {
        step = Step.Complete;
        SetInput(cameraInput: false, spawnInput: false);
        SpawnManager.Inst.CancelAim();

        PlayerPrefs.SetInt(CompletedKey, 1);
        PlayerPrefs.Save();

        ui.HideAll();
        ui.ShowResult("튜토리얼 완료!", firstClear ? msgStartGame : msgBackToMain, ExitTutorial);
    }

    // ─── 평형 맞추기 ───

    private void UpdateBalance()
    {
        var spawn = SpawnManager.Inst;
        Basket focused = GameManager.Inst.FocusedBasket;
        if (spawn.RemainingBallCount > 0 || spawn.HasPreview)
        {
            if (!aiming) ui.ShowTap(spawn.canSpawn ? focused : null);
            ui.SetMessage(focused == null ? msgBackToBasket : msgDropBall);
            return;
        }

        ui.HideTap();
        finishTimer += Time.deltaTime;
        bool settled = CheckBasketBalance(out bool balanced) && spawn.canSpawn;
        if (settled && balanced && IsScaleLevel())
        {
            ui.SetMessage(msgHold);
            imbalanceTimer = 0f;
            holdTimer += Time.deltaTime;
            if (holdTimer >= holdTime) EnterComplete();
            return;
        }

        holdTimer = 0f;
        ui.SetMessage(settled ? "바구니의 평형을 맞춰보세요!" : "잠시 기다리세요");
        imbalanceTimer = settled && !balanced ? imbalanceTimer + Time.deltaTime : 0f;
        if (imbalanceTimer >= 2f || finishTimer >= 8f) OnGameOverRequested();
    }

    private bool CheckBasketBalance(out bool balanced)
    {
        var baskets = ScaleSystem.Inst.Baskets;
        balanced = baskets.Count == 2;
        float totalWeight = 0f;
        float targetWeight = expectedWeight * 0.5f;
        countedBalls.Clear();
        foreach (Basket basket in baskets)
        {
            if (basket.Sensor == null) return false;
            float basketWeight = 0f;
            foreach (Rigidbody2D ball in basket.Sensor.Balls)
            {
                if (ball.velocity.sqrMagnitude > 0.25f || Mathf.Abs(ball.angularVelocity) > 10f)
                    return false;
                basketWeight += ball.mass;
                if (countedBalls.Add(ball)) totalWeight += ball.mass;
            }
            balanced &= Mathf.Abs(basketWeight - targetWeight) <= 0.01f;
        }

        balanced &= Mathf.Abs(totalWeight - expectedWeight) < 0.01f;
        return true;
    }

    private bool IsScaleLevel()
    {
        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
        {
            float tolerance = node is Basket ? basketTolerance : beamTolerance;
            if (Mathf.Abs(node.CurrentAngle) > tolerance) return false;
        }

        return true;
    }

    // ─── 이벤트 ───

    private void HandleFocusChanged(Basket basket)
    {
        if (step == Step.ReturnOverview && basket == null)
            EnterBalance();
        else if (step == Step.SelectBasket && basket != null)
            EnterDropBall();
        else if (step == Step.DropBall && basket == null && !waitingLanding)
            EnterSelectBasket();
    }

    private void HandleAimStart(Transform ball)
    {
        aiming = true;
        ui.HideTap();
    }

    private void HandleAimEnd()
    {
        aiming = false;
        if (step != Step.DropBall) return;

        // 공을 놓았으면 착지를 기다리고, 조준을 취소했으면 탭 표시를 다시 띄운다.
        if (!SpawnManager.Inst.canSpawn)
        {
            waitingLanding = true;
            ui.SetMessage("잠시 기다리세요");
        }
        else
        {
            ui.ShowTap(GameManager.Inst.FocusedBasket);
        }
    }

    // 튜토리얼 중 게임오버 조건이 되면 GameManager가 대신 호출한다.
    public void OnGameOverRequested()
    {
        if (step == Step.Failed || step == Step.Complete || step == Step.Settle) return;
        step = Step.Failed;
        SetInput(cameraInput: false, spawnInput: false);
        ui.HideAll();
        ui.ShowResult(msgFailed, "다시 시작", Retry);
    }

    private void Retry()
    {
        ui.HideResult();
        cameraFocus.Focus(null);
        SetupBalls();
        EnterSettle();
    }

    // 완료 패널의 버튼. 처음 클리어면 본게임으로, 다시 한 경우면 메인 화면으로 간다.
    public void ExitTutorial()
    {
        if (firstClear) SceneFlow.LoadGame();
        else SceneFlow.LoadMain();
    }

    // 완료하지 않고 메인 화면으로 나간다. (나가기 버튼용)
    public void QuitToMain()
    {
        SceneFlow.LoadMain();
    }

    // ─── 공 배치 ───

    private void SetupBalls()
    {
        expectedWeight = 0f;
        finishTimer = 0f;
        imbalanceTimer = 0f;

        foreach (BallBehaviour ball in FindObjectsOfType<BallBehaviour>())
            Destroy(ball.gameObject);

        ScaleSystem.Inst.ResetPose();
        SpawnManager.Inst.ResetForStage();

        // 플레이어에게는 정해진 순서의 공만 준다.
        var queue = new List<int>();
        foreach (int level in ballOrder)
        {
            if (level < 0 || level >= GameManager.Inst.ballList.Count) continue;
            queue.Add(level);
            expectedWeight += GameManager.Inst.kgList[level];
        }
        TotalProvidedBalls = queue.Count;
        SpawnManager.Inst.RestoreQueue(queue);

        foreach (BasketPreset preset in presets)
            SpawnPreset(preset);
    }

    private void SpawnPreset(BasketPreset preset)
    {
        Basket basket = preset.basket;
        if (basket == null || basket.SpawnLeftPoint == null || basket.SpawnRightPoint == null) return;

        float leftX = basket.SpawnLeftPoint.position.x;
        float rightX = basket.SpawnRightPoint.position.x;
        float center = (leftX + rightX) * 0.5f;
        float half = Mathf.Abs(rightX - leftX) * 0.5f;
        float y = Mathf.Max(basket.SpawnLeftPoint.position.y, basket.SpawnRightPoint.position.y);

        foreach (int level in preset.levels)
        {
            if (level < 0 || level >= GameManager.Inst.ballList.Count) continue;

            GameObject prefab = GameManager.Inst.ballList[level];
            float radius = GetRadius(prefab);

            // 벽에 걸리지 않게 반지름만큼 안쪽에 놓는다.
            float reach = Mathf.Max(0f, half - radius - 0.3f);
            Vector2 pos = new Vector2(center + preset.side * reach, y + radius);

            GameObject ball = Instantiate(prefab, pos, Quaternion.identity);
            SpawnManager.Inst.SetupBallProperties(ball, level, false, basket);
            expectedWeight += GameManager.Inst.kgList[level];

            y += radius * 2f + 0.5f;
        }
    }

    private static float GetRadius(GameObject prefab)
    {
        CircleCollider2D circle = prefab.GetComponent<CircleCollider2D>();
        if (circle == null) return 1f;
        Vector3 s = prefab.transform.localScale;
        return circle.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
    }

    private void SetInput(bool cameraInput, bool spawnInput)
    {
        if (cameraFocus != null) cameraFocus.inputLocked = !cameraInput;
        SpawnManager.Inst.inputLocked = !spawnInput;
        if (!spawnInput) SpawnManager.Inst.CancelAim();
    }

#if UNITY_EDITOR
    [ContextMenu("튜토리얼 완료 기록 지우기")]
    private void ResetCompleted()
    {
        PlayerPrefs.DeleteKey(CompletedKey);
        Debug.Log("[Tutorial] 완료 기록을 지웠습니다.");
    }
#endif
}
