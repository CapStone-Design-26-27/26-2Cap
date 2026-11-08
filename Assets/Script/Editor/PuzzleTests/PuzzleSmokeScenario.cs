using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

// 검증 순서: 초기 상태/조준 → 클리어 → 다음 스테이지 → 저장 → 복원.
internal sealed class PuzzleSmokeScenario
{
    private enum Step
    {
        VerifyInitialState,
        WaitForFirstClear,
        VerifySecondStage,
        WaitForLanding,
        VerifyRestoredState,
        Complete
    }

    private const double StartupTimeoutSeconds = 90;
    private const double StepTimeoutSeconds = 35;
    private Step currentStep = Step.VerifyInitialState;
    private double deadline = EditorApplication.timeSinceStartup + StartupTimeoutSeconds;

    public bool IsComplete => currentStep == Step.Complete;

    public void Tick()
    {
        if (EditorApplication.timeSinceStartup > deadline)
            throw new TimeoutException($"Puzzle smoke test timed out: {currentStep}");

        StageManager stage = StageManager.Inst;
        if (!EditorApplication.isPlaying || stage == null || !stage.IsStageReady || stage.IsSettling) return;

        switch (currentStep)
        {
            case Step.VerifyInitialState: VerifyInitialState(stage); break;
            case Step.WaitForFirstClear: WaitForFirstClear(stage); break;
            case Step.VerifySecondStage: VerifySecondStage(stage); break;
            case Step.WaitForLanding: WaitForLanding(); break;
            case Step.VerifyRestoredState: VerifyRestoredState(stage); break;
        }
    }

    private void VerifyInitialState(StageManager stage)
    {
        Require(File.Exists(SaveManager.Inst.SavePath), "New stage must automatically save");
        VerifyContentCatalog();
        Require(stage.Stage == 1, "Expected stage 1");
        Require(SpawnManager.Inst.RemainingBallCount == 1, "Stage 1 must supply one ball");
        Require(stage.ExpectedMass == 2, "Stage 1 total mass must be 2");

        BallBehaviour[] balls = UnityEngine.Object.FindObjectsOfType<BallBehaviour>();
        Require(balls.Length == 1, "One initial ball must exist");
        SpriteRenderer sprite = balls[0].GetComponent<SpriteRenderer>();
        Require(sprite != null && sprite.enabled && sprite.sprite != null, "Initial ball must have a visible sprite");

        VerifyNextPanel();
        VerifyQueueLayout();
        VerifyAimCancellation();
        AimAtBasket(1);
        PuzzleTestAccess.DropAimedBall();
        Require(SpawnManager.Inst.RemainingBallCount == 0, "Dropping must consume exactly one ball");
        AdvanceTo(Step.WaitForFirstClear);
    }

    private static void VerifyContentCatalog()
    {
        GameContentCatalog content = GameManager.Inst.Content;
        Require(content != null && content.stageData != null, "Aset data catalog must be linked");
        Require(content.maps.Count == 3 && content.maps.TrueForAll(map => map != null), "All map references must resolve");
        Require(UnityEngine.Object.FindObjectsOfType<GameStateUI>().Length == 1, "Scene must contain exactly one GameStateUI");
        Require(UnityEngine.Object.FindObjectsOfType<BallQueueUI>().Length == 1, "Scene must contain exactly one BallQueueUI");
        Require(BallQueueUI.Inst.GetComponent<Canvas>() == null && BallQueueUI.Inst.GetComponentInParent<Canvas>() != null,
            "Queue UI must use the scene Canvas");
    }

    private static void VerifyNextPanel()
    {
        Require(BallQueueUI.Inst != null, "NEXT prefab must exist");
        Require(PuzzleTestAccess.HasVisibleNextAnimal(), "NEXT must show an animal sprite");
        UIManager legacyUI = UnityEngine.Object.FindObjectOfType<UIManager>();
        Transform legacyPanel = legacyUI != null ? legacyUI.transform.Find("NextUI") : null;
        if (legacyPanel != null) Require(!legacyPanel.gameObject.activeSelf, "New NEXT must hide the legacy panel");
        BallQueueUI.Inst.ToggleInventory();
        Require(SpawnManager.Inst.RemainingBallCount == 1, "NEXT must not consume supply");
        BallQueueUI.Inst.ToggleInventory();
    }

