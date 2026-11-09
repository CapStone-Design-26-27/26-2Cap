using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// JSON 스테이지 데이터를 읽어 공을 배치하고 스테이지 진행을 관리한다.
public class StageManager : MonoBehaviour
{
    public static StageManager Inst { get; private set; }

    public static event Action<int> OnStageStarted;
    public static event Action<int> OnStageCleared;

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
    private readonly Dictionary<int, StageDefinition> definitionsByStage = new Dictionary<int, StageDefinition>();
    [Header("시작 카운트다운")]
    [SerializeField, Min(0f)] private float startCountdownSeconds = 3f;

    private float settleTimer;
    private float holdTimer;
    private bool stageReady;
    private Coroutine stageRoutine;
    private float exhaustedTimer;
    private float expectedMass;
    private static readonly Dictionary<BallBehaviour, StageBallData> ballDataByInstance =
        new Dictionary<BallBehaviour, StageBallData>();

    public int Stage { get; private set; } = 1;
    public bool IsCleared { get; private set; }
    public bool IsStageReady => stageReady;
    public string DataSignature => GetStageSignature(Stage);
    public float ExpectedMass => expectedMass;
    public int CurrentMapId => GetStageMapId(Stage);
    public bool HasNextStage => FindStageData(Stage + 1) != null;
    public int TotalProvidedBalls => FindStageData(Stage)?.playerBallQueue?.Count ?? 0;
    public bool IsAttemptFailed { get; private set; }
    public bool IsSettling => settleTimer > 0f;
    public int CountdownSeconds => Mathf.CeilToInt(Mathf.Max(0f, settleTimer));
    public bool AcceptsInput => stageReady && !IsSettling && !IsCheckingClear && !IsCleared && !IsAttemptFailed &&
        GameManager.Inst != null && !GameManager.Inst.gameOver && !GameManager.Inst.gamePaused;
    public float HoldProgress => holdTime > 0f ? Mathf.Clamp01(holdTimer / holdTime) : 1f;
    public bool IsCheckingClear => stageReady && !IsSettling && !IsCleared && holdTimer > 0f;
    public float ClearSecondsRemaining => Mathf.Max(0f, holdTime - holdTimer);

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

    private void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    private void LoadStageDatabase()
    {
        TextAsset jsonFile = GameManager.Inst != null && GameManager.Inst.Content != null
            ? GameManager.Inst.Content.stageData : null;

        if (jsonFile == null)
        {
            Debug.LogError(
                "StageManager: Game Content에 Assets/Aset/Data/StageData.json을 연결하세요.");
            return;
        }

        try
        {
            stageDatabase = JsonUtility.FromJson<StageDatabase>(jsonFile.text);
        }
        catch (ArgumentException exception)
        {
            Debug.LogError("StageManager: 스테이지 JSON을 읽을 수 없습니다: " + exception.Message);
            stageDatabase = null;
            return;
        }

        // JsonUtility는 누락된 float를 0으로 읽을 수 있어 원본 JSON에서 좌표 존재를 확인한다.
        var records = System.Text.RegularExpressions.Regex.Matches(
            jsonFile.text, @"\{[^{}]*""ballId""\s*:[^{}]*\}");
        int recordIndex = 0;
        if (stageDatabase != null && stageDatabase.stages != null)
        {
            foreach (StageDefinition stage in stageDatabase.stages)
            {
                if (stage.balls == null) continue;
                foreach (StageBallData ball in stage.balls)
                {
                    string record = recordIndex < records.Count ? records[recordIndex].Value : "";
                    recordIndex++;
                    if (!System.Text.RegularExpressions.Regex.IsMatch(record, @"""x""\s*:") ||
                        !System.Text.RegularExpressions.Regex.IsMatch(record, @"""y""\s*:"))
                    {
                        ball.x = float.NaN;
                        ball.y = float.NaN;
                    }
                }
            }
        }

        if (stageDatabase == null || stageDatabase.stages == null || stageDatabase.stages.Count == 0)
        {
            Debug.LogError("StageManager: StageData.json에 스테이지 데이터가 없거나 형식이 잘못되었습니다.");
            stageDatabase = null;
            return;
        }

        definitionsByStage.Clear();
        foreach (StageDefinition definition in stageDatabase.stages)
        {
            if (definitionsByStage.ContainsKey(definition.stage))
            {
                Debug.LogError($"StageManager: 중복 스테이지 번호 {definition.stage}");
                definitionsByStage.Clear();
                return;
            }
            definitionsByStage.Add(definition.stage, definition);
        }
        Debug.Log($"StageManager: JSON 스테이지 {stageDatabase.stages.Count}개 로드 완료");
    }

    public string GetStageSignature(int stageNumber)
    {
        StageDefinition definition = FindStageData(stageNumber);
        if (definition == null || GameManager.Inst == null) return null;
        string weights = string.Join(",", GameManager.Inst.kgList.ConvertAll(w =>
            w.ToString("R", System.Globalization.CultureInfo.InvariantCulture)));
        using (var hash = System.Security.Cryptography.SHA256.Create())
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(
                JsonUtility.ToJson(definition) + "|" + weights)));
    }

