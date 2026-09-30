using UnityEngine;

// 바닥(DeadZone)을 저울이 어떤 자세가 되어도 닿지 않는 높이에 자동으로 둔다.
// 저울의 어느 부분이든 최상위 고리에서 "줄과 막대를 따라간 거리의 합"보다 멀어질 수 없으므로,
// 그 최대 거리(도달 반경)만큼 고리 아래에 여유를 더해 배치한다. 폭도 도달 반경을 덮도록 맞춘다.
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class DeadZonePlacer : MonoBehaviour
{
    [Tooltip("비워 두면 씬의 ScaleSystem에서 최상위 저울대를 찾는다.")]
    [SerializeField] private HangingNode root;

    [Tooltip("도달 반경보다 더 내려둘 거리. 공 크기 정도는 두는 것이 안전하다.")]
    [SerializeField, Min(0f)] private float margin = 5f;

    [Tooltip("도달 반경 바깥으로 좌우에 더 넓힐 폭")]
    [SerializeField, Min(0f)] private float extraWidth = 20f;

    private BoxCollider2D box;

    public float Reach { get; private set; }

    private void Awake()
    {
        box = GetComponent<BoxCollider2D>();
    }

    // 게임 중에는 저울 모양이 바뀌지 않으므로 시작할 때 한 번만 배치한다.
    private void Start()
    {
        if (Application.isPlaying) Place();
    }

    private void Update()
    {
        if (!Application.isPlaying) Place();
    }

    private void Place()
    {
        if (box == null) box = GetComponent<BoxCollider2D>();

        HangingNode top = root;
        if (top == null)
        {
            ScaleSystem system = FindObjectOfType<ScaleSystem>();
            if (system != null) top = system.Root;
        }
        if (top == null) return;

        Reach = ComputeReach(top, 0);
        Vector2 hook = top.transform.position;

        Vector3 pos = new Vector3(hook.x, hook.y - Reach - margin - HalfHeight(), transform.position.z);
        if ((transform.position - pos).sqrMagnitude > 0.000001f)
        {
            transform.position = pos;
            LayoutUtil.MarkDirty(transform);
        }

        float scaleX = Mathf.Abs(transform.lossyScale.x) > 0.0001f ? Mathf.Abs(transform.lossyScale.x) : 1f;
        float sizeX = (Reach + extraWidth) * 2f / scaleX;
        if (Mathf.Abs(box.size.x - sizeX) > 0.001f || box.offset != Vector2.zero)
        {
            box.size = new Vector2(sizeX, box.size.y);
            box.offset = Vector2.zero;
            LayoutUtil.MarkDirty(box);
        }
    }

    private float HalfHeight()
    {
        return box.size.y * Mathf.Abs(transform.lossyScale.y) * 0.5f;
    }

    // 노드 고리에서 노드와 그 아래 매달린 모든 것이 닿을 수 있는 최대 거리.
    // 자기 모양의 가장 먼 모서리까지의 거리와, 각 매다는 지점까지의 거리 + 하위 노드의 도달 반경 중 큰 값이다.
    private static float ComputeReach(HangingNode node, int depth)
    {
        if (node == null || depth > 16) return 0f;

        Transform t = node.transform;
        float reach = 0f;

        foreach (Renderer r in node.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;
            Bounds b = r.bounds;
            reach = Mathf.Max(reach, FarthestCorner(t, b));
        }

        if (node is ScaleBeam beam)
        {
            foreach (ScaleBeam.Attachment a in beam.Attachments)
            {
                if (a.point == null || a.node == null || a.node == node) continue;
                float toPoint = Vector2.Distance(t.position, a.point.position);
                reach = Mathf.Max(reach, toPoint + ComputeReach(a.node, depth + 1));
            }
        }
        return reach;
    }

    private static float FarthestCorner(Transform origin, Bounds b)
    {
        Vector2 o = origin.position;
        float max = 0f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 c = new Vector2(i % 2 == 0 ? b.min.x : b.max.x, i < 2 ? b.min.y : b.max.y);
            max = Mathf.Max(max, Vector2.Distance(o, c));
        }
        return max;
    }
}
