using UnityEngine;

// 공의 합성 잠금과 프리팹에 배치된 자물쇠 표시만 관리한다.
public class BallMergeLock : MonoBehaviour
{
    [SerializeField] private bool isLocked;
    [SerializeField] private GameObject lockIcon;

    public bool IsLocked => isLocked;

    // 튜토리얼 코드: 켜져 있는 동안 모든 공의 합성을 막는다. 공별 잠금 상태는 바꾸지 않는다.
    public static bool GlobalLocked { get; private set; }

    public static void SetGlobalLocked(bool locked)
    {
        if (GlobalLocked == locked) return;
        GlobalLocked = locked;

        foreach (BallMergeLock ball in FindObjectsOfType<BallMergeLock>())
            ball.RefreshIcon();

        // 풀리면 잠긴 동안 맞닿아 있던 같은 레벨 공을 합친다.
        if (!locked)
            foreach (BallBehaviour ball in FindObjectsOfType<BallBehaviour>())
                ball.TryMergeTouching();
    }

    private void Awake() => RefreshIcon();
    private void OnValidate() => RefreshIcon();

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        RefreshIcon();
    }

    private void RefreshIcon()
    {
        if (lockIcon == null)
        {
            Transform child = transform.Find("MergeLockIcon");
            if (child != null) lockIcon = child.gameObject;
        }
        if (lockIcon != null) lockIcon.SetActive(isLocked || GlobalLocked);
    }

    public static bool CanMerge(GameObject ball)
    {
        BallMergeLock mergeLock = ball.GetComponent<BallMergeLock>();
        return mergeLock == null || !mergeLock.IsLocked;
    }

    public static void SetMergeAllowed(GameObject ball, bool allowed)
    {
        BallMergeLock mergeLock = ball.GetComponent<BallMergeLock>();
        if (mergeLock == null && !allowed) mergeLock = ball.AddComponent<BallMergeLock>();
        if (mergeLock != null) mergeLock.SetLocked(!allowed);
    }
}
