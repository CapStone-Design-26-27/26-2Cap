using UnityEngine;
using System.IO;

// 진행 상황(지금 할 스테이지 번호)만 저장한다.
// 공 배치, 남은 큐, 저울 기울기는 저장하지 않는다. 스테이지는 항상 StageData.json의
// 정해진 공 개수, 레벨, 위치로 새로 만들고, 플레이어는 그 위에 공을 떨어뜨린다.
public class SaveManager : Singleton<SaveManager>
{
    [System.Serializable]
    private class GameSaveData
    {
        // 6: 스테이지 번호만 저장. 이전 버전(5 이하) 파일의 공/큐/각도 정보는 읽지 않고 무시한다.
        public int schemaVersion = 6;
        public int stage;
    }

    // 로컬 퍼즐 실험은 기존 게임 저장 파일과 분리한다.
    public string SavePath => PuzzlePaths.SaveFile;

    // 저장 파일의 스테이지 번호만 읽는다. 파일이 없거나 읽을 수 없으면 1을 돌려준다.
    public static int PeekSavedStage()
    {
        int stage = ReadSavedStage();
        return stage > 0 ? stage : 1;
    }

    // 저장된 스테이지 번호. 파일이 없거나 읽을 수 없으면 0
    private static int ReadSavedStage()
    {
        try
        {
            if (!File.Exists(PuzzlePaths.SaveFile)) return 0;
            GameSaveData data = JsonUtility.FromJson<GameSaveData>(File.ReadAllText(PuzzlePaths.SaveFile));
            return data != null && data.stage > 0 ? data.stage : 0;
        }
        catch (System.Exception exception) when (exception is IOException ||
            exception is System.ArgumentException || exception is System.UnauthorizedAccessException)
        {
            Debug.LogWarning("저장 파일을 읽을 수 없습니다: " + exception.Message);
            return 0;
        }
    }

    public void SaveGame()
    {
        // 튜토리얼 코드: 튜토리얼 중에는 저장하지 않는다.
        if (TutorialManager.Inst != null) return;

        StageManager stageManager = StageManager.Inst;
        if (stageManager == null) return;

        // 클리어했으면 다음 스테이지 번호를 저장해, 다시 켰을 때 새 스테이지로 시작한다.
        bool cleared = stageManager.IsCleared;
        GameSaveData data = new GameSaveData
        {
            stage = stageManager.Stage + (cleared && stageManager.HasNextStage ? 1 : 0)
        };

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            File.WriteAllText(SavePath, JsonUtility.ToJson(data, true));
            Debug.Log($"진행 저장 완료 (Stage {data.stage}): {SavePath}");
        }
        catch (System.Exception exception) when (exception is IOException ||
            exception is System.UnauthorizedAccessException)
        {
            Debug.LogWarning("진행을 저장할 수 없습니다: " + exception.Message);
        }
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