    private void Update()
    {
        if (GameManager.Inst == null || GameManager.Inst.gameOver || GameManager.Inst.gamePaused)
            return;
        if (settleTimer > 0f)
            settleTimer = Mathf.Max(0f, settleTimer - Time.deltaTime);
    }

    private void FixedUpdate()
    {
        if (GameManager.Inst == null ||
            GameManager.Inst.gameOver ||
            GameManager.Inst.gamePaused ||
            IsCleared ||
            IsAttemptFailed ||
            !stageReady ||
            IsSettling ||
            ScaleSystem.Inst == null ||
            SpawnManager.Inst == null)
        {
            return;
        }

        // 던진 공이 아직 떨어지는 중이면 판정하지 않는다.
        bool allUsed = SpawnManager.Inst.RemainingBallCount == 0 && !SpawnManager.Inst.HasPreview;
        bool allInBaskets = HasAllMassInBaskets();
        bool stopped = BallsHaveSettled();
        bool stable = allUsed && allInBaskets && stopped && HasBalancedMasses() && IsLevel();
        if (stable && holdTimer <= 0f)
            SpawnManager.Inst.CancelAim();
        holdTimer = stable ? holdTimer + Time.fixedDeltaTime : 0f;

        if (stable && holdTimer >= holdTime)
            Clear();

        // 마지막 공이 실제로 멈춘 뒤 기다린다. 낙하/합성 중에는 실패 판정하지 않는다.
        exhaustedTimer = allUsed && allInBaskets && stopped && !stable
            ? exhaustedTimer + Time.fixedDeltaTime : 0f;
        if (!IsCleared && exhaustedTimer >= 2f) IsAttemptFailed = true;
    }

    private bool HasAllMassInBaskets()
    {
        HashSet<Rigidbody2D> counted = new HashSet<Rigidbody2D>();
        float mass = 0f;
        foreach (Basket basket in ScaleSystem.Inst.Baskets)
        {
            if (basket.Sensor == null) return false;
            foreach (Rigidbody2D ball in basket.Sensor.Balls)
                if (counted.Add(ball)) mass += ball.mass;
        }
        return Mathf.Abs(mass - expectedMass) < 0.01f;
    }

    private bool BallsHaveSettled()
    {
        foreach (Basket basket in ScaleSystem.Inst.Baskets)
        {
            if (basket.Sensor == null) return false;
            foreach (Rigidbody2D ball in basket.Sensor.Balls)
                if (ball.velocity.sqrMagnitude > 0.25f || Mathf.Abs(ball.angularVelocity) > 10f)
                    return false;
        }
        return true;
    }