    private static void VerifyAimCancellation()
    {
        AimAtBasket(1);
        Require(SpawnManager.Inst.HasPreview, "Aiming must create a ball");
        Require(SpawnManager.Inst.RemainingBallCount == 1, "Aiming must not consume supply");
        SpawnManager.Inst.CancelAim();
        Require(!SpawnManager.Inst.HasPreview, "Cancel must remove the preview");
        Require(SpawnManager.Inst.RemainingBallCount == 1, "Cancel must retain the ball");
    }

    private static void VerifyQueueLayout()
    {
        List<int> supply = new List<int>(SpawnManager.Inst.nextBallQueue);
        List<int> retry = SpawnManager.Inst.GetRetryLevels();
        SpawnManager.Inst.RestoreQueues(new List<int> { 0, 1, 2, 0, 1, 2, 0 }, null);
        BallQueueUI.Inst.ToggleInventory();
        Canvas.ForceUpdateCanvases();
        GridLayoutGroup grid = BallQueueUI.Inst.GetComponentInChildren<GridLayoutGroup>();
        Require(grid != null, "Queue must have an Inspector grid");
        // Destroy는 프레임 끝에 처리되므로 비활성화된 이전 항목은 제외한다.
        var visibleEntries = new List<RectTransform>();
        foreach (Transform child in grid.transform)
            if (child.gameObject.activeSelf) visibleEntries.Add((RectTransform)child);
        Require(visibleEntries.Count == 7, "Inspector grid must display seven supplied balls");
        RectTransform first = visibleEntries[0];
        RectTransform seventh = visibleEntries[6];
        Require(seventh.anchoredPosition.y < first.anchoredPosition.y, "Seventh ball must wrap to the next row");
        RectTransform panel = (RectTransform)grid.transform.parent;
        Require(panel.rect.height > 200f, "Inventory must expand through layout components");
        BallQueueUI.Inst.ToggleInventory();
        SpawnManager.Inst.RestoreQueues(supply, retry);
    }

    private void WaitForFirstClear(StageManager stage)
    {
        Require(!GameManager.Inst.gameOver, "Reference solution triggered game over");
        Require(!stage.IsAttemptFailed, "Reference solution failed");
        if (!stage.IsCleared) return;

        SavedCheckpoint checkpoint = JsonUtility.FromJson<SavedCheckpoint>(File.ReadAllText(SaveManager.Inst.SavePath));
        Require(checkpoint != null && checkpoint.stage == 2, "Clear must save progress to stage 2");
        stage.NextStage();
        AdvanceTo(Step.VerifySecondStage);
    }

    private void VerifySecondStage(StageManager stage)
    {
        Require(stage.Stage == 2, "NEXT must enter stage 2");
        Require(SpawnManager.Inst.RemainingBallCount == 4, "Stage 2 must supply four balls");
        AimAtBasket(0);
        PuzzleTestAccess.DropAimedBall();
        AdvanceTo(Step.WaitForLanding);
    }

    private void WaitForLanding()
    {
        if (!SpawnManager.Inst.canSpawn) return;
        Require(SpawnManager.Inst.RemainingBallCount == 3, "Stage 2 must consume one ball");
        SaveManager.Inst.SaveGame();
        Require(SaveManager.Inst.LoadGame(), "SaveManager must accept the current save");
        AdvanceTo(Step.VerifyRestoredState);
    }

    private void VerifyRestoredState(StageManager stage)
    {
        Require(stage.Stage == 2, "Restored stage must remain 2");
        Require(SpawnManager.Inst.RemainingBallCount == 3, "Restore must retain the remaining queue");
        Require(UnityEngine.Object.FindObjectsOfType<BallBehaviour>().Length == 3,
            "Restore must retain the two initial balls and one dropped ball");
        AdvanceTo(Step.Complete);
    }

    private static void AimAtBasket(int basketIndex)
    {
        Basket basket = ScaleSystem.Inst.Baskets[basketIndex];
        UnityEngine.Object.FindObjectOfType<CameraFocusController>().Focus(basket);
        Vector2 position = (basket.SpawnLeftPoint.position + basket.SpawnRightPoint.position) * 0.5f;
        PuzzleTestAccess.StartAiming(position);
    }

    private void AdvanceTo(Step nextStep)
    {
        currentStep = nextStep;
        deadline = EditorApplication.timeSinceStartup + StepTimeoutSeconds;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    [Serializable]
    private sealed class SavedCheckpoint
    {
        public int stage;
    }
}
