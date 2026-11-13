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

    internal RectTransform NextButtonRect => (RectTransform)nextButton.transform;

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
        if (expanded) TutorialManager.Inst?.OnDropOrderOpened();
    }

    internal void CollapseInventory()
    {
        if (!expanded) return;
        expanded = false;
        inventoryPanel.SetActive(false);
    }

    private void Refresh(int unused)
    {
        if (SpawnManager.Inst == null || GameManager.Inst == null) return;
        List<int> levels = SpawnManager.Inst.GetRemainingLevels();
        int total = StageManager.Inst?.TotalProvidedBalls ?? TutorialManager.Inst?.TotalProvidedBalls ?? levels.Count;
        remainingText.text = $"LEFT <b><size=48>{levels.Count}</size></b>";
        totalText.text = $"TOTAL {total}";
        nextIcon.sprite = GetSprite(SpawnManager.Inst.getNextBall());
        nextIcon.enabled = nextIcon.sprite != null;
        if (TutorialManager.Inst != null && levels.Count == 0 && expanded)
        {
            expanded = false;
            inventoryPanel.SetActive(false);
        }
        foreach (GameObject entry in entries)
        {
            entry.SetActive(false);
            Destroy(entry);
        }
        entries.Clear();
        if (!expanded) return;

        for (int i = 0; i < levels.Count; i++)
            AddIcon(levels[i], $"{i + 1}");
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
