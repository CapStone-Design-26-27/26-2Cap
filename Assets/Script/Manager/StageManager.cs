using System;
using System.Collections.Generic;
using UnityEngine;

// JSON 스테이지 데이터를 읽어 공을 배치하고 스테이지 진행을 관리한다.
public class StageManager : MonoBehaviour
{
    public static StageManager Inst { get; private set; }

    public static event Action<int> OnStageStarted;
    public static event Action<int> OnStageCleared;

    [Header("JSON 스테이지 데이터")]
    [Tooltip("Assets/Resources 폴더 안 JSON 파일 이름. 확장자 .json은 쓰지 않는다.")]
    [SerializeField] private string jsonResourceName = "StageData";

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

    private StageDatabase stageDatabase;
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
        LoadStageDatabase();
    }

    private void LoadStageDatabase()
    {
        TextAsset jsonFile = Resources.Load<TextAsset>(jsonResourceName);

        if (jsonFile == null)
        {
            Debug.LogError(
                $"StageManager: JSON 파일을 찾을 수 없습니다. " +
                $"Assets/Resources/{jsonResourceName}.json 경로를 확인하세요.");
            return;
        }

        stageDatabase = JsonUtility.FromJson<StageDatabase>(jsonFile.text);

        if (stageDatabase == null || stageDatabase.stages == null || stageDatabase.stages.Count == 0)
        {
            Debug.LogError($"StageManager: {jsonResourceName}.json에 스테이지 데이터가 없거나 형식이 잘못되었습니다.");
            stageDatabase = null;
            return;
        }

        Debug.Log($"StageManager: JSON 스테이지 {stageDatabase.stages.Count}개 로드 완료");
    }

    private void Update()
    {
        if (settleTimer > 0f)
            settleTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (GameManager.Inst == null ||
            GameManager.Inst.gameOver ||
            IsCleared ||
            IsSettling ||
            ScaleSystem.Inst == null ||
            SpawnManager.Inst == null)
        {
            return;
        }

        // 던진 공이 아직 떨어지는 중이면 판정하지 않는다.
        bool stable = SpawnManager.Inst.canSpawn && IsLevel();
        holdTimer = stable ? holdTimer + Time.fixedDeltaTime : 0f;

        if (holdTimer >= holdTime)
            Clear();
    }

    private bool IsLevel()
    {
        foreach (HangingNode node in ScaleSystem.Inst.AllNodes)
        {
            if (node is ScaleBeam)
            {
                if (Mathf.Abs(node.CurrentAngle) > levelTolerance)
                    return false;
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

        if (SpawnManager.Inst != null)
            SpawnManager.Inst.CancelAim();

        Debug.Log($"Stage {Stage} Clear");
        OnStageCleared?.Invoke(Stage);
    }

    // 스테이지 시작: 랜덤 배치 대신 JSON에 정의된 공과 월드 좌표를 사용한다.
    public void BeginStage()
    {
        if (GameManager.Inst == null || ScaleSystem.Inst == null || SpawnManager.Inst == null)
        {
            Debug.LogError("StageManager: GameManager, ScaleSystem, SpawnManager 연결을 확인하세요.");
            return;
        }
        IsCleared = false;
        holdTimer = 0f;

        ClearBalls();
        SpawnManager.Inst.ResetForStage();
        ScaleSystem.Inst.ResetPose();

        StageDefinition stageData = FindStageData(Stage);
        if (stageData == null)
        {
            Debug.LogError($"StageManager: JSON에서 Stage {Stage} 데이터를 찾지 못했습니다. 공을 생성하지 않습니다.");
            settleTimer = 0f;
            return;
        }

        ApplyStageSettings(stageData);
        SpawnJsonBalls(stageData);

        settleTimer = stageData.settleTime;
        OnStageStarted?.Invoke(Stage);
    }

    private StageDefinition FindStageData(int stageNumber)
    {
        if (stageDatabase == null || stageDatabase.stages == null)
            return null;

        for (int i = 0; i < stageDatabase.stages.Count; i++)
        {
            if (stageDatabase.stages[i].stage == stageNumber)
                return stageDatabase.stages[i];
        }

        return null;
    }

    private void ApplyStageSettings(StageDefinition stageData)
    {
        levelTolerance = stageData.levelTolerance;
        holdTime = stageData.holdTime;
    }

    private void SpawnJsonBalls(StageDefinition stageData)
    {
        if (stageData.balls == null)
        {
            Debug.LogError($"StageManager: Stage {stageData.stage}의 balls 데이터가 없습니다.");
            return;
        }

        int spawnedCount = 0;

        foreach (StageBallData ballData in stageData.balls)
        {
            if (ballData.level < 0 || ballData.level >= GameManager.Inst.ballList.Count)
            {
                Debug.LogError(
                    $"StageManager: Stage {stageData.stage}, Ball ID {ballData.ballId}의 level " +
                    $"{ballData.level}이 ballList 범위를 벗어났습니다.");
                continue;
            }

            Vector3 worldPosition = new Vector3(ballData.x, ballData.y, 0f);
            GameObject ball = Instantiate(
                GameManager.Inst.ballList[ballData.level],
                worldPosition,
                Quaternion.identity);

            SpawnManager.Inst.SetupBallProperties(ball, ballData.level, false);
            ball.name = $"Stage{stageData.stage}_Ball{ballData.ballId}_Level{ballData.level}";
            spawnedCount++;
        }

        Debug.Log($"StageManager: Stage {stageData.stage} - JSON 공 {spawnedCount}/{stageData.balls.Count}개 생성");
    }

    public void NextStage()
    {
        Stage++;
        BeginStage();
    }

    // 게임오버 후 같은 스테이지를 처음부터 다시 한다. 점수도 스테이지 시작 시점으로 되돌린다.
    public void RetryStage()
    {
        if (GameManager.Inst == null)
            return;

        GameManager.Inst.ResetGameOver();
        BeginStage();
    }

    // 저장된 게임을 불러왔을 때 호출. 공은 SaveManager가 배치한다.
    public void RestoreStage(int stage)
    {
        Stage = Mathf.Max(1, stage);
        IsCleared = false;
        holdTimer = 0f;
        settleTimer = 0f;
        OnStageStarted?.Invoke(Stage);
    }

    private static void ClearBalls()
    {
        foreach (BallBehaviour ball in FindObjectsOfType<BallBehaviour>())
            Destroy(ball.gameObject);
    }

    private void OnGUI()
    {
        if (!drawDebugUI || GameManager.Inst == null)
            return;

        float s = Screen.height / 720f;
        GUIStyle label = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(22 * s),
            alignment = TextAnchor.UpperRight
        };

        GUIStyle big = new GUIStyle(GUI.skin.label)
        {
            fontSize = Mathf.RoundToInt(56 * s),
            alignment = TextAnchor.MiddleCenter,
            fontStyle = FontStyle.Bold
        };

        GUIStyle button = new GUIStyle(GUI.skin.button)
        {
            fontSize = Mathf.RoundToInt(28 * s)
        };

        string status = IsSettling ? "READY..." : $"LEVEL {HoldProgress * 100f:F0}%";
        GUI.Label(
            new Rect(Screen.width - 420 * s, 20 * s, 400 * s, 80 * s),
            $"STAGE {Stage}\n{status}",
            label);

        Rect center = new Rect(
            Screen.width * 0.5f - 300 * s,
            Screen.height * 0.5f - 120 * s,
            600 * s,
            100 * s);

        Rect buttonRect = new Rect(
            Screen.width * 0.5f - 120 * s,
            Screen.height * 0.5f + 10 * s,
            240 * s,
            70 * s);

        if (IsCleared)
        {
            GUI.Label(center, "STAGE CLEAR", big);
            if (GUI.Button(buttonRect, "NEXT", button))
                NextStage();
        }
        else if (GameManager.Inst.gameOver)
        {
            GUI.Label(center, "GAME OVER", big);
            if (GUI.Button(buttonRect, "RETRY", button))
                RetryStage();
        }
    }
}

[Serializable]
public class StageDatabase
{
    public List<StageDefinition> stages = new List<StageDefinition>();
}

[Serializable]
public class StageDefinition
{
    public int stage;
    public float minImbalance;
    public float maxImbalance;
    public float levelTolerance = 3f;
    public float holdTime = 2f;
    public float settleTime = 2.5f;
    public int forcedMergeTargetBallId = -1;
    public List<StageBallData> balls = new List<StageBallData>();
}

[Serializable]
public class StageBallData
{
    public int ballId;
    public int level;
    public float x;
    public float y;
}
