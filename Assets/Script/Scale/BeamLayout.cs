using System.Collections.Generic;
using UnityEngine;

// 저울대의 모양을 에디터에서 자동으로 맞춘다. 플레이 중에는 동작하지 않는다.
//  - 걸이(Hanger): 원점(고리)에서 막대 윗면까지 잇는 줄
//  - 막대(rod): 걸이 아래에 가로로 놓이며, 매달린 것들이 서로 겹치지 않는 길이로 자동 조절
//  - 매다는 지점(Att_*): 막대 위의 비율 위치(t)에 배치하고, 연결된 노드를 그 위치로 옮겨 미리 보여줌
//  - 간격 확보: 막대가 기울어도 매달린 바구니 벽을 지나가지 않도록 하위 노드의 줄/걸이를 늘림
[ExecuteAlways]
[DisallowMultipleComponent]
public class BeamLayout : MonoBehaviour
{
    [System.Serializable]
    public class Point
    {
        public Transform point;

        [Tooltip("막대 위 위치. 0 = 왼쪽 끝, 0.5 = 가운데, 1 = 오른쪽 끝")]
        [Range(0f, 1f)] public float t;
    }

    [Header("걸이")]
    [SerializeField] private SpriteRenderer hanger;

    [Tooltip("고리에서 막대 윗면까지의 길이. 길수록 저울대가 덜 민감하게 기운다.")]
    [SerializeField, Min(0f)] private float hangerLength = 3f;

    [Tooltip("걸이 두께. 0이면 현재 두께 유지")]
    [SerializeField, Min(0f)] private float hangerWidth = 0.4f;

    [Header("막대")]
    [SerializeField] private SpriteRenderer rod;

    [Tooltip("막대 전체 길이. 자동 길이가 켜져 있으면 계산된 값으로 바뀐다.")]
    [SerializeField, Min(0.1f)] private float length = 20f;
    [SerializeField, Min(0.01f)] private float thickness = 0.5f;

    [Header("자동 길이")]
    [Tooltip("매달린 것들의 실제 폭을 재서 서로 겹치지 않는 최소 길이로 맞춘다.")]
    [SerializeField] private bool autoLength = true;
    [Tooltip("이웃한 바구니 또는 저울대 사이의 최소 간격")]
    [SerializeField, Min(0f)] private float gap = 4f;
    [SerializeField, Min(0.1f)] private float minLength = 10f;

    [Header("막대와 바구니 간격")]
    [Tooltip("막대와 매달린 바구니가 이 각도만큼 서로 기울어도 막대가 바구니 벽을 지나가지 않도록\n하위 바구니의 줄과 하위 저울대의 걸이를 자동으로 늘린다.")]
    [SerializeField] private bool autoClearance = true;
    [SerializeField, Range(0f, 70f)] private float clearanceAngle = 35f;
    [SerializeField, Min(0f)] private float clearanceMargin = 0.5f;

    [Header("매다는 지점")]
    [Tooltip("막대 아랫면에서 매다는 지점까지의 추가 거리")]
    [SerializeField] private float pointDrop = 0f;

    [Tooltip("비워 두면 Att_Left(0), Att_LeftThird(1/3), Att_Center(0.5), Att_RightThird(2/3), Att_Right(1) 이름으로 위치를 정한다.")]
    [SerializeField] private List<Point> points = new List<Point>();

    [Tooltip("연결된 바구니와 저울대를 에디터에서도 매다는 지점으로 옮겨 실제 배치를 보여준다.")]
    [SerializeField] private bool snapChildrenInEditor = true;

    // 상위 저울대가 막대끼리 겹치지 않게 하려고 더하는 걸이 길이
    [SerializeField, HideInInspector] private float extraDrop;

    public float Length => length;
    public float TotalHangerLength => hangerLength + extraDrop;
    public float ExtraDrop => extraDrop;

    private float RodTopY => -TotalHangerLength;
    private float RodBottomY => RodTopY - thickness;

    public void SetExtraDrop(float value)
    {
        value = Mathf.Max(0f, value);
        if (Mathf.Abs(value - extraDrop) < 0.001f) return;
        extraDrop = value;
        LayoutUtil.MarkDirty(this);
    }

