using System.Collections.Generic;
using UnityEngine;

// 바구니 안쪽을 덮는 트리거 영역. 실제로 바구니 안에 들어온 공만 무게로 센다.
// 파괴된 공, 합쳐지는 중인 공, 조준 중인(Kinematic) 공은 제외한다.
[RequireComponent(typeof(Collider2D))]
public class BasketWeightSensor : MonoBehaviour
{
    private readonly HashSet<Rigidbody2D> inside = new HashSet<Rigidbody2D>();
    private readonly List<Rigidbody2D> valid = new List<Rigidbody2D>();

    public IReadOnlyList<Rigidbody2D> Balls
    {
        get
        {
            Refresh();
            return valid;
        }
    }

    public float TotalWeight
    {
        get
        {
            float sum = 0f;
            foreach (Rigidbody2D rb in Balls) sum += rb.mass;
            return sum;
        }
    }

    private void Reset()
    {
        GetComponent<Collider2D>().isTrigger = true;
    }

    private void Refresh()
    {
        inside.RemoveWhere(rb => rb == null);
        valid.Clear();

        foreach (Rigidbody2D rb in inside)
        {
            if (rb.isKinematic) continue;

            BallBehaviour bb = rb.GetComponent<BallBehaviour>();
            if (bb == null || bb.isMerged) continue;

            valid.Add(rb);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb != null && rb.GetComponent<BallBehaviour>() != null)
            inside.Add(rb);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        Rigidbody2D rb = other.attachedRigidbody;
        if (rb != null) inside.Remove(rb);
    }
}
