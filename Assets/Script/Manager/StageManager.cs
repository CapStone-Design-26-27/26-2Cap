using System;
using System.Collections.Generic;
using UnityEngine;

// 스테이지 진행을 관리한다.
//  - 시작: 각 바구니에 무작위 공을 채워 저울이 기운 상태를 만든다.
//  - 클리어: 모든 저울대가 수평 허용 각도 안에서 일정 시간 유지되면 스테이지 클리어.
//  - 초기 공이 자리 잡는 동안(안정화 시간)은 게임오버, 클리어 판정과 투척을 막는다.
public class StageManager : MonoBehaviour
{
    public static StageManager Inst { get; private set; }

    public static event Action<int> OnStageStarted;
    public static event Action<int> OnStageCleared;

    [Header("초기 배치")]
    [SerializeField, Min(0)] private int minBallsPerBasket = 2;
    [SerializeField, Min(0)] private int maxBallsPerBasket = 4;

    [Tooltip("초기 공의 최대 레벨. 스테이지가 오를 때마다 1씩 늘어나며 공 종류 수를 넘지 않는다.")]
    [SerializeField, Min(0)] private int baseMaxInitialLevel = 3;

    [Tooltip("공을 바구니 가운데 쪽에 놓는 비율 (1 = 스폰 범위 전체). 작을수록 시작할 때 바구니가 덜 기운다.")]
    [SerializeField, Range(0.1f, 1f)] private float spawnSpread = 0.5f;

    [Tooltip("시작 시 좌우 무게 차이(kg)의 범위. 이 범위가 되도록 여러 번 다시 뽑는다.")]
    [SerializeField, Min(0f)] private float minImbalance = 2f;
    [SerializeField, Min(0f)] private float maxImbalance = 10f;
    [SerializeField, Min(1)] private int maxGenerateTries = 50;

    [Tooltip("초기 공이 떨어져 자리 잡을 때까지 기다리는 시간")]
    [SerializeField, Min(0f)] private float settleTime = 2.5f;

    [Header("클리어 조건")]
    [Tooltip("저울대가 이 각도 이내면 수평으로 본다.")]
    [SerializeField, Min(0f)] private float levelTolerance = 3f;
    [Tooltip("수평을 이 시간 동안 유지하면 클리어")]
    [SerializeField, Min(0f)] private float holdTime = 2f;
    [Tooltip("켜면 바구니도 basketTolerance 이내로 수평이어야 클리어")]
    [SerializeField] private bool requireBasketsLevel = false;
    [SerializeField, Min(0f)] private float basketTolerance = 10f;

    [Header("화면 표시")]
    [Tooltip("별도 UI를 만들기 전까지 쓰는 간단한 스테이지 정보, 클리어/게임오버 화면")]
    [SerializeField] private bool drawDebugUI = true;

    private float settleTimer;
    private float holdTimer;
    private int stageStartScore;

    public int Stage { get; private set; } = 1;
    public bool IsCleared { get; private set; }
    public bool IsSettling => settleTimer > 0f;
    public bool AcceptsInput => !IsSettling && !IsCleared;
    public float HoldProgress => holdTime > 0f ? Mathf.Clamp01(holdTimer / holdTime) : 1f;

    private void Awake()
    {
        if (Inst != null && Inst != this)
        {
            Destroy(this);
            return;
        }
        Inst = this;
    }

    private void Update()
    {
        if (settleTimer > 0f) settleTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (GameManager.Inst.gameOver || IsCleared || IsSettling || ScaleSystem.Inst == null) return;

        // 던진 공이 아직 떨어지는 중이면 판정하지 않는다.
        bool stable = SpawnManager.Inst.canSpawn && IsLevel();
        holdTimer = stable ? holdTimer + Time.fixedDeltaTime : 0f;

        if (holdTimer >= holdTime) Clear();
    }