    private void Reset()
    {
        rod = FindSprite("rod");
        hanger = FindSprite("Hanger");

        points.Clear();
        foreach (Transform child in transform)
        {
            if (TryInferT(child.name, out float t))
                points.Add(new Point { point = child, t = t });
        }
    }

    private SpriteRenderer FindSprite(string childName)
    {
        Transform c = transform.Find(childName);
        return c != null ? c.GetComponent<SpriteRenderer>() : null;
    }

    public static bool TryInferT(string objName, out float t)
    {
        string n = objName.ToLowerInvariant();
        t = 0f;
        if (!n.StartsWith("att")) return false;

        if (n.Contains("leftthird")) { t = 1f / 3f; return true; }
        if (n.Contains("rightthird")) { t = 2f / 3f; return true; }
        if (n.Contains("center") || n.Contains("mid")) { t = 0.5f; return true; }
        if (n.Contains("left")) { t = 0f; return true; }
        if (n.Contains("right")) { t = 1f; return true; }
        return false;
    }

    // 인스펙터 목록에 있는 지점 + 목록에 없지만 이름이 Att_로 시작하는 자식
    private List<Point> GetEffectivePoints()
    {
        List<Point> result = new List<Point>();
        HashSet<Transform> listed = new HashSet<Transform>();

        foreach (Point p in points)
        {
            if (p.point == null) continue;
            result.Add(p);
            listed.Add(p.point);
        }

        foreach (Transform child in transform)
        {
            if (listed.Contains(child)) continue;
            if (TryInferT(child.name, out float t))
                result.Add(new Point { point = child, t = t });
        }
        return result;
    }

    private void Update()
    {
        if (Application.isPlaying) return;
        Apply();
    }

    private void Apply()
    {
        if (transform.lossyScale != Vector3.one)
            Debug.LogWarning($"[{name}] Scale이 (1,1,1)이 아니면 길이가 틀어집니다. 저울대와 그 부모의 Scale을 1로 맞추세요.", this);

        // 에디터에서 회전해 둔 각도로 게임이 시작되지 않도록 0으로 고정
        LayoutUtil.ResetLocalRotation(transform);

        ScaleBeam beam = GetComponent<ScaleBeam>();
        List<Point> effective = GetEffectivePoints();

        if (autoLength && beam != null)
        {
            float newLength = Mathf.Max(minLength, ComputeRequiredLength(beam, effective));
            if (Mathf.Abs(newLength - length) > 0.001f)
            {
                length = newLength;
                LayoutUtil.MarkDirty(this);
            }
        }

        if (hanger != null)
        {
            bool show = TotalHangerLength > 0.01f;
            if (hanger.enabled != show) { hanger.enabled = show; LayoutUtil.MarkDirty(hanger); }
            if (show) LayoutUtil.StretchVertical(hanger, 0f, RodTopY, hangerWidth);
        }

        if (rod != null) ApplyRod();

        float y = RodBottomY - pointDrop;
        foreach (Point p in effective)
        {
            float x = Mathf.Lerp(-length * 0.5f, length * 0.5f, p.t);
            LayoutUtil.SetLocalPosition(p.point, new Vector3(x, y, 0f));
        }

        if (beam == null) return;
        if (autoClearance) ApplyClearance(beam);
        if (snapChildrenInEditor) SnapChildren(beam);
    }

    // ─── 자동 길이 ───
    // 이웃한 두 노드 i, j (t_i < t_j)가 겹치지 않으려면
    //   (t_j - t_i) × 길이 ≥ i의 오른쪽 폭 + j의 왼쪽 폭 + gap
    // 을 만족해야 하므로, 모든 이웃 쌍 중 가장 큰 값을 막대 길이로 쓴다.
    private float ComputeRequiredLength(ScaleBeam beam, List<Point> effective)
    {
        List<(float t, float left, float right)> list = new List<(float, float, float)>();

        foreach (Point p in effective)
        {
            HangingNode node = FindNode(beam, p.point);
            if (node == null || node == beam) continue;
            if (!TryGetExtents(node, out float l, out float r, 0)) continue;

            list.Add((p.t, l, r));
        }

        list.Sort((a, b) => a.t.CompareTo(b.t));

        float required = 0f;
        for (int i = 1; i < list.Count; i++)
        {
            float dt = list[i].t - list[i - 1].t;
            if (dt < 0.0001f) continue;

            float need = (list[i - 1].right - list[i].left + gap) / dt;
            required = Mathf.Max(required, need);
        }
        return required;
    }

