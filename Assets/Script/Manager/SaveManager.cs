using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class SaveManager : Singleton<SaveManager>
{
    [System.Serializable]
    private class GameSaveData
    {
        public int schemaVersion = 3;
        public int stage;
        public int mapId;
        public int stageDataVersion;
        public List<int> nextBalls;
        public List<int> retryBalls;
        public List<float> nodeAngles;
        public List<StageBallData> balls;
    }

    // 기존 savefile.json의 posX/posY를 새 x/y 형식으로 옮긴다.
    [System.Serializable]
    private class LegacyBallData
    {
        public int level;
        public float posX;
        public float posY;
        public float rotZ;
    }

    [System.Serializable]
    private class LegacySaveData
    {
        public int schemaVersion;
        public List<LegacyBallData> balls;
    }

    private string SavePath => Path.Combine(Application.persistentDataPath, "savefile.json");

    public void SaveGame()
    {
        if (GameManager.Inst == null || GameManager.Inst.gameOver || SpawnManager.Inst == null) return;

        // 튜토리얼 코드: 튜토리얼 중에는 저장하지 않는다.
        if (TutorialManager.Inst != null) return;
        if (StageManager.Inst != null && !StageManager.Inst.IsStageReady) return;

        if (ScaleSystem.Inst == null)
        {
            Debug.LogError("씬에 ScaleSystem이 없습니다.");
            return;
        }

        StageManager stageManager = StageManager.Inst;
        bool cleared = stageManager != null && stageManager.IsCleared;

        // 클리어한 상태로 종료하면 다음 스테이지 번호만 저장해, 다시 켰을 때 새 스테이지로 시작한다.
        GameSaveData data = new GameSaveData
        {
            stage = stageManager == null ? 1 : stageManager.Stage + (cleared && stageManager.HasNextStage ? 1 : 0),
            nextBalls = new List<int>(SpawnManager.Inst.nextBallQueue),
            retryBalls = cleared ? new List<int>() : SpawnManager.Inst.GetRetryLevels(),
            nodeAngles = cleared ? null : ScaleSystem.Inst.GetAngles(),
            balls = new List<StageBallData>()
        };

        BallBehaviour[] allBalls = cleared ? new BallBehaviour[0] : FindObjectsOfType<BallBehaviour>();

        foreach (BallBehaviour ball in allBalls)
        {
            if (ball.isMerged) continue;

            Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
            if (rb == null || rb.isKinematic) continue; // 조준 중인 공 제외

            data.balls.Add(StageManager.CaptureBallData(ball, data.balls.Count + 1));
        }

        data.mapId = stageManager != null ? stageManager.GetStageMapId(data.stage) : 1;
        data.stageDataVersion = 4;
        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("게임 저장 완료: " + SavePath);
    }

    // 저장된 공을 배치했으면 true. 파일이 없거나 공이 하나도 없으면 false를 돌려
    // 호출한 쪽이 새 스테이지를 만들게 한다. 스테이지 번호는 공이 없어도 복원한다.
    public bool LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("저장된 파일이 없습니다. 새로 시작합니다.");
            return false;
        }

        GameSaveData data;
        try
        {
            string json = File.ReadAllText(SavePath);
            data = JsonUtility.FromJson<GameSaveData>(json);
            LegacySaveData legacy = JsonUtility.FromJson<LegacySaveData>(json);
            if (data != null && legacy != null && legacy.schemaVersion < 2 && legacy.balls != null)
            {
                data.balls = new List<StageBallData>();
                foreach (LegacyBallData ball in legacy.balls)
                    data.balls.Add(new StageBallData
                    {
                        ballId = data.balls.Count + 1, level = ball.level,
                        x = ball.posX, y = ball.posY, rotZ = ball.rotZ, canMerge = true
                    });
            }
        }
        catch (System.Exception exception) when (exception is IOException ||
            exception is System.ArgumentException || exception is System.UnauthorizedAccessException)
        {
            Debug.LogWarning("저장 파일을 읽을 수 없습니다: " + exception.Message);
            return false;
        }
        if (data == null)
        {
            Debug.LogWarning("저장 파일을 읽을 수 없습니다. 새로 시작합니다.");
            return false;
        }

        if (ScaleSystem.Inst == null || GameManager.Inst == null || SpawnManager.Inst == null)
        {
            Debug.LogError("저장 복원에 필요한 매니저가 없습니다.");
            return false;
        }
        if (data.balls != null)
        {
            foreach (StageBallData ball in data.balls)
            {
                if (ball == null || float.IsNaN(ball.x) || float.IsInfinity(ball.x) ||
                    float.IsNaN(ball.y) || float.IsInfinity(ball.y) ||
                    ball.level < 0 || ball.level >= GameManager.Inst.ballList.Count ||
                    ball.positionSpace != BallPositionSpace.World)
                {
                    Debug.LogWarning("저장 파일의 공 좌표 또는 레벨이 잘못되었습니다.");
                    return false;
                }
            }
        }
        if (StageManager.Inst != null)
        {
            StageManager.Inst.RestoreStage(data.stage, restored => CompleteLoad(data, restored));
            return true;
        }
        return false;
    }

    private void CompleteLoad(GameSaveData data, bool restored)
    {
        if (!restored)
        {
            StageManager.Inst.BeginStage();
            return;
        }
        if (data.mapId > 0 && data.mapId != StageManager.Inst.CurrentMapId)
        {
            Debug.LogWarning("저장된 맵과 현재 스테이지 맵이 다릅니다. 새 배치로 시작합니다.");
            StageManager.Inst.BeginStage();
            return;
        }
        // 새 라운드 구성이 적용되면 이전 배치를 그대로 복원하지 않는다.
        if (data.stageDataVersion != 4)
        {
            StageManager.Inst.BeginStage();
            return;
        }
        SpawnManager.Inst.RestoreQueues(data.nextBalls, data.retryBalls);

        bool hasBalls = data.balls != null && data.balls.Count > 0;
        if (!hasBalls)
        {
            StageManager.Inst.BeginStage();
            return;
        }

        // 공보다 저울 자세를 먼저 복원해야 공이 바구니 안의 제자리에 놓인다.
        if (!ScaleSystem.Inst.ApplyAngles(data.nodeAngles))
            Debug.LogWarning("저울 구조가 저장 당시와 달라 기울기는 초기 상태로 시작합니다.");

        if (data.balls != null)
        {
            foreach (StageBallData ballData in data.balls)
            {
                if (ballData.level < 0 || ballData.level >= GameManager.Inst.ballList.Count) continue;

                GameObject newBall = Instantiate(
                    GameManager.Inst.ballList[ballData.level],
                    new Vector3(ballData.x, ballData.y, 0f),
                    Quaternion.Euler(0f, 0f, ballData.rotZ));

                SpawnManager.Inst.SetupBallProperties(newBall, ballData.level, false);
                StageManager.RegisterBallData(newBall.GetComponent<BallBehaviour>(), ballData);

                Rigidbody2D rb = newBall.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    rb.velocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }
            }
        }

        Physics2D.SyncTransforms();
        Debug.Log("게임 불러오기 성공!");
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("게임 오버 - 저장 파일 삭제 완료");
        }
    }
}
