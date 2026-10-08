using System;
using System.Collections.Generic;
using UnityEngine;

// 공이 합쳐질 때 레벨별 색으로 강조선을 띄운다. 씬에 없으면 자동으로 만들어진다.
public class MergeEffectManager : Singleton<MergeEffectManager>
{
    [Serializable]
    public class BurstSettings
    {
        [Tooltip("톱니 개수")]
        public int spikes = 12;
        [Tooltip("공 반지름 대비 톱니 안쪽 반지름")]
        public float innerRadius = 1.15f;
        [Tooltip("공 반지름 대비 톱니 바깥쪽 반지름")]
        public float outerRadius = 1.55f;
        [Tooltip("손그림 흔들림 정도")]
        [Range(0f, 0.5f)] public float jitter = 0.12f;
        [Tooltip("공 반지름 대비 선 두께")]
        public float widthRatio = 0.08f;

        [Header("연출")]
        public float duration = 0.4f;
        [Tooltip("전체 시간 중 커지는 구간 비율")]
        [Range(0.05f, 0.9f)] public float popRatio = 0.3f;
        [Tooltip("전체 시간 중 사라지는 구간 비율")]
        [Range(0.05f, 1f)] public float fadeRatio = 0.4f;
        public float startScale = 0.6f;
        public float peakScale = 1.15f;
        [Tooltip("선을 다시 그려 떨리게 하는 간격(초)")]
        public float redrawInterval = 0.06f;
        [Tooltip("초당 최대 회전 각도")]
        public float spinSpeed = 60f;
    }

    [Tooltip("합쳐져서 생긴 공의 레벨 순서대로 사용할 색")]
    [SerializeField] private Color[] levelColors =
    {
        new Color(1.00f, 0.45f, 0.45f),
        new Color(1.00f, 0.65f, 0.30f),
        new Color(1.00f, 0.85f, 0.25f),
        new Color(0.60f, 0.90f, 0.30f),
        new Color(0.30f, 0.85f, 0.65f),
        new Color(0.30f, 0.75f, 1.00f),
        new Color(0.45f, 0.50f, 1.00f),
        new Color(0.75f, 0.45f, 1.00f),
        new Color(1.00f, 0.45f, 0.85f),
        new Color(1.00f, 1.00f, 1.00f),
    };

    [SerializeField] private BurstSettings burst = new BurstSettings();
    [Tooltip("공보다 위에 보이도록 할 정렬 순서")]
    [SerializeField] private int sortingOrder = 10;
    [SerializeField] private int prewarmCount = 8;

    private readonly List<MergeBurst> pool = new List<MergeBurst>();
    private Material lineMaterial;

    // 첫 씬이 로드된 뒤 매니저가 없으면 하나 만든다.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Inst == null && FindObjectOfType<MergeEffectManager>() == null)
            new GameObject(nameof(MergeEffectManager)).AddComponent<MergeEffectManager>();
    }

    protected override void DoAwake()
    {
        if (Inst != this) return;

        lineMaterial = new Material(Shader.Find("Sprites/Default"));
        for (int i = 0; i < prewarmCount; i++)
            CreateBurst();
    }

    private void OnEnable()
    {
        SpawnManager.OnBallMerged += HandleBallMerged;
    }

    private void OnDisable()
    {
        SpawnManager.OnBallMerged -= HandleBallMerged;
    }

    private void HandleBallMerged(GameObject ball, int level)
    {
        if (Inst != this || ball == null) return;

        GetBurst().Play(ball.transform, GetBallRadius(ball), GetColor(level), burst);
    }

    // 레벨에 맞는 색을 돌려준다. 색이 모자라면 흰색을 쓴다.
    public Color GetColor(int level)
    {
        if (levelColors == null || level < 0 || level >= levelColors.Length)
            return Color.white;
        return levelColors[level];
    }

    // 프리팹마다 스케일이 달라서 콜라이더 반지름에 실제 스케일을 곱해 월드 반지름을 구한다.
    private static float GetBallRadius(GameObject ball)
    {
        CircleCollider2D circle = ball.GetComponent<CircleCollider2D>();
        if (circle != null)
        {
            Vector3 s = ball.transform.lossyScale;
            return circle.radius * Mathf.Max(Mathf.Abs(s.x), Mathf.Abs(s.y));
        }

        SpriteRenderer sr = ball.GetComponent<SpriteRenderer>();
        return sr != null ? sr.bounds.extents.x : 0.5f;
    }

    private MergeBurst GetBurst()
    {
        foreach (MergeBurst b in pool)
        {
            if (!b.gameObject.activeSelf)
                return b;
        }
        return CreateBurst();
    }

    private MergeBurst CreateBurst()
    {
        GameObject go = new GameObject("MergeBurst");
        go.transform.SetParent(transform, false);
        go.SetActive(false);

        MergeBurst b = go.AddComponent<MergeBurst>();
        b.Init(lineMaterial, sortingOrder);
        pool.Add(b);
        return b;
    }
}
