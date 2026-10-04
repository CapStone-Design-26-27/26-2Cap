using UnityEngine;

// 고리에 매달려 무게중심에 따라 기우는 물체(저울대, 바구니)의 공통 부모.
// 매달린 물체는 전체 무게중심이 고리 바로 아래에 올 때까지 기운다는 원리로 목표 각도를 구한다.
// 오브젝트 원점이 고리 위치이며, 이동과 회전은 ScaleSystem이 위에서 아래 순서로 호출한다.
[RequireComponent(typeof(Rigidbody2D))]
public abstract class HangingNode : MonoBehaviour
{
    [Header("자체 무게")]
    [SerializeField] protected float selfMass = 5f;

    [Tooltip("고리 기준 자체 무게중심 위치 (기울기 0일 때). y는 음수여야 하며, 아래로 멀수록 덜 기운다.")]
    [SerializeField] protected Vector2 selfComOffset = new Vector2(0f, -2f);

    [Header("회전")]
    [SerializeField] protected float maxAngle = 80f;
    [Tooltip("목표 각도까지 따라가는 시간. 클수록 천천히 기운다.")]
    [SerializeField] protected float smoothTime = 0.4f;

    protected Rigidbody2D rb;
    private float angularVelocity;
    private float snapAngle;

    // 자기 무게 + 매달린 모든 것의 무게. RecalculateMass 호출 시 갱신된다.
    public float TotalMass { get; private set; }

    // -180 ~ 180, 반시계 방향이 +
    public float CurrentAngle => rb != null ? Mathf.DeltaAngle(0f, rb.rotation) : 0f;

    public float MaxAngle => maxAngle;

    public virtual void Initialize()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Kinematic;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        snapAngle = Mathf.DeltaAngle(0f, transform.eulerAngles.z);

        if (selfComOffset.y >= 0f)
            Debug.LogWarning($"[{name}] selfComOffset.y가 0 이상이면 불안정해집니다. 음수로 설정하세요.", this);
    }

    public float RecalculateMass()
    {
        TotalMass = selfMass + CalculateContentMass();
        return TotalMass;
    }

    protected abstract float CalculateContentMass();

    // 내용물 하중을 "기울기 0 기준 고리로부터의 위치 × 질량" 형태로 더한다.
    protected abstract void AccumulateLoads(ref Vector2 weightedSum, ref float massSum);

    private float ComputeTargetAngle()
    {
        Vector2 weighted = selfComOffset * selfMass;
        float mass = selfMass;
        AccumulateLoads(ref weighted, ref mass);

        if (mass <= Mathf.Epsilon) return 0f;

        Vector2 com = weighted / mass;
        if (com.sqrMagnitude < 0.0001f) return CurrentAngle;

        float target = Vector2.SignedAngle(com, Vector2.down);
        return Mathf.Clamp(target, -maxAngle, maxAngle);
    }

    public void Step(Vector2 hookPos, float dt)
    {
        float target = ComputeTargetAngle();
        float next = Mathf.SmoothDampAngle(CurrentAngle, target, ref angularVelocity, smoothTime, Mathf.Infinity, dt);

        rb.MovePosition(hookPos);
        rb.MoveRotation(next);

        OnStep(hookPos, next, dt);
    }

    protected virtual void OnStep(Vector2 hookPos, float angle, float dt) { }

    // 시작 또는 불러오기 시 보간 없이 즉시 배치
    public void SetSnapAngle(float angle) => snapAngle = angle;

    public void SnapTree(Vector2 hookPos)
    {
        rb.position = hookPos;
        rb.rotation = snapAngle;
        transform.SetPositionAndRotation(
            new Vector3(hookPos.x, hookPos.y, transform.position.z),
            Quaternion.Euler(0f, 0f, snapAngle));
        angularVelocity = 0f;

        OnSnap(hookPos, snapAngle);
    }

    protected virtual void OnSnap(Vector2 hookPos, float angle) { }

    public static Vector2 Rotate(Vector2 v, float degrees)
    {
        float r = degrees * Mathf.Deg2Rad;
        float c = Mathf.Cos(r);
        float s = Mathf.Sin(r);
        return new Vector2(c * v.x - s * v.y, s * v.x + c * v.y);
    }

    // 월드 좌표를 "기울기 0 기준 고리로부터의 위치"로 변환
    protected Vector2 WorldToNodeOffset(Vector2 worldPoint)
    {
        return Rotate(worldPoint - rb.position, -rb.rotation);
    }
}
