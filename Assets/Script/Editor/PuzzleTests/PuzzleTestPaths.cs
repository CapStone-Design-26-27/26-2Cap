using System.IO;

// 실행 위치와 관계없이 프로젝트 루트 기준으로 테스트 파일을 만든다.
internal static class PuzzleTestPaths
{
    public const string SceneAsset = "Assets/Scenes/TestScene.unity";
    private const string WorkingFolder = "Temp/PuzzleVerification";

    public static string WorkingDirectory => Path.Combine(PuzzlePaths.ProjectDirectory, WorkingFolder);
    public static string MarkerFile => Path.Combine(WorkingDirectory, "Smoke.running");
    public static string ReportFile => Path.Combine(PuzzlePaths.BatchResultsDirectory, "Smoke-result.json");
    public static string SaveFile => PuzzlePaths.BatchSaveFile;
}
