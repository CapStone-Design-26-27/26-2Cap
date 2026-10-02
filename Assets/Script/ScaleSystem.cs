using System.Collections.Generic;
using UnityEngine;

// 저울 전체의 갱신 순서를 관리한다.
// 매 물리 프레임마다 무게를 아래에서 위로 합산하고, 기울기와 위치를 위에서 아래로 적용한 뒤,
// 45도 이상 기운 바구니가 있으면 게임오버 처리한다.
public class ScaleSystem : SingletonDestroy<ScaleSystem>
{
    [Tooltip("가장 위의 저울대. 이 오브젝트의 원점이 천장 고리 위치로 고정된다.")]
    [SerializeField] private HangingNode root;

    private Vector2 rootHook;
    private readonly List<HangingNode> allNodes = new List<HangingNode>();
    private readonly List<Basket> baskets = new List<Basket>();

    public HangingNode Root => root;
    public IReadOnlyList<HangingNode> AllNodes => allNodes;
    public IReadOnlyList<Basket> Baskets => baskets;

    protected override void DoAwake()
    {
        if (root == null)
        {
            Debug.LogError("[ScaleSystem] root가 비어 있습니다.", this);
            return;
        }

        rootHook = root.transform.position;

        Collect(root, new HashSet<HangingNode>());
        foreach (HangingNode node in allNodes) node.Initialize();

        ValidateScene();

        root.SnapTree(rootHook);
        Physics2D.SyncTransforms();
    }

    // 자주 생기는 씬 구성 실수를 경고하고, 그 상태로도 오브젝트가 떨어지지 않게 막는다.
    private void ValidateScene()
    {
        // 어느 저울대에도 연결되지 않은 노드는 중력을 받아 떨어진다.
        foreach (HangingNode node in FindObjectsOfType<HangingNode>())
        {
            if (allNodes.Contains(node)) continue;

            Debug.LogWarning(
                $"[ScaleSystem] '{node.name}'이(가) 저울에 연결되지 않았습니다. " +
                "상위 ScaleBeam의 Attachments에 연결하거나 씬에서 지우세요.", node);

            Rigidbody2D rb = node.GetComponent<Rigidbody2D>();
            if (rb != null) rb.bodyType = RigidbodyType2D.Kinematic;
        }

        // 노드 안쪽 자식에 Rigidbody2D가 있으면 그 부분만 따로 떨어진다.
        foreach (HangingNode node in allNodes)
        {
            foreach (Rigidbody2D childRb in node.GetComponentsInChildren<Rigidbody2D>(true))
            {
                if (childRb.gameObject == node.gameObject) continue;

                Debug.LogWarning(
                    $"[ScaleSystem] '{node.name}' 안의 '{childRb.name}'에 Rigidbody2D가 있어 제거합니다. " +
                    "Rigidbody2D는 저울대와 바구니의 최상위에만 둡니다.", childRb);

                Destroy(childRb);
            }
        }
    }

    private void Collect(HangingNode node, HashSet<HangingNode> visited)
    {
        if (node == null) return;
        if (!visited.Add(node))
        {
            Debug.LogError($"[ScaleSystem] '{node.name}'이(가) 두 군데 이상 매달려 있습니다. Attachments 연결을 확인하세요.", node);
            return;
        }

        allNodes.Add(node);
        if (node is Basket basket) baskets.Add(basket);

        if (node is ScaleBeam beam)
        {
            foreach (ScaleBeam.Attachment a in beam.Attachments)
                Collect(a.node, visited);
        }
    }

    private void FixedUpdate()
    {
        if (root == null || GameManager.Inst.gameOver) return;

        root.RecalculateMass();
        root.Step(rootHook, Time.fixedDeltaTime);

        // 스테이지 시작 직후 초기 공이 자리 잡는 동안은 기울기 게임오버를 판정하지 않는다.
        if (StageManager.Inst != null && StageManager.Inst.IsSettling) return;

        foreach (Basket basket in baskets)
        {
            if (basket.IsOverTilted)
            {
                Debug.Log($"[{basket.name}] {basket.GameOverAngle}도 이상 기울어짐 -> Game Over");
                GameManager.Inst.TriggerGameOver();
                break;
            }
        }
    }

    // 모든 저울대와 바구니를 수평 자세로 즉시 되돌린다.
    public void ResetPose()
    {
        if (root == null) return;

        foreach (HangingNode node in allNodes)
            node.SetSnapAngle(0f);

        root.SnapTree(rootHook);
        Physics2D.SyncTransforms();
    }

    public List<float> GetAngles()
    {
        List<float> angles = new List<float>(allNodes.Count);
        foreach (HangingNode node in allNodes) angles.Add(node.CurrentAngle);
        return angles;
    }

    // 저장된 각도로 즉시 배치한다. 저장 당시와 노드 개수가 다르면 적용하지 않는다.
    public bool ApplyAngles(List<float> angles)
    {
        if (angles == null || angles.Count != allNodes.Count) return false;

        for (int i = 0; i < allNodes.Count; i++)
            allNodes[i].SetSnapAngle(angles[i]);

        root.SnapTree(rootHook);
        Physics2D.SyncTransforms();
        return true;
    }
}
