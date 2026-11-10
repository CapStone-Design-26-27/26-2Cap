using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// NEXT는 큐를 소비하지 않고 전체 지급 수량과 남은 투입 순서를 보여준다.
public class BallQueueUI : MonoBehaviour
{
    public static BallQueueUI Inst { get; private set; }
    [SerializeField] private Button nextButton;
    [SerializeField] private Image nextIcon;
    [SerializeField] private TextMeshProUGUI remainingText;
    [SerializeField] private TextMeshProUGUI totalText;
    [SerializeField] private GameObject inventoryPanel;
    [SerializeField] private RectTransform orderRoot;
    [SerializeField] private GameObject iconTemplate;
    private readonly List<GameObject> entries = new List<GameObject>();
    private bool expanded;

    private void Awake()
    {
        Inst = this;
        nextButton.onClick.AddListener(ToggleInventory);
        inventoryPanel.SetActive(false);
        iconTemplate.SetActive(false);
    }

    private void OnEnable()
    {
        SpawnManager.OnNextBallChanged += Refresh;
        StageManager.OnStageStarted += HandleStageStarted;
    }

    private void Start()
    {
        Refresh(-1);
    }

    private void OnDisable()
    {
        SpawnManager.OnNextBallChanged -= Refresh;
        StageManager.OnStageStarted -= HandleStageStarted;
    }

    private void OnDestroy()
    {
        if (Inst == this) Inst = null;
    }

    private void HandleStageStarted(int stage)
    {
        expanded = false;
        inventoryPanel.SetActive(false);
        Refresh(-1);
    }

    public void ToggleInventory()
    {
        if (SpawnManager.Inst == null) return;
        SpawnManager.Inst.CancelAim();
        expanded = !expanded;
        inventoryPanel.SetActive(expanded);
        Refresh(-1);
    }

    private void Refresh(int unused)
    {
        if (SpawnManager.Inst == null || GameManager.Inst == null) return;
        List<int> levels = SpawnManager.Inst.GetRemainingLevels();
        int total = StageManager.Inst != null ? StageManager.Inst.TotalProvidedBalls : levels.Count;
        // 튜토리얼 코드: 튜토리얼은 정해진 순서가 계속 반복되므로 수량 대신 반복 표시를 한다.
        bool tutorial = TutorialManager.Inst != null;
        remainingText.text = tutorial ? "REPEAT" : $"LEFT <b><size=48>{levels.Count}</size></b>";
        totalText.text = tutorial ? "TUTORIAL" : $"TOTAL {total}";
        nextIcon.sprite = GetSprite(SpawnManager.Inst.getNextBall());
        nextIcon.enabled = nextIcon.sprite != null;
        foreach (GameObject entry in entries)
        {
            entry.SetActive(false);
            Destroy(entry);
        }
        entries.Clear();
        if (!expanded) return;

        // 순서 번호는 재투입 큐부터 시작한다. R은 떨어져서 돌아온 공이다.
        for (int i = 0; i < levels.Count; i++)
            AddIcon(levels[i], i < SpawnManager.Inst.RetryBallCount ? $"R{i + 1}" : $"{i + 1}");
    }

    private Sprite GetSprite(int level)
    {
        if (level < 0 || level >= GameManager.Inst.ballList.Count) return null;
        return GameManager.Inst.ballList[level].GetComponent<SpriteRenderer>().sprite;
    }

    private void AddIcon(int level, string caption)
    {
        GameObject entry = Instantiate(iconTemplate, orderRoot);
        entry.SetActive(true);
        entry.GetComponentInChildren<Image>().sprite = GetSprite(level);
        entry.GetComponentInChildren<TextMeshProUGUI>().text = caption;
        entries.Add(entry);
    }
}
