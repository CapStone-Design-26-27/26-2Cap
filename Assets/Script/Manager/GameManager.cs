using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : Singleton<GameManager>
{
    public static event Action<int> OnScoreChanged;
    public int score { get; private set; } = 0;
    public int currentRound { get; private set; }
    public bool gameOver { get; private set; } = false;

    public bool gamePaused { get; set; } = false;

    // 현재 카메라가 집중해서 보고 있는 바구니. null이면 전체 보기
    public Basket FocusedBasket { get; set; }

    [field: SerializeField]
    public List<GameObject> ballList { get; private set; } = new List<GameObject>();

    [field: SerializeField]
    public List<float> kgList { get; private set; } = new List<float>() { 0.5f, 1.0f, 2.0f, 3.5f, 5.0f, 7.0f, 9.0f, 12.0f, 13.0f, 14.0f };

    // 저장된 공이 있으면 이어서 하고, 없으면 새 스테이지를 만든다.
    private void Start()
    {
        if (StageManager.Inst == null)
            gameObject.AddComponent<StageManager>();

        if (!SaveManager.Inst.LoadGame())
            StageManager.Inst.BeginStage();
    }

    private void OnApplicationQuit()
    {
        SaveManager.Inst.SaveGame();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveManager.Inst.SaveGame();
        }
    }

    public void TriggerGameOver()
    {
        if (gameOver) return;

        gameOver = true;
        SaveManager.Inst.DeleteSave();
    }

    public void ResetGameOver()
    {
        gameOver = false;
    }

    private int RoundCheck(int currentScore)
    {
        int round = currentScore / 500;
        if (round > 3) return 3;
        return round;
    }

    public void AddScore(int addedScore)
    {
        score += addedScore;
        currentRound = RoundCheck(score);
        OnScoreChanged?.Invoke(score);
    }

    public void SetScoreFromLoad(int savedScore)
    {
        score = savedScore;
        currentRound = RoundCheck(score);
        OnScoreChanged?.Invoke(score);
    }
}
