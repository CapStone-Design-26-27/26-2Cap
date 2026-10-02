using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

// 바구니의 모양을 에디터에서 자동으로 맞춘다. 플레이 중에는 동작하지 않는다.
// 원점(고리)에서 줄을 아래로 늘리고, 줄 끝에 컵의 윗면 가운데가 오도록 컵을 옮긴다.
// Sensor, DeadLine, SpawnLeft/Right는 컵의 자식으로 두어야 컵과 함께 움직인다.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Basket))]
public class BasketLayout : MonoBehaviour
{
    [SerializeField] private SpriteRenderer rope;

    [Tooltip("컵 오브젝트 (BasketBag)")]
    [SerializeField] private Transform cup;

    [Tooltip("줄 길이. 상위 저울대가 막대와 겹치지 않도록 필요한 만큼 자동으로 더 늘릴 수 있다.")]
    [SerializeField, Min(0f)] private float ropeLength = 4f;

    [Tooltip("줄 두께. 0이면 현재 두께 유지")]
    [SerializeField, Min(0f)] private float ropeWidth = 0f;

    [Header("무게중심")]
    [Tooltip("컵 위치에 맞춰 Basket의 Self Com Offset을 자동으로 설정")]
    [SerializeField] private bool autoCenterOfMass = true;

    [Tooltip("빈 바구니의 무게중심 높이 (0 = 컵 윗면, 1 = 컵 바닥). 클수록 덜 기운다.")]
    [SerializeField, Range(0f, 1f)] private float comDepth = 0.7f;

    // 상위 저울대의 BeamLayout이 막대와 겹치지 않게 하려고 더하는 줄 길이
    [SerializeField, HideInInspector] private float extraDrop;

    public float TotalRopeLength => ropeLength + extraDrop;
    public float ExtraDrop => extraDrop;

    public void SetExtraDrop(float value)
    {
        value = Mathf.Max(0f, value);
        if (Mathf.Abs(value - extraDrop) < 0.001f) return;
        extraDrop = value;
        LayoutUtil.MarkDirty(this);
    }

    private void Reset()
    {
        Transform r = transform.Find("Rope");
        if (r != null) rope = r.GetComponent<SpriteRenderer>();

        Transform c = transform.Find("BasketBag");
        if (c != null) cup = c;
    }

    private void Update()
    {
        if (Application.isPlaying) return;
        Apply();
    }

    private void Apply()
    {
        // 컵 영역 계산이 회전 0을 전제로 하므로 먼저 회전을 초기화
        LayoutUtil.ResetLocalRotation(transform);

        float drop = TotalRopeLength;

        if (rope != null)
            LayoutUtil.StretchVertical(rope, 0f, -drop, ropeWidth);

        if (cup == null) return;

        LayoutUtil.ResetLocalRotation(cup);

        if (!LayoutUtil.TryGetLocalBounds(transform, cup, out Bounds b)) return;

        Vector3 shift = new Vector3(-b.center.x, -drop - b.max.y, 0f);
        if (shift.sqrMagnitude > 0.000001f)
        {
            cup.position += transform.TransformVector(shift);
            LayoutUtil.MarkDirty(cup);
        }

        if (autoCenterOfMass)
            SetBasketCom(new Vector2(0f, -drop - b.size.y * comDepth));
    }

    // 고리 기준 컵 윗면의 양쪽 모서리. 상위 저울대가 막대와의 간격을 계산할 때 쓴다.
    public bool TryGetCupTopCorners(out Vector2 left, out Vector2 right)
    {
        left = right = Vector2.zero;
        if (cup == null || !LayoutUtil.TryGetLocalBounds(transform, cup, out Bounds b)) return false;

        // 컵 윗면 높이는 설정값(-줄 길이)으로 고정되므로 화면에 그려진 위치 대신 그 값을 쓴다.
        left = new Vector2(b.min.x, -TotalRopeLength);
        right = new Vector2(b.max.x, -TotalRopeLength);
        return true;
    }

    private void SetBasketCom(Vector2 value)
    {
#if UNITY_EDITOR
        SerializedObject so = new SerializedObject(GetComponent<Basket>());
        SerializedProperty prop = so.FindProperty("selfComOffset");
        if (prop != null && (prop.vector2Value - value).sqrMagnitude > 0.000001f)
        {
            prop.vector2Value = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
#endif
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);
    }
}
