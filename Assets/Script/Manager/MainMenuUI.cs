using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 메인 화면. 시작, 튜토리얼 버튼과 진행할 스테이지 표시, 스테이지 선택, 개발자용 스테이지 고정을 처리한다.
// 이 씬에는 Managers(GameManager 등)를 두지 않는다.
//
// 스테이지 번호 두 개를 다룬다.
//  - 현재 스테이지: 시작 버튼을 누르면 할 스테이지
//  - 마지막 스테이지: 클리어 기준으로 도달한 가장 먼 스테이지(아직 깨지 않은 첫 스테이지)
// 시작 버튼을 꾹 누르면 1 ~ 마지막 스테이지 중에서 현재 스테이지를 고를 수 있다.
public class MainMenuUI : MonoBehaviour
{
    [Header("버튼")]
    [SerializeField] private Button startButton;
    [Tooltip("시작 버튼을 꾹 누르면 스테이지 선택 창을 연다.")]
    [SerializeField] private LongPressButton startLongPress;
    [SerializeField] private Button tutorialButton;
    [Tooltip("오른쪽 위 설정 버튼. 지금은 개발자 패널을 연다.")]
    [SerializeField] private Button settingsButton;

    [Header("스테이지 표시")]
    [SerializeField] private TextMeshProUGUI stageText;
    [Tooltip("현재 스테이지와 마지막 스테이지가 같을 때. {0}: 현재")]
    [SerializeField] private string stageFormat = "Stage : {0}";
    [Tooltip("현재 스테이지와 마지막 스테이지가 다를 때. {0}: 현재, {1}: 마지막")]
    [SerializeField] private string stageWithMaxFormat = "Stage : {0}\n<size=60%>최고 {1}</size>";

    [Header("스테이지 선택 창 (시작 버튼 길게 누르기)")]
    [SerializeField] private GameObject stagePopup;
    [SerializeField] private TextMeshProUGUI stagePopupTitle;
    [SerializeField] private TMP_InputField stagePopupInput;
    [SerializeField] private Button stagePopupConfirmButton;
    [SerializeField] private Button stagePopupCancelButton;
    [Tooltip("{0}: 마지막 스테이지")]
    [SerializeField] private string stagePopupTitleFormat = "스테이지 선택 (1 ~ {0})";
    [SerializeField] private string stagePopupInvalidText = "1 이상의 숫자를 입력하세요";

    [Header("스테이지 데이터")]
    [Tooltip("개발자 패널에서 없는 스테이지 번호를 걸러낼 때 쓴다. Assets/Aset/Data/GameContent.asset")]
    [SerializeField] private GameContentCatalog gameContent;

    [Header("개발자 패널")]
    [SerializeField] private GameObject devPanel;
    [SerializeField] private TMP_InputField stageInput;
    [SerializeField] private Button applyStageButton;
    [SerializeField] private Button resetTutorialButton;
    [SerializeField] private Button closeDevButton;
    [Tooltip("켜면 개발 빌드와 에디터에서만 설정 버튼을 보여준다.")]
    [SerializeField] private bool devToolsOnlyInDebug = true;

    private HashSet<int> stageNumbers;

    private void Awake()
    {
#if UNITY_EDITOR
        if (gameContent == null)
            gameContent = UnityEditor.AssetDatabase.LoadAssetAtPath<GameContentCatalog>(PuzzlePaths.ContentCatalogAsset);
#endif
        if (startButton != null) startButton.onClick.AddListener(OnStartClicked);
        if (startLongPress != null) startLongPress.onLongPress.AddListener(OpenStagePopup);
        if (tutorialButton != null) tutorialButton.onClick.AddListener(SceneFlow.LoadTutorial);
        if (settingsButton != null) settingsButton.onClick.AddListener(ToggleDevPanel);

        if (stagePopupConfirmButton != null) stagePopupConfirmButton.onClick.AddListener(ConfirmStagePopup);
        if (stagePopupCancelButton != null) stagePopupCancelButton.onClick.AddListener(CloseStagePopup);
        if (stagePopupInput != null) stagePopupInput.onSubmit.AddListener(_ => ConfirmStagePopup());

        if (applyStageButton != null) applyStageButton.onClick.AddListener(ApplyStage);
        if (resetTutorialButton != null) resetTutorialButton.onClick.AddListener(ResetTutorial);
        if (closeDevButton != null) closeDevButton.onClick.AddListener(() => devPanel.SetActive(false));
        if (stageInput != null) stageInput.onSubmit.AddListener(_ => ApplyStage());

        if (stagePopup != null) stagePopup.SetActive(false);
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
        // 길게 눌러 선택 창을 열었으면 손을 뗄 때 들어오는 클릭은 무시한다.
        if (startLongPress != null && startLongPress.LongPressTriggered) return;
        if (stagePopup != null && stagePopup.activeSelf) return;

        if (devPanel != null && devPanel.activeSelf) ApplyStage();
        SceneFlow.StartGame();
    }