    private bool HasBalancedMasses()
    {
        StageMapDefinition map = stageDatabase.maps.Find(m => m.mapId == CurrentMapId);
        if (map == null || map.targetWeightRatios == null ||
            map.targetWeightRatios.Count != ScaleSystem.Inst.Baskets.Count) return false;
        int ratioSum = 0;
        foreach (int ratio in map.targetWeightRatios)
        {
            if (ratio <= 0) return false;
            ratioSum += ratio;
        }
        if (ratioSum == 0) return false;
        float unitMass = expectedMass / ratioSum;
        for (int i = 0; i < map.targetWeightRatios.Count; i++)
            if (Mathf.Abs(ScaleSystem.Inst.Baskets[i].Sensor.TotalWeight -
                unitMass * map.targetWeightRatios[i]) > 0.01f) return false;
        return true;
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
        if (SaveManager.Inst != null) SaveManager.Inst.SaveGame();
    }

    // 스테이지 시작: 랜덤 배치 대신 JSON에 정의된 공과 월드 좌표를 사용한다.
    public void BeginStage()
    {
        StartStageRoutine(BeginStageRoutine());
    }

    private void StartStageRoutine(IEnumerator routine)
    {
        if (stageRoutine != null) StopCoroutine(stageRoutine);
        stageRoutine = StartCoroutine(routine);
    }

    private IEnumerator BeginStageRoutine()
    {
        if (GameManager.Inst == null || SpawnManager.Inst == null)
        {
            Debug.LogError("StageManager: GameManager와 SpawnManager 연결을 확인하세요.");
            yield break;
        }
        IsCleared = false;
        IsAttemptFailed = false;
        exhaustedTimer = 0f;
        stageReady = false;
        holdTimer = 0f;
        settleTimer = 0f;

        ClearBalls();
        ballDataByInstance.Clear();
        SpawnManager.Inst.ResetForStage();
        yield return EnsureStageMap(Stage);
        if (!IsStageCompatible(Stage)) yield break;

        ScaleSystem.Inst.ResetPose();

        StageDefinition stageData = FindStageData(Stage);
        if (stageData == null)
        {
            Debug.LogError($"StageManager: JSON에서 Stage {Stage} 데이터를 찾지 못했습니다. 공을 생성하지 않습니다.");
            settleTimer = 0f;
            yield break;
        }

        if (stageData.balls == null || stageData.balls.Count == 0)
        {
            Debug.LogError($"StageManager: Stage {Stage}에 배치할 공이 없습니다.");
            yield break;
        }
        foreach (StageBallData ball in stageData.balls)
        {
            if (ball == null || float.IsNaN(ball.x) || float.IsInfinity(ball.x) ||
                float.IsNaN(ball.y) || float.IsInfinity(ball.y) ||
                ball.level < 0 || ball.level >= GameManager.Inst.ballList.Count ||
                (ball.positionSpace != BallPositionSpace.World &&
                    ball.positionSpace != BallPositionSpace.BasketSensor) ||
                (ball.positionSpace == BallPositionSpace.BasketSensor &&
                    (ball.basketIndex < 0 || ball.basketIndex >= ScaleSystem.Inst.Baskets.Count ||
                    ScaleSystem.Inst.Baskets[ball.basketIndex].Sensor == null)))
            {
                Debug.LogError($"StageManager: Stage {Stage}의 공 level 및 월드 x/y 좌표를 확인하세요. " +
                    "x/y 좌표와 positionSpace 및 basketIndex를 확인하세요.");
                yield break;
            }
        }
        ApplyStageSettings(stageData);
        ConfigurePuzzle(stageData);
        SpawnJsonBalls(stageData);

        Physics2D.SyncTransforms();
        stageReady = true;
        settleTimer = Mathf.Max(startCountdownSeconds, stageData.settleTime, 0f);
        OnStageStarted?.Invoke(Stage);
        stageRoutine = null;
        if (SaveManager.Inst != null) SaveManager.Inst.SaveGame();
    }

