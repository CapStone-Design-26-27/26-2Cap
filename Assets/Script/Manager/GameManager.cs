using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public class GameManager : Singleton<GameManager>
{
    public bool gameOver { get; private set; } = false;

    public bool gamePaused { get; set; } = false;

    // 현재 카메라가 집중해서 보고 있는 바구니. null이면 전체 보기
    public Basket FocusedBasket { get; set; }

    [field: SerializeField]
    public List<GameObject> ballList { get; private set; } = new List<GameObject>();

    [field: SerializeField]
    public List<float> kgList { get; private set; } = new List<float>() { 0.5f, 1.0f, 2.0f, 3.5f, 5.0f, 7.0f, 9.0f, 12.0f, 13.0f, 14.0f };

    // 튜토리얼 코드: 튜토리얼을 끝내지 않았으면 튜토리얼 씬부터 시작한다.
    [Header("튜토리얼")]
    [SerializeField] private bool redirectToTutorial = true;
    [SerializeField] private string tutorialSceneName = "TutoScene";

    // 저장된 공이 있으면 이어서 하고, 없으면 새 스테이지를 만든다.
    private void Start()
    {
        // 튜토리얼 코드: 튜토리얼 씬에서는 TutorialManager가 진행한다.
        if (TutorialManager.Inst != null)
            return;

        // 튜토리얼 코드: 매니저가 DontDestroyOnLoad라 지운 뒤 튜토리얼 씬으로 넘어간다.
        if (redirectToTutorial && !TutorialManager.IsCompleted)
        {
            Destroy(gameObject);
            SceneManager.LoadScene(tutorialSceneName);
            return;
        }

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

        // 튜토리얼 코드: 게임오버 대신 처음 배치로 되돌린다.
        if (TutorialManager.Inst != null)
        {
            TutorialManager.Inst.OnGameOverRequested();
            return;
        }

        gameOver = true;
        SaveManager.Inst.DeleteSave();
    }

    public void ResetGameOver()
    {
        gameOver = false;
    }

}