    private static HangingNode FindNode(ScaleBeam beam, Transform point)
    {
        foreach (ScaleBeam.Attachment a in beam.Attachments)
            if (a.point == point) return a.node;
        return null;
    }

    // 노드의 가로 범위 (고리 기준 왼쪽 끝 x, 오른쪽 끝 x). 저울대면 매달린 것들까지 포함한다.
    public static bool TryGetExtents(HangingNode node, out float left, out float right, int depth)
    {
        left = right = 0f;
        if (node == null || depth > 16) return false;

        bool has = LayoutUtil.TryGetLocalBounds(node.transform, node.transform, out Bounds own);
        if (has)
        {
            left = own.min.x;
            right = own.max.x;
        }

        if (node is ScaleBeam sb)
        {
            foreach (ScaleBeam.Attachment a in sb.Attachments)
            {
                if (a.point == null || a.node == null || a.node == node) continue;
                if (!TryGetExtents(a.node, out float cl, out float cr, depth + 1)) continue;

                float px = node.transform.InverseTransformPoint(a.point.position).x;
                if (!has)
                {
                    left = px + cl;
                    right = px + cr;
                    has = true;
                }
                else
                {
                    left = Mathf.Min(left, px + cl);
                    right = Mathf.Max(right, px + cr);
                }
            }
        }
        return has;
    }

    // ─── 막대와 바구니 간격 ───
    // 막대가 매다는 지점을 축으로 clearanceAngle만큼 기울면, 지점에서 가로로 x만큼 떨어진 곳의 막대는
    // 대략 x × tan(clearanceAngle)만큼 내려온다. 하위 노드의 윗부분(컵 모서리, 하위 막대 끝)이
    // 모두 그보다 아래에 있도록, 부족한 만큼 하위 노드의 줄 또는 걸이를 늘린다.
    private void ApplyClearance(ScaleBeam beam)
    {
        float tan = Mathf.Tan(clearanceAngle * Mathf.Deg2Rad);
        float rodLeft = -length * 0.5f;
        float rodRight = length * 0.5f;
        List<Vector2> profile = new List<Vector2>();

        foreach (ScaleBeam.Attachment a in beam.Attachments)
        {
            if (a.point == null || a.node == null || a.node == beam) continue;

            BasketLayout basketLayout = a.node.GetComponent<BasketLayout>();
            BeamLayout beamLayout = a.node.GetComponent<BeamLayout>();
            if (basketLayout == null && beamLayout == null) continue;

            profile.Clear();
            CollectProfile(a.node, Vector2.zero, profile, 0);
            if (profile.Count == 0) continue;

            // 막대가 걸린 지점 기준으로 막대가 뻗어 있는 가로 범위
            float hookX = transform.InverseTransformPoint(a.point.position).x;
            float minX = rodLeft - hookX;
            float maxX = rodRight - hookX;

            // 매다는 지점은 막대 아랫면에서 pointDrop만큼 아래이므로, 막대 아랫면은 지점보다 pointDrop 위에 있다.
            // 컵 윗면과 하위 막대는 수평이므로, 막대 아래에 놓인 구간 중 지점에서 가장 먼 x에서 가장 가까워진다.
            float deficit = float.MinValue;
            foreach (Vector2 q in profile)
            {
                float x = Mathf.Clamp(q.x, minX, maxX);
                float depth = -q.y;
                float need = Mathf.Abs(x) * tan - pointDrop + clearanceMargin;
                deficit = Mathf.Max(deficit, need - depth);
            }
            if (deficit == float.MinValue) continue;

            if (basketLayout != null) basketLayout.SetExtraDrop(basketLayout.ExtraDrop + deficit);
            else beamLayout.SetExtraDrop(beamLayout.ExtraDrop + deficit);
        }
    }