    private void ConfigurePuzzle(StageDefinition definition)
    {
        expectedMass = 0f;
        foreach (StageBallData ball in definition.balls)
            expectedMass += GameManager.Inst.kgList[ball.level];
        foreach (int level in definition.playerBallQueue)
            expectedMass += GameManager.Inst.kgList[level];
        SpawnManager.Inst.RestoreQueues(definition.playerBallQueue, null);
    }

    private IEnumerator EnsureStageMap(int stageNumber)
    {
        int mapId = GetStageMapId(stageNumber);
        if (IsCurrentMap(mapId)) yield break;

        GameObject prefab = GetMapPrefab(mapId);
        if (prefab == null)
        {
            Debug.LogError($"StageManager: Map {mapId} 프리팹을 찾지 못했습니다. Map Prefabs를 연결하세요.");
            yield break;
        }

        CameraFocusController cameraFocus = FindObjectOfType<CameraFocusController>();
        if (cameraFocus != null) cameraFocus.Focus(null);
        if (ScaleSystem.Inst != null)
        {
            ScaleSystem.Inst.gameObject.SetActive(false);
            Destroy(ScaleSystem.Inst.gameObject);
            yield return null;
        }

        Instantiate(prefab);
        yield return null;
        if (cameraFocus != null) cameraFocus.RefreshMapView();
    }

    private bool IsCurrentMap(int mapId)
    {
        if (ScaleSystem.Inst == null) return false;
        int count = ScaleSystem.Inst.Baskets.Count;
        return (mapId == 1 && count == 2) || (mapId == 2 && count == 3) || (mapId == 3 && count == 4);
    }

    private GameObject GetMapPrefab(int mapId)
    {
        int index = mapId - 1;
        GameContentCatalog content = GameManager.Inst != null ? GameManager.Inst.Content : null;
        if (content != null && index >= 0 && index < content.maps.Count)
            return content.maps[index];
        return null;
    }

    private StageDefinition FindStageData(int stageNumber)
    {
        StageDefinition definition;
        return definitionsByStage.TryGetValue(stageNumber, out definition) ? definition : null;
    }

    public int GetStageMapId(int stageNumber)
    {
        StageDefinition definition = FindStageData(stageNumber);
        return definition != null && definition.mapId > 0 ? definition.mapId : 1;
    }

    // 3/4바구니 맵 에셋은 별도 연결이 필요하다. 다른 맵에 잘못 배치하지 않는다.
    public bool IsStageCompatible(int stageNumber)
    {
        StageDefinition definition = FindStageData(stageNumber);
        if (definition == null || ScaleSystem.Inst == null || GameManager.Inst == null)
            return false;

        int mapId = GetStageMapId(stageNumber);
        int basketCount = mapId == 1 ? 2 : mapId == 2 ? 3 : mapId == 3 ? 4 : 0;
        if (basketCount == 0 || ScaleSystem.Inst.Baskets.Count != basketCount)
        {
            Debug.LogError($"Stage {stageNumber}: Map {mapId}의 {basketCount}바구니 맵이 필요합니다. " +
                "해당 맵을 연결한 뒤 시작하세요.");
            return false;
        }

        // 모든 연결은 좌우 길이가 같은 이진 저울을 전제로 한다.
        ScaleBeam root = ScaleSystem.Inst.Root as ScaleBeam;
        if (root == null || !HasEqualArms(root)) return false;
        HangingNode left = root.Attachments[0].node;
        HangingNode right = root.Attachments[1].node;
        bool valid = mapId == 1
            ? left is Basket && right is Basket
            : mapId == 2
                ? IsBasketPair(left) && right is Basket
                : IsBasketPair(left) && IsBasketPair(right);
        if (!valid)
        {
            Debug.LogError($"Stage {stageNumber}: Map {mapId}의 저울 연결 순서를 확인하세요. " +
                "3바구니 맵은 왼쪽 하위 저울의 바구니 2개, 오른쪽 바구니 1개 순서입니다.");
            return false;
        }

        return true;
    }

