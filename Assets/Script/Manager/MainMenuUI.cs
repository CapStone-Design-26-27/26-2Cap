using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 메인 화면. 시작, 튜토리얼 버튼과 진행할 스테이지 표시, 개발자용 스테이지 지정을 처리한다.
// 이 씬에는 Managers(GameManager 등)를 두지 않는다.
public class MainMenuUI : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button startButton;
    [SerializeField] private Button tutorialButton;
    [Tooltip("오른쪽 위 설정 버튼. 지금은 개발자 패널을 연다.")]
    [SerializeField] private Button settingsButton;

    [Header("스테이지 표시")]
    [SerializeField] private TextMeshProUGUI stageText;
    [SerializeField] private string stageFormat = "Stage : {0}";

    [Header("개발자 패널")]
    [SerializeField] private GameObject devPanel;
    [SerializeField] private TMP_InputField stageInput;
    [SerializeField] private Button applyStageButton;
    [SerializeField] private Button resetTutorialButton;
    [SerializeField] private Button closeDevButton;
    [Tooltip("켜면 개발 빌드와 에디터에서만 설정 버튼을 보여준다.")]
    [SerializeField] private bool devToolsOnlyInDebug = true;

    private int StageToShow => SceneFlow.PendingStage > 0 ? SceneFlow.PendingStage : SaveManager.PeekSavedStage();

    private void Awake()
    {
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(SceneFlow.LoadTutorial);
        if (settingsButton != null) settingsButton.onClick.AddListener(ToggleDevPanel);
        if (applyStageButton != null) applyStageButton.onClick.AddListener(ApplyStage);
        if (resetTutorialButton != null) resetTutorialButton.onClick.AddListener(ResetTutorial);
        if (closeDevButton != null) closeDevButton.onClick.AddListener(() => devPanel.SetActive(false));
        if (stageInput != null) stageInput.onSubmit.AddListener(_ => ApplyStage());

        if (devPanel != null) devPanel.SetActive(false);
        if (settingsButton != null && devToolsOnlyInDebug && !Debug.isDebugBuild)
            settingsButton.gameObject.SetActive(false);
    }

    private void Start()
    {
        RefreshStage();
    }

    // 개발자 패널을 연 채로 시작하면 입력한 스테이지를 먼저 적용한다.
    private void OnStartClicked()
    {
        if (devPanel != null && devPanel.activeSelf) ApplyStage();
        SceneFlow.StartGame();
    }

    private void RefreshStage()
    {
        if (stageText != null) stageText.text = string.Format(stageFormat, StageToShow);
    }

    // ─── 개발자 패널 ───

    private void ToggleDevPanel()
    {
        if (devPanel == null) return;

        bool show = !devPanel.activeSelf;
        devPanel.SetActive(show);
        if (show && stageInput != null) stageInput.text = StageToShow.ToString();
    }

    // 입력한 스테이지를 다음 게임 시작 때 사용한다. 저장 파일은 그 스테이지를 시작할 때 덮어쓴다.
    private void ApplyStage()
    {
        if (stageInput == null || !int.TryParse(stageInput.text, out int stage) || stage < 1)
        {
            Debug.LogWarning("[MainMenu] 1 이상의 스테이지 번호를 입력하세요.");
            return;
        }

        SceneFlow.PendingStage = stage;
        Debug.Log($"[MainMenu] 다음 시작 스테이지: {stage}");
        RefreshStage();
        devPanel.SetActive(false);
    }

    // 튜토리얼 코드: 완료 기록을 지워 다음 시작 때 튜토리얼부터 하게 한다.
    private void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(TutorialManager.CompletedKey);
        PlayerPrefs.Save();
        Debug.Log("[MainMenu] 튜토리얼 완료 기록을 지웠습니다.");
    }
}
