using System.IO;
using UnityEngine;

// 퍼즐 기능의 에셋/저장 경로. Editor 테스트도 같은 저장 경로를 참조한다.
public static class PuzzlePaths
{
    public const string ContentCatalogAsset = "Assets/Aset/Data/GameContent.asset";
    public const string SaveFileName = "savefile-puzzle-local.json";
    private const string BatchResultsFolder = "TestResults/Puzzle";

    public static string ProjectDirectory => Path.GetDirectoryName(Application.dataPath);
    public static string BatchResultsDirectory => Path.Combine(ProjectDirectory, BatchResultsFolder);
    public static string BatchSaveFile => Path.Combine(BatchResultsDirectory, "save.json");

    public static string SaveFile => Application.isEditor && Application.isBatchMode
        ? BatchSaveFile
        : Path.Combine(Application.persistentDataPath, SaveFileName);
}