    private static bool IsBasketPair(HangingNode node)
    {
        ScaleBeam beam = node as ScaleBeam;
        return beam != null && HasEqualArms(beam) &&
            beam.Attachments[0].node is Basket && beam.Attachments[1].node is Basket;
    }

    private static bool HasEqualArms(ScaleBeam beam)
    {
        if (beam.Attachments.Count != 2) return false;
        Vector2 a = beam.Attachments[0].offset;
        Vector2 b = beam.Attachments[1].offset;
        bool valid = a.x < 0f && b.x > 0f &&
            Mathf.Abs(a.x + b.x) < 0.05f && Mathf.Abs(a.y - b.y) < 0.05f;
        if (!valid) Debug.LogError("스테이지 데이터는 연결 순서가 왼쪽/오른쪽이며 좌우 팔 길이가 같은 저울을 전제로 합니다.");
        return valid;
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
            if (ballData.positionSpace == BallPositionSpace.BasketSensor)
                worldPosition = ScaleSystem.Inst.Baskets[ballData.basketIndex].Sensor.transform
                    .TransformPoint(worldPosition);
            GameObject ball = Instantiate(
                GameManager.Inst.ballList[ballData.level],
                worldPosition,
                Quaternion.Euler(0f, 0f, ballData.rotZ));

            Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.velocity = Vector2.zero;
                rb.angularVelocity = 0f;
            }

