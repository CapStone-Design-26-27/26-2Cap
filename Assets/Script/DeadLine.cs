using System.Collections.Generic;
using UnityEngine;

// 공이 바구니 테두리 선 위에 timeLimit초 이상 머물면 게임오버. 바구니의 컵 안에 두어 함께 움직인다.
public class DeadLine : MonoBehaviour
{
    [SerializeField]
    private float timeLimit = 2.0f;

    private float overTime = 0f;

    private List<Collider2D> ballsInZone = new List<Collider2D>();

    void Update()
    {
        // 이미 게임 오버 상태면 계산 중지
        if (GameManager.Inst.gameOver) return;

        // 합쳐져서 파괴된 공은 목록에서 제거
        ballsInZone.RemoveAll(c => c == null);

        // 스테이지 시작 직후 초기 공이 떨어지며 선을 지나가는 것은 세지 않는다.
        if (StageManager.Inst != null && StageManager.Inst.IsSettling)
        {
            overTime = 0f;
            return;
        }

        if (ballsInZone.Count > 0)
        {
            overTime += Time.deltaTime;

            // 제한 시간이 지나면 게임 오버
            if (overTime >= timeLimit)
            {
                GameManager.Inst.TriggerGameOver();
            }
        }
        else
        {
            overTime = 0f;
        }
    }

    // 공이 데드라인 영역에 들어왔을 때
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.GetComponent<BallBehaviour>() != null && !ballsInZone.Contains(collision))
        {
            ballsInZone.Add(collision);
        }
    }

    // 공이 데드라인 영역에서 빠져나갔을 때
    private void OnTriggerExit2D(Collider2D collision)
    {
        ballsInZone.Remove(collision);
    }
}
