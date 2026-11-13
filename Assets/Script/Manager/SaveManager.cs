using UnityEngine;
using System.IO;

// 진행 상황(지금 할 스테이지 번호와 지금까지 도달한 마지막 스테이지 번호)만 저장한다.
// 공 배치, 남은 큐, 저울 기울기는 저장하지 않는다. 스테이지는 항상 StageData.json의
// 정해진 공 개수, 레벨, 위치로 새로 만들고, 플레이어는 그 위에 공을 떨어뜨린다.
public class SaveManager : Singleton<SaveManager>
{
    [System.Serializable]
    private class GameSaveData
    {
        // 7: 마지막 스테이지(maxStage) 추가. 6 이하 파일은 stage를 마지막 스테이지로 본다.
        public int schemaVersion = 7;
        // 다음에 시작할 스테이지
        public int stage;
        // 클리어 기준으로 도달한 마지막 스테이지(아직 깨지 않은 첫 스테이지). 메인 화면에서 이 번호까지 고를 수 있다.
        public int maxStage;
    }

    // 저장 파일에서 읽은 진행 상황. 값은 항상 1 이상이고 maxStage >= stage다.
    public struct Progress
    {
        public int stage;
        public int maxStage;
    }

    // 로컬 퍼즐 실험은 기존 게임 저장 파일과 분리한다.
    public string SavePath => PuzzlePaths.SaveFile;

    // 저장 파일의 현재 스테이지 번호만 읽는다. 파일이 없거나 읽을 수 없으면 1을 돌려준다.
    public static int PeekSavedStage() => ReadProgress().stage;

    // 지금까지 도달한 마지막 스테이지 번호. 파일이 없거나 읽을 수 없으면 1을 돌려준다.
    public static int PeekMaxStage() => ReadProgress().maxStage;

    // 저장 파일이 없거나 읽을 수 없으면 1스테이지로 본다.
    public static Progress ReadProgress()
    {
        GameSaveData data = ReadSaveData();
        int stage = data != null && data.stage > 0 ? data.stage : 1;
        int maxStage = data != null && data.maxStage > 0 ? data.maxStage : stage;
        return new Progress { stage = stage, maxStage = Mathf.Max(stage, maxStage) };
    }

    // 진행 상황을 바로 저장 파일에 쓴다. 메인 화면(매니저 없음)에서도 쓸 수 있다.
    // maxStage가 stage보다 작으면 stage로 맞춘다.
    public static bool WriteProgress(int stage, int maxStage)
    {
        stage = Mathf.Max(1, stage);
        GameSaveData data = new GameSaveData { stage = stage, maxStage = Mathf.Max(stage, maxStage) };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(PuzzlePaths.SaveFile));
            File.WriteAllText(PuzzlePaths.SaveFile, JsonUtility.ToJson(data, true));
            Debug.Log($"진행 저장 완료 (Stage {data.stage}, 마지막 Stage {data.maxStage}): {PuzzlePaths.SaveFile}");
            return true;
        }
        catch (System.Exception exception) when (exception is IOException ||
            exception is System.UnauthorizedAccessException)
        {
            Debug.LogWarning("진행을 저장할 수 없습니다: " + exception.Message);
            return false;
        }
    }

    // 저장 파일이 없거나 읽을 수 없으면 null
    private static GameSaveData ReadSaveData()
    {
        try
        {
            if (!File.Exists(PuzzlePaths.SaveFile)) return null;
            return JsonUtility.FromJson<GameSaveData>(File.ReadAllText(PuzzlePaths.SaveFile));
        }
        catch (System.Exception exception) when (exception is IOException ||
            exception is System.ArgumentException || exception is System.UnauthorizedAccessException)
        {
            Debug.LogWarning("저장 파일을 읽을 수 없습니다: " + exception.Message);
            return null;
        }
    }

    // 저장된 스테이지 번호. 파일이 없거나 읽을 수 없으면 0
    private static int ReadSavedStage()
    {
        GameSaveData data = ReadSaveData();
        return data != null && data.stage > 0 ? data.stage : 0;
    }

    public void SaveGame()
    {
        // 튜토리얼 코드: 튜토리얼 중에는 저장하지 않는다.
        if (TutorialManager.Inst != null) return;

        StageManager stageManager = StageManager.Inst;
        if (stageManager == null) return;

        // 클리어했으면 다음 스테이지 번호를 저장해, 다시 켰을 때 새 스테이지로 시작한다.
        bool cleared = stageManager.IsCleared;
        int stage = stageManager.Stage + (cleared && stageManager.HasNextStage ? 1 : 0);

        // 이전 스테이지를 다시 하는 중이어도 마지막 스테이지는 줄어들지 않는다.
        GameSaveData previous = ReadSaveData();
        int previousMax = previous == null ? 0 : Mathf.Max(previous.stage, previous.maxStage);
        WriteProgress(stage, Mathf.Max(previousMax, stage));
    }

    // 저장된 스테이지 번호로 스테이지를 새로 시작했으면 true.
    // 저장 파일이 없으면 false를 돌려 호출한 쪽이 1스테이지부터 시작하게 한다.
    public bool LoadGame()
    {
        int stage = ReadSavedStage();
        if (stage <= 0)
        {
            Debug.Log("저장된 진행이 없습니다. 처음부터 시작합니다.");
            return false;
        }
        if (StageManager.Inst == null)
        {
            Debug.LogError("StageManager가 없어 저장된 진행을 불러올 수 없습니다.");
            return false;
        }

        Debug.Log($"저장된 진행을 불러옵니다: Stage {stage} (StageData 배치로 새로 시작)");
        StageManager.Inst.StartStage(stage);
        return true;
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
        {
            File.Delete(SavePath);
            Debug.Log("저장 파일 삭제 완료");
        }
    }
}