    private void RefreshStage()
    {
        if (stageText == null) return;

        SaveManager.Progress progress = SaveManager.ReadProgress();
        stageText.text = progress.stage == progress.maxStage
            ? string.Format(stageFormat, progress.stage)
            : string.Format(stageWithMaxFormat, progress.stage, progress.maxStage);
    }

    // ─── 스테이지 선택 창 ───

    private void OpenStagePopup()
    {
        if (stagePopup == null) return;
        if (devPanel != null) devPanel.SetActive(false);

        SaveManager.Progress progress = SaveManager.ReadProgress();
        if (stagePopupTitle != null) stagePopupTitle.text = string.Format(stagePopupTitleFormat, progress.maxStage);
        stagePopup.SetActive(true);

        if (stagePopupInput != null)
        {
            stagePopupInput.text = progress.stage.ToString();
            stagePopupInput.Select();
            stagePopupInput.ActivateInputField();
        }
    }

    private void CloseStagePopup()
    {
        if (stagePopup != null) stagePopup.SetActive(false);
    }

    // 1 이상만 받는다. 마지막 스테이지를 넘는 번호를 고르면 마지막 스테이지로 맞춘다.
    private void ConfirmStagePopup()
    {
        if (stagePopup == null || !stagePopup.activeSelf) return;

        if (stagePopupInput == null || !int.TryParse(stagePopupInput.text, out int stage) || stage < 1)
        {
            if (stagePopupTitle != null) stagePopupTitle.text = stagePopupInvalidText;
            Debug.LogWarning("[MainMenu] 1 이상의 스테이지 번호를 입력하세요.");
            return;
        }

        SaveManager.Progress progress = SaveManager.ReadProgress();
        if (stage > progress.maxStage)
        {
            Debug.Log($"[MainMenu] Stage {stage}는 아직 열리지 않아 마지막 Stage {progress.maxStage}로 맞춥니다.");
            stage = progress.maxStage;
        }

        SaveManager.WriteProgress(stage, progress.maxStage);
        SceneFlow.PendingStage = 0;
        RefreshStage();
        CloseStagePopup();
    }

    // ─── 개발자 패널 ───

    private void ToggleDevPanel()
    {
        if (devPanel == null) return;

        bool show = !devPanel.activeSelf;
        devPanel.SetActive(show);
        if (show && stageInput != null) stageInput.text = SaveManager.PeekSavedStage().ToString();
    }

    // 개발자용 강제 고정: 깨지 않은 스테이지라도 입력한 스테이지를 현재이자 마지막 스테이지로 저장한다.
    // StageData.json에 없는 번호면 진행을 1스테이지로 초기화한다.
    private void ApplyStage()
    {
        if (stageInput == null || !int.TryParse(stageInput.text, out int stage) || stage < 1)
        {
            Debug.LogWarning("[MainMenu] 1 이상의 스테이지 번호를 입력하세요.");
            return;
        }

        if (!StageExists(stage))
        {
            Debug.LogWarning($"[MainMenu] Stage {stage} 데이터가 없어 진행을 Stage 1로 초기화합니다.");
            stage = 1;
        }

        SaveManager.WriteProgress(stage, stage);
        SceneFlow.PendingStage = 0;
        Debug.Log($"[MainMenu] 개발자 고정: Stage {stage}");
        stageInput.text = stage.ToString();
        RefreshStage();
        devPanel.SetActive(false);
    }

    // StageData.json에 있는 스테이지 번호인지. 데이터를 읽을 수 없으면 확인하지 않고 true를 돌려준다.
    private bool StageExists(int stage)
    {
        if (stageNumbers == null)
        {
            TextAsset json = gameContent != null ? gameContent.stageData : null;
            if (json == null)
            {
                Debug.LogError("[MainMenu] Game Content가 연결되지 않아 스테이지 번호를 확인할 수 없습니다.");
                return true;
            }

            try
            {
                StageDatabase database = JsonUtility.FromJson<StageDatabase>(json.text);
                var numbers = new HashSet<int>();
                if (database != null && database.stages != null)
                    foreach (StageDefinition definition in database.stages)
                        numbers.Add(definition.stage);
                stageNumbers = numbers;
            }
            catch (System.ArgumentException exception)
            {
                Debug.LogError("[MainMenu] 스테이지 JSON을 읽을 수 없습니다: " + exception.Message);
                return true;
            }
        }

        return stageNumbers.Contains(stage);
    }

    // 튜토리얼 코드: 완료 기록을 지워 다음 시작 때 튜토리얼부터 하게 한다.
    private void ResetTutorial()
    {
        PlayerPrefs.DeleteKey(TutorialManager.CompletedKey);
        PlayerPrefs.Save();
        Debug.Log("[MainMenu] 튜토리얼 완료 기록을 지웠습니다.");
    }
}
