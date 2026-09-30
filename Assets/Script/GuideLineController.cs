using UnityEngine;

public class GuideLineController : MonoBehaviour
{
    [SerializeField] private GameObject guideLineVisual;
    private Transform targetBall;

    private void OnEnable()
    {
        SpawnManager.OnAimStart += ShowGuideLine;
        SpawnManager.OnAimEnd += HideGuideLine;
    }

    private void OnDisable()
    {
        SpawnManager.OnAimStart -= ShowGuideLine;
        SpawnManager.OnAimEnd -= HideGuideLine;
    }

    private void Start()
    {
        HideGuideLine();
    }

    private void ShowGuideLine(Transform ball)
    {
        targetBall = ball;
        if (guideLineVisual != null) guideLineVisual.SetActive(true);
    }

    private void HideGuideLine()
    {
        targetBall = null;
        if (guideLineVisual != null) guideLineVisual.SetActive(false);
    }

    // 조준 중인 공을 따라다니며 떨어질 위치를 보여준다.
    private void LateUpdate()
    {
        if (targetBall != null)
        {
            Vector3 newPos = targetBall.position;
            transform.position = newPos;
        }
    }
}
