using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI angleText;
    [SerializeField] private Image nextBall;

    // 바구니 집중 중에만 보이는 전체 보기 버튼
    [SerializeField] private GameObject btnOverview;

    private void OnEnable()
    {
        GameManager.OnScoreChanged += UpdateScoreUI;
        SpawnManager.OnNextBallChanged += UpdateNextBallUI;
        CameraFocusController.OnFocusChanged += UpdateCameraUI;
    }

    private void OnDisable()
    {
        GameManager.OnScoreChanged -= UpdateScoreUI;
        SpawnManager.OnNextBallChanged -= UpdateNextBallUI;
        CameraFocusController.OnFocusChanged -= UpdateCameraUI;
    }

    private void Start()
    {
        UpdateScoreUI(GameManager.Inst.score);
        UpdateNextBallUI(SpawnManager.Inst.getNextBall());
        UpdateCameraUI(GameManager.Inst.FocusedBasket);
    }

    private void Update()
    {
        if (GameManager.Inst.gameOver)
            return;

        PrintAngle();
    }

    private void UpdateScoreUI(int currentScore)
    {
        if (scoreText != null)
        {
            scoreText.text = $"Score : {currentScore:D4}";
        }
    }

    private void UpdateNextBallUI(int nextLevel)
    {
        if (nextBall == null)
            return;

        if (nextLevel >= 0 && nextLevel < GameManager.Inst.ballList.Count)
        {
            Sprite nextSprite = GameManager.Inst.ballList[nextLevel]
                .GetComponent<SpriteRenderer>()
                .sprite;

            nextBall.sprite = nextSprite;
            nextBall.color = Color.white;
        }
    }

    // 집중 중인 바구니의 기울기. 게임오버 각도에 가까울수록 주황, 빨강으로 표시
    private void PrintAngle()
    {
        if (angleText == null)
            return;

        Basket basket = GameManager.Inst.FocusedBasket;

        if (basket == null)
        {
            angleText.text = "-";
            angleText.color = Color.black;
            return;
        }

        float angle = basket.CurrentAngle;
        float ratio = Mathf.Abs(angle) / basket.GameOverAngle;

        angleText.text = $"{angle:F0}°";

        if (ratio >= 2f / 3f)
            angleText.color = Color.red;
        else if (ratio >= 1f / 3f)
            angleText.color = new Color(1f, 0.5f, 0f);
        else
            angleText.color = Color.black;
    }

    private void UpdateCameraUI(Basket focused)
    {
        if (btnOverview != null)
            btnOverview.SetActive(focused != null);
    }
}
