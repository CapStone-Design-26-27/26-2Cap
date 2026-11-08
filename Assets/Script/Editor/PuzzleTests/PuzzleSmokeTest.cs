using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// CLI 진입점: -batchmode -executeMethod PuzzleSmokeTest.Run
// 테스트 단계는 PuzzleSmokeScenario, 파일 경로는 PuzzleTestPaths에서 관리한다.
[InitializeOnLoad]
public static class PuzzleSmokeTest
{
    private static PuzzleSmokeScenario scenario;

    static PuzzleSmokeTest()
    {
        // Play Mode 진입 시 도메인이 다시 로드되어도 명시적으로 시작한 검사만 재개한다.
        if (Application.isBatchMode && File.Exists(PuzzleTestPaths.MarkerFile)) Watch();
    }

    public static void Run()
    {
        if (!Application.isBatchMode) throw new InvalidOperationException("Run smoke tests in batch mode.");
        PrepareTestFiles();
        EditorSceneManager.OpenScene(PuzzleTestPaths.SceneAsset);
        Watch();
        EditorApplication.EnterPlaymode();
    }

    private static void PrepareTestFiles()
    {
        Directory.CreateDirectory(PuzzleTestPaths.WorkingDirectory);
        Directory.CreateDirectory(PuzzlePaths.BatchResultsDirectory);
        if (File.Exists(PuzzleTestPaths.SaveFile)) File.Delete(PuzzleTestPaths.SaveFile);
        if (File.Exists(PuzzleTestPaths.ReportFile)) File.Delete(PuzzleTestPaths.ReportFile);
        File.WriteAllText(PuzzleTestPaths.MarkerFile, "Puzzle smoke test running");
    }

    private static void Watch()
    {
        if (scenario != null) return;
        scenario = new PuzzleSmokeScenario();
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        try
        {
            scenario.Tick();
            if (scenario.IsComplete)
                Finish(true, "Catalog, initial spawn, NEXT, aiming, stage progression, and save/restore passed.");
        }
        catch (Exception exception)
        {
            Finish(false, exception.ToString());
        }
    }

    private static void Finish(bool passed, string message)
    {
        EditorApplication.update -= Tick;
        try
        {
            if (File.Exists(PuzzleTestPaths.MarkerFile)) File.Delete(PuzzleTestPaths.MarkerFile);
            Directory.CreateDirectory(PuzzlePaths.BatchResultsDirectory);
            var result = new SmokeTestResult
            {
                passed = passed,
                message = message,
                completedAtUtc = DateTime.UtcNow.ToString("O")
            };
            File.WriteAllText(PuzzleTestPaths.ReportFile, JsonUtility.ToJson(result, true));
        }
        catch (Exception exception)
        {
            passed = false;
            Debug.LogException(exception);
        }

        if (passed) Debug.Log("PUZZLE SMOKE PASS: " + message);
        else Debug.LogError("PUZZLE SMOKE FAIL: " + message);
        EditorApplication.Exit(passed ? 0 : 1);
    }

    [Serializable]
    private sealed class SmokeTestResult
    {
        public bool passed;
        public string message;
        public string completedAtUtc;
    }
}
