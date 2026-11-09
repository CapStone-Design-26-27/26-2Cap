using UnityEngine;
using UnityEngine.SceneManagement;

// 씬 이동과 씬 사이에 넘길 값을 관리한다.
public static class SceneFlow
{
    public const string MainScene = "MainScene";
    public const string GameScene = "TestScene";
    public const string TutorialScene = "TutoScene";

    // 다음 게임 씬에서 시작할 스테이지. 0이면 저장된 진행을 이어간다.
    public static int PendingStage { get; set; }

    // 시작 버튼. 튜토리얼을 끝내지 않았으면 튜토리얼부터 한다.
    public static void StartGame()
    {
        if (TutorialManager.IsCompleted) LoadGame();
        else LoadTutorial();
    }

    public static void LoadMain() => Load(MainScene);
    public static void LoadGame() => Load(GameScene);
    public static void LoadTutorial() => Load(TutorialScene);

    // 매니저가 DontDestroyOnLoad라 지워야 다음 씬의 매니저가 새로 시작한다.
    public static void Load(string sceneName)
    {
        if (GameManager.Inst != null)
            Object.Destroy(GameManager.Inst.gameObject);

        SceneManager.LoadScene(sceneName);
    }
}