            // 바구니 기준 좌표의 공은 그 바구니에 속한다. 월드 좌표의 공은 위치로 정한다.
            Basket owner = ballData.positionSpace == BallPositionSpace.BasketSensor
                ? ScaleSystem.Inst.Baskets[ballData.basketIndex]
                : null;
            SpawnManager.Inst.SetupBallProperties(ball, ballData.level, false, owner);
            ball.name = $"Stage{stageData.stage}_Ball{ballData.ballId}_Level{ballData.level}";
            RegisterBallData(ball.GetComponent<BallBehaviour>(), ballData);
            spawnedCount++;
        }

        Debug.Log($"StageManager: Stage {stageData.stage} - JSON 공 {spawnedCount}/{stageData.balls.Count}개 생성");
    }

    // 지정한 스테이지로 바로 시작한다. 없는 스테이지면 1스테이지로 시작한다.
    public void StartStage(int stage)
    {
        if (FindStageData(stage) == null)
        {
            Debug.LogWarning($"StageManager: Stage {stage} 데이터가 없어 Stage 1로 시작합니다.");
            stage = 1;
        }
        Stage = stage;
        BeginStage();
    }

    public void NextStage()
    {
        if (FindStageData(Stage + 1) == null)
        {
            Debug.Log("마지막으로 등록된 스테이지입니다. StageData.json에 다음 스테이지를 추가하세요.");
            return;
        }
        Stage++;
        BeginStage();
    }

    // 게임오버 후 같은 스테이지를 처음부터 다시 한다.
    public void RetryStage()
    {
        if (GameManager.Inst == null)
            return;

        GameManager.Inst.ResetGameOver();
        BeginStage();
    }

    // 저장된 게임을 불러오기 전에 해당 스테이지 맵을 준비한다. 공은 SaveManager가 배치한다.
    public void RestoreStage(int stage, Action<bool> completed)
    {
        Stage = FindStageData(stage) != null ? stage : 1;
        StartStageRoutine(RestoreStageRoutine(completed));
    }

    private IEnumerator RestoreStageRoutine(Action<bool> completed)
    {
        stageReady = false;
        IsCleared = false;
        IsAttemptFailed = false;
        exhaustedTimer = 0f;
        holdTimer = 0f;
        ClearBalls();
        ballDataByInstance.Clear();
        if (SpawnManager.Inst != null) SpawnManager.Inst.ResetForStage();

        yield return EnsureStageMap(Stage);
        stageReady = IsStageCompatible(Stage);
        StageDefinition definition = FindStageData(Stage);
        if (definition != null)
        {
            ApplyStageSettings(definition);
            ConfigurePuzzle(definition);
        }
        settleTimer = definition != null ? Mathf.Max(startCountdownSeconds, definition.settleTime, 0f) : 0f;
        if (stageReady) OnStageStarted?.Invoke(Stage);
        stageRoutine = null;
        completed?.Invoke(stageReady);
    }

    // 초기 배치와 저장 복원 모두 같은 잠금 상태를 적용한다.
    public static void RegisterBallData(BallBehaviour ball, StageBallData data)
    {
        if (ball == null || data == null) return;
        ballDataByInstance[ball] = data;
        BallMergeLock.SetMergeAllowed(ball.gameObject, data.canMerge);
    }

    public static StageBallData CaptureBallData(BallBehaviour ball, int fallbackId)
    {
        StageBallData original;
        ballDataByInstance.TryGetValue(ball, out original);
        if (original == null)
        {
            int maxId = 0;
            foreach (StageBallData data in ballDataByInstance.Values)
                maxId = Mathf.Max(maxId, data.ballId);
            fallbackId += maxId;
        }
        return new StageBallData
        {
            ballId = original != null ? original.ballId : fallbackId,
            level = ball.level,
            x = ball.transform.position.x,
            y = ball.transform.position.y,
            rotZ = ball.transform.eulerAngles.z,
            canMerge = BallMergeLock.CanMerge(ball.gameObject)
        };
    }

    private static void ClearBalls()
    {
        foreach (BallBehaviour ball in FindObjectsOfType<BallBehaviour>())
        {
            ball.gameObject.SetActive(false);
            Destroy(ball.gameObject);
        }
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

        string status = !stageReady ? "MAP / DATA REQUIRED" :
            IsSettling ? $"READY {CountdownSeconds}" : $"LEVEL {HoldProgress * 100f:F0}%";
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
            GUI.Label(center, HasNextStage ? "STAGE CLEAR" : "CAMPAIGN COMPLETE", big);
            if (HasNextStage && GUI.Button(buttonRect, "NEXT", button))
                NextStage();
        }
        else if (GameManager.Inst.gameOver || IsAttemptFailed)
        {
            GUI.Label(center, IsAttemptFailed ? "TRY AGAIN" : "GAME OVER", big);
            if (GUI.Button(buttonRect, "RETRY", button))
                RetryStage();
        }
    }
}

[Serializable]
public class StageDatabase
{
    public int schemaVersion;
    public List<StageMapDefinition> maps;
    public List<StageDefinition> stages = new List<StageDefinition>();
}

[Serializable]
public class StageMapDefinition
{
    public int mapId;
    public string name;
    public int basketCount;
    public List<int> targetWeightRatios;
}

[Serializable]
public class StageDefinition
{
    public string title;
    public List<int> playerBallQueue = new List<int>();
    public int mapId;
    public int difficulty;
    public string patternId;
    public int stage;
    public float minImbalance;
    public float maxImbalance;
    public float levelTolerance = 3f;
    public float holdTime = 2f;
    public float settleTime = 2.5f;
    public int forcedMergeTargetBallId = -1;
    public List<StageBallData> balls = new List<StageBallData>();
}

public enum BallPositionSpace
{
    World = 0,
    BasketSensor = 1
}

[Serializable]
public class StageBallData
{
    public BallPositionSpace positionSpace = BallPositionSpace.World;
    public int basketIndex;
    public int ballId;
    public int level;
    public float x = float.NaN;
    public float y = float.NaN;
    public float rotZ;
    public bool canMerge = true;
}