    private bool IsLevel()
    {
        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
        {
            if (node is ScaleBeam)
            {
                if (Mathf.Abs(node.CurrentAngle) > levelTolerance) return false;
            }
            else if (requireBasketsLevel && Mathf.Abs(node.CurrentAngle) > basketTolerance)
            {
                return false;
            }
        }
        return true;
    }

    private void Clear()
    {
        IsCleared = true;
        SpawnManager.Inst.CancelAim();
        Debug.Log($"Stage {Stage} Clear");
        OnStageCleared?.Invoke(Stage);
    }

    // ─── 스테이지 시작 ───

    public void BeginStage()
    {
        stageStartScore = GameManager.Inst.score;
        IsCleared = false;
        holdTimer = 0f;

        ClearBalls();
        SpawnManager.Inst.ResetForStage();
        ScaleSystem.Inst.ResetPose();

        FillBaskets();

        settleTimer = settleTime;
        OnStageStarted?.Invoke(Stage);
    }

    public void NextStage()
    {
        Stage++;
        BeginStage();
    }

    // 게임오버 후 같은 스테이지를 처음부터 다시 한다. 점수도 스테이지 시작 시점으로 되돌린다.
    public void RetryStage()
    {
        GameManager.Inst.ResetGameOver();
        GameManager.Inst.SetScoreFromLoad(stageStartScore);
        BeginStage();
    }

    // 저장된 게임을 불러왔을 때 호출. 공은 SaveManager가 배치한다.
    public void RestoreStage(int stage)
    {
        Stage = Mathf.Max(1, stage);
        stageStartScore = GameManager.Inst.score;
        IsCleared = false;
        holdTimer = 0f;
        settleTimer = settleTime;
        OnStageStarted?.Invoke(Stage);
    }

    private static void ClearBalls()
    {
        foreach (BallBehaviour ball in FindObjectsOfType<BallBehaviour>())
            Destroy(ball.gameObject);
    }

    // ─── 초기 공 배치 ───
    // 바구니마다 서로 다른 레벨의 공을 넣어 시작하자마자 합쳐지지 않게 한다.
    // 각 저울대의 좌우 무게 차이가 minImbalance ~ maxImbalance가 되는 조합을 찾을 때까지 다시 뽑는다.

    private void FillBaskets()
    {
        IReadOnlyList<Basket> baskets = ScaleSystem.Inst.Baskets;
        if (baskets.Count == 0) return;

        int levelCount = GameManager.Inst.ballList.Count;
        int maxLevel = Mathf.Clamp(baseMaxInitialLevel + Stage - 1, 0, Mathf.Max(0, levelCount - 2));

        Dictionary<Basket, List<int>> best = null;
        float bestScore = float.MaxValue;

        for (int attempt = 0; attempt < maxGenerateTries; attempt++)
        {
            Dictionary<Basket, List<int>> plan = RandomPlan(baskets, maxLevel);
            float imbalance = MaxImbalance(plan);

            float score = imbalance < minImbalance ? minImbalance - imbalance
                        : imbalance > maxImbalance ? imbalance - maxImbalance
                        : 0f;

            if (score < bestScore)
            {
                bestScore = score;
                best = plan;
            }
            if (score <= 0f) break;
        }

        foreach (KeyValuePair<Basket, List<int>> pair in best)
            SpawnInBasket(pair.Key, pair.Value);
    }

    private Dictionary<Basket, List<int>> RandomPlan(IReadOnlyList<Basket> baskets, int maxLevel)
    {
        Dictionary<Basket, List<int>> plan = new Dictionary<Basket, List<int>>();
        List<int> levels = new List<int>();

        foreach (Basket basket in baskets)
        {
            levels.Clear();
            for (int lv = 0; lv <= maxLevel; lv++) levels.Add(lv);

            int count = UnityEngine.Random.Range(minBallsPerBasket, maxBallsPerBasket + 1);
            count = Mathf.Min(count, levels.Count);

            List<int> picked = new List<int>();
            for (int i = 0; i < count; i++)
            {
                int idx = UnityEngine.Random.Range(0, levels.Count);
                picked.Add(levels[idx]);
                levels.RemoveAt(idx);
            }
            plan[basket] = picked;
        }
        return plan;
    }