    // 노드 윗부분의 모서리 점들 (고리 기준). 막대가 지나갈 때 먼저 부딪히는 곳이다.
    // 높이는 화면에 그려진 위치가 아니라 설정값으로 계산해, 하위 노드가 아직 갱신되기 전이어도 결과가 같게 한다.
    private static void CollectProfile(HangingNode node, Vector2 offset, List<Vector2> pts, int depth)
    {
        if (node == null || depth > 16) return;

        BasketLayout basketLayout = node.GetComponent<BasketLayout>();
        if (basketLayout != null)
        {
            if (basketLayout.TryGetCupTopCorners(out Vector2 l, out Vector2 r))
            {
                pts.Add(offset + l);
                pts.Add(offset + r);
            }
            return;
        }

        BeamLayout beamLayout = node.GetComponent<BeamLayout>();
        if (beamLayout == null) return;

        float half = beamLayout.length * 0.5f;
        pts.Add(offset + new Vector2(-half, beamLayout.RodTopY));
        pts.Add(offset + new Vector2(half, beamLayout.RodTopY));

        if (node is ScaleBeam sb)
        {
            foreach (ScaleBeam.Attachment a in sb.Attachments)
            {
                if (a.point == null || a.node == null || a.node == node) continue;
                float childX = node.transform.InverseTransformPoint(a.point.position).x;
                Vector2 childHook = new Vector2(childX, beamLayout.RodBottomY - beamLayout.pointDrop);
                CollectProfile(a.node, offset + childHook, pts, depth + 1);
            }
        }
    }

    // ─── 에디터 미리보기 ───

    private void SnapChildren(ScaleBeam beam)
    {
        foreach (ScaleBeam.Attachment a in beam.Attachments)
        {
            if (a.point == null || a.node == null || a.node == beam) continue;

            Transform nt = a.node.transform;
            if (nt.IsChildOf(transform))
            {
                Debug.LogWarning($"[{name}] '{a.node.name}'이(가) 이 저울대의 자식입니다. 형제로 옮겨 주세요.", a.node);
                continue;
            }

            Vector3 target = new Vector3(a.point.position.x, a.point.position.y, nt.position.z);
            if ((nt.position - target).sqrMagnitude > 0.00000001f)
            {
                nt.position = target;
                LayoutUtil.MarkDirty(nt);
            }
            LayoutUtil.ResetLocalRotation(nt);
        }
    }

    // ─── 막대 ───

    private void ApplyRod()
    {
        Transform rt = rod.transform;
        Vector2 size = new Vector2(length, thickness);

        LayoutUtil.ResetLocalRotation(rt);

        if (rod.drawMode == SpriteDrawMode.Simple)
        {
            if (rod.sprite == null) return;

            Vector2 spriteSize = rod.sprite.bounds.size;
            Vector3 scale = new Vector3(size.x / spriteSize.x, size.y / spriteSize.y, 1f);
            if (rt.localScale != scale) { rt.localScale = scale; LayoutUtil.MarkDirty(rt); }
        }
        else
        {
            if (rod.size != size) { rod.size = size; LayoutUtil.MarkDirty(rod); }
            if (rt.localScale != Vector3.one) { rt.localScale = Vector3.one; LayoutUtil.MarkDirty(rt); }

            BoxCollider2D col = rod.GetComponent<BoxCollider2D>();
            if (col != null && (col.size != size || col.offset != Vector2.zero))
            {
                col.size = size;
                col.offset = Vector2.zero;
                LayoutUtil.MarkDirty(col);
            }
        }

        // 피벗이 가운데가 아니어도 막대 중심이 고리 바로 아래에 오도록 보정
        Vector2 pivot01 = Vector2.one * 0.5f;
        if (rod.sprite != null)
            pivot01 = rod.sprite.pivot / rod.sprite.rect.size;

        Vector2 pivotShift = (pivot01 - Vector2.one * 0.5f) * size;
        float centerY = RodTopY - thickness * 0.5f;
        LayoutUtil.SetLocalPosition(rt, new Vector3(pivotShift.x, centerY + pivotShift.y, rt.localPosition.z));
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, 0.3f);

        Gizmos.color = Color.red;
        foreach (Point p in GetEffectivePoints())
            Gizmos.DrawWireSphere(p.point.position, 0.25f);
    }
}
