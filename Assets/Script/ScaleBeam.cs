using System.Collections.Generic;
using UnityEngine;

// 저울대. 여러 지점에 바구니나 다른 저울대를 매달 수 있다.
// 매달린 노드는 줄에 걸려 있으므로 내부 배치와 상관없이 총무게만 전달한다.
public class ScaleBeam : HangingNode
{
    [System.Serializable]
    public class Attachment
    {
        [Tooltip("이 저울대의 자식 오브젝트. 하위 노드의 고리가 이 위치에 걸린다.")]
        public Transform point;

        [Tooltip("매달 저울대 또는 바구니. 씬에서 이 저울대의 자식이 아닌 형제로 둔다.")]
        public HangingNode node;

        [HideInInspector] public Vector2 offset;
    }

    [SerializeField] private List<Attachment> attachments = new List<Attachment>();

    [Header("영점 조절")]
    [Tooltip("시작 시 빈 상태의 좌우 쏠림을 보이지 않는 균형추로 상쇄해, 빈 저울이 수평에서 시작하게 한다.")]
    [SerializeField] private bool autoBalance = true;

    private bool tared;
    private float tareMoment;

    public IReadOnlyList<Attachment> Attachments => attachments;

    public override void Initialize()
    {
        base.Initialize();

        Vector2 origin = transform.position;
        float angle = transform.eulerAngles.z;

        foreach (Attachment a in attachments)
        {
            if (a.point == null)
            {
                Debug.LogWarning($"[{name}] point가 비어 있는 Attachment가 있습니다.", this);
                continue;
            }
            a.offset = Rotate((Vector2)a.point.position - origin, -angle);
        }
    }

    protected override float CalculateContentMass()
    {
        float sum = 0f;
        foreach (Attachment a in attachments)
        {
            if (a.node != null) sum += a.node.RecalculateMass();
        }
        return sum;
    }

    protected override void AccumulateLoads(ref Vector2 weightedSum, ref float massSum)
    {
        float moment = selfComOffset.x * selfMass;

        foreach (Attachment a in attachments)
        {
            if (a.node == null) continue;
            weightedSum += a.offset * a.node.TotalMass;
            massSum += a.node.TotalMass;
            moment += a.offset.x * a.node.TotalMass;
        }

        // 첫 계산 시점(바구니가 비어 있을 때)의 쏠림을 기준값으로 저장해 이후 계속 상쇄한다.
        if (!tared)
        {
            tareMoment = autoBalance ? -moment : 0f;
            tared = true;
        }
        weightedSum.x += tareMoment;
    }

    protected override void OnStep(Vector2 hookPos, float angle, float dt)
    {
        foreach (Attachment a in attachments)
        {
            if (a.node == null) continue;
            a.node.Step(hookPos + Rotate(a.offset, angle), dt);
        }
    }

    protected override void OnSnap(Vector2 hookPos, float angle)
    {
        foreach (Attachment a in attachments)
        {
            if (a.node == null) continue;
            a.node.SnapTree(hookPos + Rotate(a.offset, angle));
        }
    }
}
