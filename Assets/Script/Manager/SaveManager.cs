using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class SaveManager : Singleton<SaveManager>
{
    [System.Serializable]
    private class BallSaveData
    {
        public int level;
        public float posX;   // 최상위 고리가 고정이라 월드 좌표로 저장해도 위치가 일치한다.
        public float posY;
        public float rotZ;
    }

    [System.Serializable]
    private class GameSaveData
    {
        public int score;
        public int stage;
        public List<int> nextBalls;
        public List<int> retryBalls;     // 재투척 대기 중인 공
        public List<float> nodeAngles;   // ScaleSystem.AllNodes 순서의 저울대, 바구니 각도
        public List<BallSaveData> balls;
    }

    private string SavePath => Path.Combine(Application.persistentDataPath, "savefile.json");

    public void SaveGame()
    {
        if (GameManager.Inst.gameOver) return;

        // 튜토리얼 코드: 튜토리얼 중에는 저장하지 않는다.
        if (TutorialManager.Inst != null) return;

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
            score = GameManager.Inst.score,
            stage = stageManager == null ? 1 : stageManager.Stage + (cleared ? 1 : 0),
            nextBalls = new List<int>(SpawnManager.Inst.nextBallQueue),
            retryBalls = cleared ? new List<int>() : SpawnManager.Inst.GetRetryLevels(),
            nodeAngles = cleared ? null : ScaleSystem.Inst.GetAngles(),
            balls = new List<BallSaveData>()
        };

        BallBehaviour[] allBalls = cleared ? new BallBehaviour[0] : FindObjectsOfType<BallBehaviour>();

        foreach (BallBehaviour ball in allBalls)
        {
            if (ball.isMerged) continue;

            Rigidbody2D rb = ball.GetComponent<Rigidbody2D>();
            if (rb == null || rb.isKinematic) continue; // 조준 중인 공 제외

            data.balls.Add(new BallSaveData
            {
                level = ball.level,
                posX = ball.transform.position.x,
                posY = ball.transform.position.y,
                rotZ = ball.transform.eulerAngles.z
            });
        }

        string json = JsonUtility.ToJson(data, true);
        File.WriteAllText(SavePath, json);
        Debug.Log("게임 저장 완료: " + SavePath);
    }

    // 저장된 공을 배치했으면 true. 파일이 없거나 공이 하나도 없으면 false를 돌려
    // 호출한 쪽이 새 스테이지를 만들게 한다. 점수와 스테이지 번호는 공이 없어도 복원한다.
    public bool LoadGame()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("저장된 파일이 없습니다. 새로 시작합니다.");
            return false;
        }

        GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(SavePath));
        if (data == null)
        {
            Debug.LogWarning("저장 파일을 읽을 수 없습니다. 새로 시작합니다.");
            return false;
        }

        if (ScaleSystem.Inst == null)
        {
            Debug.LogError("씬에 ScaleSystem이 없습니다.");
            return false;
        }

        GameManager.Inst.SetScoreFromLoad(data.score);
        SpawnManager.Inst.RestoreQueues(data.nextBalls, data.retryBalls);

        bool hasBalls = data.balls != null && data.balls.Count > 0;
        if (StageManager.Inst != null)
            StageManager.Inst.RestoreStage(data.stage);

        if (!hasBalls)
            return false;

        // 공보다 저울 자세를 먼저 복원해야 공이 바구니 안의 제자리에 놓인다.
        if (!ScaleSystem.Inst.ApplyAngles(data.nodeAngles))
            Debug.LogWarning("저울 구조가 저장 당시와 달라 기울기는 초기 상태로 시작합니다.");

        if (data.balls != null)
        {
            foreach (BallSaveData ballData in data.balls)
            {
                if (ballData.level < 0 || ballData.level >= GameManager.Inst.ballList.Count) continue;

                GameObject newBall = Instantiate(
                    GameManager.Inst.ballList[ballData.level],
                    new Vector3(ballData.posX, ballData.posY, 0f),
                    Quaternion.Euler(0f, 0f, ballData.rotZ));

                SpawnManager.Inst.SetupBallProperties(newBall, ballData.level, false);

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
        return true;
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
