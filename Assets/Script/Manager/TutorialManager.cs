using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// 튜토리얼 코드. 바구니 선택 → 공 떨어뜨리기 → 두 바구니 평형 순서로 진행한다.
// 실패하면 게임오버 대신 처음 배치로 되돌린다.
public class TutorialManager : MonoBehaviour
{
    public const string CompletedKey = "TutorialCompleted";

    // 튜토리얼 씬에서만 값이 있다.
    public static TutorialManager Inst { get; private set; }
    public static bool IsCompleted => PlayerPrefs.GetInt(CompletedKey, 0) == 1;

    public enum Step { Settle, SelectBasket, DropBall, Balance, Failed, Complete }

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
    [SerializeField] private string mainSceneName = "TestScene";

    [Header("공 배치")]
    [SerializeField] private List<BasketPreset> presets = new List<BasketPreset>();
    [Tooltip("플레이어가 던지는 공의 레벨")]
    [SerializeField] private int playerBallLevel = 3;
    [SerializeField, Min(0f)] private float settleTime = 2.5f;

    [Header("평형 판정")]
    [Tooltip("바구니가 이 각도 이내면 평형으로 본다.")]
    [SerializeField, Min(0f)] private float basketTolerance = 3f;
    [Tooltip("켜면 저울대(막대)도 수평이어야 완료")]
    [SerializeField] private bool requireBeamLevel = false;
    [SerializeField, Min(0f)] private float beamTolerance = 3f;
    [Tooltip("평형을 이 시간 동안 유지하면 완료")]
    [SerializeField, Min(0f)] private float holdTime = 1f;

    [Header("연출")]
    [Tooltip("줌인이 끝날 때까지 기다리는 시간")]
    [SerializeField, Min(0f)] private float zoomWait = 0.6f;
    [SerializeField, Min(0f)] private float failMessageTime = 1.5f;

    [Header("문구")]
    [SerializeField] private string msgSelectBasket = "바구니를 눌러보세요!";
    [SerializeField] private string msgDropBall = "화면을 눌러 공을 떨어뜨리세요!";
    [SerializeField] private string msgBalance = "바구니의 평형을 이루세요!";
    [SerializeField] private string msgOtherBasket = "좋아요! 다른 바구니도 평형을 맞춰보세요";
    [SerializeField] private string msgPickTilted = "기울어진 바구니를 눌러보세요";
    [SerializeField] private string msgHold = "그대로 유지하세요...";
    [SerializeField] private string msgFailed = "앗! 다시 해볼까요?";

    private Step step;
    private float timer;
    private float holdTimer;
    private bool waitingLanding;
    private bool aiming;
    private bool afterFail;
    private readonly List<Basket> unbalanced = new List<Basket>();

    public Step CurrentStep => step;

    private void Awake()
    {
        Inst = this;
        if (cameraFocus == null) cameraFocus = FindObjectOfType<CameraFocusController>();
    }

