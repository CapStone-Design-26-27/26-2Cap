using UnityEngine;

public class BallBehaviour : MonoBehaviour
{
    public int level;
    public bool isMerged = false;
    public bool isDroppedByPlayer = false;

    // 이 공이 속한 바구니
    [HideInInspector] public Basket owner;

    private static readonly Collider2D[] contactBuffer = new Collider2D[16];

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (isMerged) return;

        if (collision.gameObject.CompareTag("DeadZone"))
        {
            // 방금 던진 공인지 함께 넘겨, 쌓여 있다 쏟아진 공과 구분한다.
            SpawnManager.Inst.OnBallHitGroundAndRetry(level, this.gameObject, isDroppedByPlayer);
            return;
        }

        if (isDroppedByPlayer)
        {
            isDroppedByPlayer = false;
            SpawnManager.Inst.OnBallLanded();
        }

        // 공끼리 부딪혔는지 확인 (Merge 로직)
        BallBehaviour otherBall = collision.gameObject.GetComponent<BallBehaviour>();
        if (otherBall != null)
            TryMerge(otherBall);
    }

    // 튜토리얼 코드: 합성 잠금이 풀렸을 때 이미 맞닿아 있는 공과 합성을 다시 시도한다.
    public void TryMergeTouching()
    {
        if (isMerged) return;

        Collider2D col = GetComponent<Collider2D>();
        if (col == null || !col.enabled) return;

        int count = col.GetContacts(contactBuffer);
        for (int i = 0; i < count; i++)
        {
            BallBehaviour other = contactBuffer[i] != null ? contactBuffer[i].GetComponent<BallBehaviour>() : null;
            if (other != null && TryMerge(other)) return;
        }
    }

    // 같은 레벨이고 잠겨 있지 않으면 두 공을 다음 레벨 공 하나로 합친다.
    private bool TryMerge(BallBehaviour otherBall)
    {
        if (otherBall.isMerged || this.level != otherBall.level) return false;

        // 튜토리얼 코드: 합성 잠금 버튼이 켜져 있으면 합치지 않는다.
        if (BallMergeLock.GlobalLocked) return false;

        if (!BallMergeLock.CanMerge(gameObject) || !BallMergeLock.CanMerge(otherBall.gameObject)) return false;
        if (this.level >= GameManager.Inst.ballList.Count - 1) return false;

        // 던진 공이 상대 쪽에서 먼저 합쳐지면 착지 처리가 빠지므로 여기서 처리한다.
        if (otherBall.isDroppedByPlayer)
        {
            otherBall.isDroppedByPlayer = false;
            SpawnManager.Inst.OnBallLanded();
        }

        this.isMerged = true;
        otherBall.isMerged = true;

        Vector2 mergePos = (transform.position + otherBall.transform.position) / 2f;
        int nextLevel = this.level + 1;

        // 합쳐진 공은 부모 공과 같은 바구니에 속한다.
        SpawnManager.Inst.SpawnMergedBall(nextLevel, mergePos, this.owner);

        Destroy(this.gameObject);
        Destroy(otherBall.gameObject);
        return true;
    }
}
