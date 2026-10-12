using UnityEngine;

public class BallBehaviour : MonoBehaviour
{
    public int level;
    public bool isMerged = false;
    public bool isDroppedByPlayer = false;

    // 이 공이 속한 바구니
    [HideInInspector] public Basket owner;

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
        {
            if (!otherBall.isMerged && this.level == otherBall.level)
            {
                if (this.level < GameManager.Inst.ballList.Count - 1)
                {
                    this.isMerged = true;
                    otherBall.isMerged = true;

                    Vector2 mergePos = (transform.position + otherBall.transform.position) / 2f;
                    int nextLevel = this.level + 1;

                    // 합쳐진 공은 부모 공과 같은 바구니에 속한다.
                    SpawnManager.Inst.SpawnMergedBall(nextLevel, mergePos, this.owner);

                    Destroy(this.gameObject);
                    Destroy(otherBall.gameObject);
                }
            }
        }
    }
}
