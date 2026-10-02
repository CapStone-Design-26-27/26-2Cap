using UnityEngine;

public class BallBehaviour : MonoBehaviour
{
    public int level;
    public bool isMerged = false;
    public bool isDroppedByPlayer = false;

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

                    SpawnManager.Inst.SpawnMergedBall(nextLevel, mergePos);
                    GameManager.Inst.AddScore((nextLevel * 10));

                    Destroy(this.gameObject);
                    Destroy(otherBall.gameObject);
                }
            }
        }
    }
}