    // 모든 저울대 중 가장 큰 "양 끝 기준으로 환산한 무게 차이"
    private float MaxImbalance(Dictionary<Basket, List<int>> plan)
    {
        float max = 0f;
        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
        {
            if (!(node is ScaleBeam beam)) continue;

            float moment = 0f;
            float halfLength = 0f;
            foreach (ScaleBeam.Attachment a in beam.Attachments)
            {
                if (a.node == null) continue;
                moment += a.offset.x * AddedWeight(a.node, plan, 0);
                halfLength = Mathf.Max(halfLength, Mathf.Abs(a.offset.x));
            }
            if (halfLength > 0f) max = Mathf.Max(max, Mathf.Abs(moment) / halfLength);
        }
        return max;
    }

    private float AddedWeight(HangingNode node, Dictionary<Basket, List<int>> plan, int depth)
    {
        if (node == null || depth > 16) return 0f;

        if (node is Basket basket)
        {
            float sum = 0f;
            if (plan.TryGetValue(basket, out List<int> levels))
                foreach (int lv in levels) sum += GameManager.Inst.kgList[lv];
            return sum;
        }

        float total = 0f;
        if (node is ScaleBeam beam)
            foreach (ScaleBeam.Attachment a in beam.Attachments)
                total += AddedWeight(a.node, plan, depth + 1);
        return total;
    }

    // 스폰 범위 가운데 쪽에서 공을 위로 겹치지 않게 쌓아 떨어뜨린다.
    private void SpawnInBasket(Basket basket, List<int> levels)
    {
        if (basket.SpawnLeftPoint == null || basket.SpawnRightPoint == null) return;

        float leftX = basket.SpawnLeftPoint.position.x;
        float rightX = basket.SpawnRightPoint.position.x;
        float center = (leftX + rightX) * 0.5f;
        float halfRange = Mathf.Abs(rightX - leftX) * 0.5f * spawnSpread;
        float y = Mathf.Max(basket.SpawnLeftPoint.position.y, basket.SpawnRightPoint.position.y);

        foreach (int level in levels)
        {
            Vector2 pos = new Vector2(center + UnityEngine.Random.Range(-halfRange, halfRange), y);
            GameObject ball = Instantiate(GameManager.Inst.ballList[level], pos, Quaternion.identity);
            SpawnManager.Inst.SetupBallProperties(ball, level, false);

            Collider2D col = ball.GetComponent<Collider2D>();
            y += (col != null ? col.bounds.size.y : 2f) + 0.5f;
        }
    }

    // ─── 임시 화면 ───

    private void OnGUI()
    {
        if (!drawDebugUI || GameManager.Inst == null) return;

        float s = Screen.height / 720f;
        GUIStyle label = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(22 * s), alignment = TextAnchor.UpperRight };
        GUIStyle big = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(56 * s), alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        GUIStyle button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(28 * s) };

        string status = IsSettling ? "READY..." : $"LEVEL {HoldProgress * 100f:F0}%";
        GUI.Label(new Rect(Screen.width - 420 * s, 20 * s, 400 * s, 80 * s), $"STAGE {Stage}\n{status}", label);

        Rect center = new Rect(Screen.width * 0.5f - 300 * s, Screen.height * 0.5f - 120 * s, 600 * s, 100 * s);
        Rect buttonRect = new Rect(Screen.width * 0.5f - 120 * s, Screen.height * 0.5f + 10 * s, 240 * s, 70 * s);

        if (IsCleared)
        {
            GUI.Label(center, "STAGE CLEAR", big);
            if (GUI.Button(buttonRect, "NEXT", button)) NextStage();
        }
        else if (GameManager.Inst.gameOver)
        {
            GUI.Label(center, "GAME OVER", big);
            if (GUI.Button(buttonRect, "RETRY", button)) RetryStage();
        }
    }
}
