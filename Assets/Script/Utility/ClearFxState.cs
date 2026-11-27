// 본게임과 튜토리얼 어느 쪽이든 현재 클리어 판정 상태를 알려 준다.
public static class ClearFxState
{
    // 클리어 판정 중인지
    public static bool IsChecking =>
        (StageManager.Inst != null && StageManager.Inst.IsCheckingClear) ||
        (TutorialManager.Inst != null && TutorialManager.Inst.IsCheckingClear);

    // 클리어되었는지
    public static bool IsCleared =>
        (StageManager.Inst != null && StageManager.Inst.IsCleared) ||
        (TutorialManager.Inst != null && TutorialManager.Inst.IsCleared);

    // 이번 시도가 실패했는지
    public static bool IsFailed =>
        (GameManager.Inst != null && GameManager.Inst.gameOver) ||
        (StageManager.Inst != null && StageManager.Inst.IsAttemptFailed) ||
        (TutorialManager.Inst != null && TutorialManager.Inst.IsFailed);

    // 클리어 판정 중이거나 클리어 상태인지
    public static bool IsClearing => !IsFailed && (IsChecking || IsCleared);

    // 저울대가 수평으로 인정되는 최대 기울기 (도)
    public static float LevelTolerance
    {
        get
        {
            if (TutorialManager.Inst != null) return TutorialManager.Inst.BeamTolerance;
            if (StageManager.Inst != null) return StageManager.Inst.LevelTolerance;
            return 3f;
        }
    }
}
