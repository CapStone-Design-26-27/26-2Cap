using UnityEngine;

// 공의 합성 잠금과 프리팹에 배치된 자물쇠 표시만 관리한다.
public class BallMergeLock : MonoBehaviour
{
    [SerializeField] private bool isLocked;
    [SerializeField] private GameObject lockIcon;

    public bool IsLocked => isLocked;

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
        if (lockIcon != null) lockIcon.SetActive(isLocked);
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
