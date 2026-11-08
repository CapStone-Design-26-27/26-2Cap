using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEngine.SceneManagement;

public class GameManager : Singleton<GameManager>
{
    [Header("Aset 게임 데이터")]
    [SerializeField] private GameContentCatalog gameContent;
    public GameContentCatalog Content => gameContent;
    public bool gameOver { get; private set; } = false;

    public bool gamePaused { get; set; } = false;

    // 현재 카메라가 집중해서 보고 있는 바구니. null이면 전체 보기
    public Basket FocusedBasket { get; set; }

    [field: SerializeField]
    public List<GameObject> ballList { get; private set; } = new List<GameObject>();

    [field: SerializeField]
    public List<float> kgList { get; private set; } = CreateBallWeights();

    // 무게를 실험할 때 이 목록만 수정한다. JSON은 공 level과 순서만 관리한다.
    private static List<float> CreateBallWeights()
    {
        return new List<float>() { 1f, 2f, 4f, 8f, 16f, 32f, 64f, 128f, 256f, 512f };
    }

    protected override void DoAwake()
    {
        // 씬에 직렬화된 예전 값이 남아 있어도 게임에서 사용하는 무게는 이 목록으로 통일한다.
        kgList = CreateBallWeights();
#if UNITY_EDITOR
        if (gameContent == null)
            gameContent = UnityEditor.AssetDatabase.LoadAssetAtPath<GameContentCatalog>(PuzzlePaths.ContentCatalogAsset);
#endif
    }

    // 튜토리얼 코드: 튜토리얼을 끝내지 않았으면 튜토리얼 씬부터 시작한다.
    [Header("튜토리얼")]
    [SerializeField] private bool redirectToTutorial = true;
    [SerializeField] private string tutorialSceneName = "TutoScene";
    [Tooltip("에디터에서 로컬 퍼즐 실험 시 튜토리얼을 건너뛴다.")]
    [SerializeField] private bool localPuzzleExperiment = true;

    // 저장된 공이 있으면 이어서 하고, 없으면 새 스테이지를 만든다.
    private void Start()
    {
        // 튜토리얼 코드: 튜토리얼 씬에서는 TutorialManager가 진행한다.
        if (TutorialManager.Inst != null)
            return;

        // 튜토리얼 코드: 매니저가 DontDestroyOnLoad라 지운 뒤 튜토리얼 씬으로 넘어간다.
        bool shouldRedirect = redirectToTutorial && !TutorialManager.IsCompleted;
#if UNITY_EDITOR
        if (localPuzzleExperiment) shouldRedirect = false;
#endif
        if (shouldRedirect)
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