    private void OnDestroy()
    {
        if (Inst == this) Inst = null;
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
                if (timer <= 0f)
                {
                    WarnIfAlreadyLevel();
                    if (afterFail) EnterBalance();
                    else EnterSelectBasket();
                }
                break;

            case Step.DropBall:
                if (timer > 0f)
                {
                    timer -= Time.deltaTime;
                    if (timer <= 0f && !waitingLanding && !aiming) ui.ShowTap(GameManager.Inst.FocusedBasket);
                }

                // 던진 공이 착지하면 canSpawn이 다시 true가 된다.
                if (waitingLanding && SpawnManager.Inst.canSpawn)
                    EnterBalance();
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

        bool hide = step == Step.Settle || step == Step.SelectBasket || step == Step.DropBall
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

    private void EnterSelectBasket()
    {
        step = Step.SelectBasket;
        SetInput(cameraInput: true, spawnInput: false);

        List<Basket> all = new List<Basket>(ScaleSystem.Inst.Baskets);
        ui.SetBasketArrows(all);
        ui.SetMessage(msgSelectBasket);
    }

    private void EnterDropBall()
    {
        step = Step.DropBall;
        timer = zoomWait;
        waitingLanding = false;
        SetInput(cameraInput: false, spawnInput: true);

        ui.SetBasketArrows(null);
        ui.SetMessage(msgDropBall);
    }

    private void EnterBalance()
    {
        step = Step.Balance;
        holdTimer = 0f;
        waitingLanding = false;
        SetInput(cameraInput: true, spawnInput: true);

        ui.HideTap();
        ui.SetMessage(msgBalance);

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
        ui.ShowComplete(GoToMainScene);
    }

    // ─── 평형 맞추기 ───

    private void UpdateBalance()
    {
        unbalanced.Clear();
        foreach (Basket b in ScaleSystem.Inst.Baskets)
            if (Mathf.Abs(b.CurrentAngle) > basketTolerance) unbalanced.Add(b);

        bool beamOk = !requireBeamLevel || IsBeamLevel();
        bool ballSettled = SpawnManager.Inst.canSpawn;
        Basket focused = GameManager.Inst.FocusedBasket;

        if (unbalanced.Count == 0 && beamOk && ballSettled)
        {
            holdTimer += Time.deltaTime;
            ui.SetBasketArrows(null);
            ui.SetOverviewHint(false);
            ui.SetMessage(msgHold);

            if (holdTimer >= holdTime) EnterComplete();
            return;
        }

        holdTimer = 0f;

        if (focused == null)
        {
            // 전체 보기에서는 기울어진 바구니를 가리킨다.
            ui.SetOverviewHint(false);
            ui.SetBasketArrows(unbalanced);
            ui.SetMessage(unbalanced.Count > 0 ? msgPickTilted : msgBalance);
        }
        else
        {
            ui.SetBasketArrows(null);

            // 이 바구니를 맞췄으면 전체 보기 버튼을 가리킨다.
            bool focusedDone = !unbalanced.Contains(focused);
            bool goOther = focusedDone && unbalanced.Count > 0 && ballSettled;
            ui.SetOverviewHint(goOther);
            ui.SetMessage(goOther ? msgOtherBasket : msgBalance);
        }
    }

    private bool IsBeamLevel()
    {
        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
            if (node is ScaleBeam && Mathf.Abs(node.CurrentAngle) > beamTolerance) return false;
        return true;
    }

    // ─── 이벤트 ───

    private void HandleFocusChanged(Basket basket)
    {
        if (step == Step.SelectBasket && basket != null)
            EnterDropBall();
        else if (step == Step.DropBall && basket == null && !waitingLanding)
            EnterSelectBasket();
    }

    private void HandleAimStart(Transform ball)
    {
        aiming = true;
        if (step == Step.DropBall) ui.HideTap();
    }

    private void HandleAimEnd()
    {
        aiming = false;
        if (step != Step.DropBall) return;

        // 공을 놓았으면 착지를 기다리고, 조준을 취소했으면 탭 표시를 다시 띄운다.
        if (!SpawnManager.Inst.canSpawn)
        {
            waitingLanding = true;
            ui.SetMessage(msgBalance);
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
        StartCoroutine(FailRoutine());
    }

    private IEnumerator FailRoutine()
    {
        step = Step.Failed;
        SetInput(cameraInput: false, spawnInput: false);
        SpawnManager.Inst.CancelAim();

        ui.HideAll();
        ui.SetMessage(msgFailed);

        yield return new WaitForSeconds(failMessageTime);

        // 공 소속이 잘못 잡히지 않도록 전체 보기로 돌린 뒤 배치한다.
        cameraFocus.ShowOverview();
        SetupBalls();

        afterFail = true;
        EnterSettle();
    }

    // 완료 패널의 시작 버튼
    public void GoToMainScene()
    {
        // 매니저가 DontDestroyOnLoad라 지워야 본게임 씬의 매니저가 새로 시작한다.
        if (GameManager.Inst != null)
            Destroy(GameManager.Inst.gameObject);

        SceneManager.LoadScene(mainSceneName);
    }

    // ─── 공 배치 ───

    private void SetupBalls()
    {
        foreach (BallBehaviour ball in FindObjectsOfType<BallBehaviour>())
            Destroy(ball.gameObject);

        ScaleSystem.Inst.ResetPose();
        SpawnManager.Inst.ResetForStage();

        // 플레이어에게는 정해진 레벨의 공만 준다.
        List<int> queue = new List<int>();
        for (int i = 0; i < 30; i++) queue.Add(playerBallLevel);
        SpawnManager.Inst.RestoreQueues(queue, null);

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
            SpawnManager.Inst.SetupBallProperties(ball, level, false);

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

    private void WarnIfAlreadyLevel()
    {
        foreach (BasketPreset preset in presets)
        {
            if (preset.basket == null) continue;
            float angle = preset.basket.CurrentAngle;
            Debug.Log($"[Tutorial] {preset.basket.name} 시작 기울기 {angle:F1}도");

            if (Mathf.Abs(angle) <= basketTolerance)
                Debug.LogWarning($"[Tutorial] {preset.basket.name}이(가) 이미 평형({angle:F1}도)입니다. " +
                                 "공 레벨을 올리거나 side를 벽 쪽으로, basketTolerance를 작게 조정하세요.");
        }
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
