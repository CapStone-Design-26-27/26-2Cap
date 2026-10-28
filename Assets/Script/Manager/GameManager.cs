using System.Collections.Generic;
using UnityEngine;
using System;

public class GameManager : Singleton<GameManager>
{
    public bool gameOver { get; private set; } = false;

    public bool gamePaused { get; set; } = false;

    // 현재 카메라가 집중해서 보고 있는 바구니. null이면 전체 보기
    public Basket FocusedBasket { get; set; }

    [field: SerializeField]
    public List<GameObject> ballList { get; private set; } = new List<GameObject>();

    [field: SerializeField]
    public List<float> kgList { get; private set; } = new List<float>() { 5f, 10f, 15f, 20f, 25f, 30f, 35f, 40f, 45f, 50f };

    protected override void DoAwake()
    {
        // 씬에 직렬화된 예전 값이 남아 있어도 게임에서 사용하는 무게는 이 목록으로 통일한다.
        kgList = new List<float>() { 5f, 10f, 15f, 20f, 25f, 30f, 35f, 40f, 45f, 50f };
    }

    // 튜토리얼 코드: 튜토리얼을 끝내지 않았으면 튜토리얼 씬부터 시작한다.
    [Header("튜토리얼")]
    [SerializeField] private bool redirectToTutorial = true;

    // 저장된 공이 있으면 이어서 하고, 없으면 새 스테이지를 만든다.
    private void Start()
    {
        // 튜토리얼 코드: 튜토리얼 씬에서는 TutorialManager가 진행한다.
        if (TutorialManager.Inst != null)
            return;

        // 튜토리얼 코드: 튜토리얼을 끝내지 않았으면 튜토리얼 씬으로 넘어간다.
        if (redirectToTutorial && !TutorialManager.IsCompleted)
        {
            SceneFlow.LoadTutorial();
            return;
        }

        if (StageManager.Inst == null)
            gameObject.AddComponent<StageManager>();

        // 메인 화면에서 스테이지를 지정했으면 저장된 진행 대신 그 스테이지로 시작한다.
        if (SceneFlow.PendingStage > 0)
        {
            int stage = SceneFlow.PendingStage;
            SceneFlow.PendingStage = 0;
            Debug.Log($"[GameManager] 지정한 Stage {stage}로 시작합니다.");
            StageManager.Inst.StartStage(stage);
            return;
        }

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
