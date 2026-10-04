using UnityEngine;

// 바구니. 안에 든 공들의 위치와 무게로 무게중심을 구해 스스로 기운다.
public class Basket : HangingNode
{
    [Header("구성 요소")]
    [SerializeField] private BasketWeightSensor sensor;

    [Tooltip("공을 떨어뜨릴 수 있는 좌우 범위. 테두리보다 약간 위에 둔다.")]
    [SerializeField] private Transform spawnLeftPoint;
    [SerializeField] private Transform spawnRightPoint;

    [Header("게임오버")]
    [SerializeField] private float gameOverAngle = 45f;

    private Renderer[] renderers;

    public BasketWeightSensor Sensor => sensor;
    public Transform SpawnLeftPoint => spawnLeftPoint;
    public Transform SpawnRightPoint => spawnRightPoint;
    public float GameOverAngle => gameOverAngle;
    public bool IsOverTilted => Mathf.Abs(CurrentAngle) >= gameOverAngle;

    public override void Initialize()
    {
        base.Initialize();
        renderers = GetComponentsInChildren<Renderer>();

        if (sensor == null)
            Debug.LogError($"[{name}] BasketWeightSensor가 연결되지 않았습니다.", this);

        if (maxAngle <= gameOverAngle)
            Debug.LogWarning($"[{name}] maxAngle({maxAngle})이 gameOverAngle({gameOverAngle}) 이하라 게임오버가 발생하지 않습니다.", this);
    }

    protected override float CalculateContentMass()
    {
        return sensor != null ? sensor.TotalWeight : 0f;
    }

    protected override void AccumulateLoads(ref Vector2 weightedSum, ref float massSum)
    {
        if (sensor == null) return;

        foreach (Rigidbody2D ball in sensor.Balls)
        {
            weightedSum += WorldToNodeOffset(ball.position) * ball.mass;
            massSum += ball.mass;
        }
    }

    // 쌓인 공 중 가장 높은 윗면의 y. 공이 없으면 float.MinValue
    public float GetHighestBallTop()
    {
        float top = float.MinValue;
        if (sensor == null) return top;

        foreach (Rigidbody2D ball in sensor.Balls)
        {
            Collider2D col = ball.GetComponent<Collider2D>();
            if (col != null && col.bounds.max.y > top) top = col.bounds.max.y;
        }
        return top;
    }

    // 카메라가 이 바구니를 비출 때 쓰는 화면 영역
    public Bounds GetVisualBounds()
    {
        Bounds b = new Bounds(transform.position, Vector3.zero);
        bool first = true;

        if (renderers != null)
        {
            foreach (Renderer r in renderers)
            {
                if (r == null || !r.enabled) continue;
                if (first) { b = r.bounds; first = false; }
                else b.Encapsulate(r.bounds);
            }
        }
        return b;
    }
}
