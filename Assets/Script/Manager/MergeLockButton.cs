using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// 튜토리얼 코드. 합성 잠금 버튼. 켜져 있는 동안 공이 합쳐지지 않는다.
// holdToLock을 켜면 누르고 있는 동안만 잠근다.
[RequireComponent(typeof(Button))]
public class MergeLockButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [SerializeField] private Image background;
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private bool holdToLock = false;

    [Header("표시")]
    [SerializeField] private Color offColor = Color.white;
    [SerializeField] private Color onColor = new Color(1f, 0.82f, 0.3f);
    [SerializeField] private string offText = "잠금";
    [SerializeField] private string onText = "해제";

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (background == null) background = GetComponent<Image>();
        if (label == null) label = GetComponentInChildren<TextMeshProUGUI>();

        button.onClick.AddListener(OnClick);
        Refresh();
    }

    private void OnClick()
    {
        if (holdToLock) return;
        BallMergeLock.SetGlobalLocked(!BallMergeLock.GlobalLocked);
        Refresh();
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!holdToLock) return;
        BallMergeLock.SetGlobalLocked(true);
        Refresh();
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!holdToLock) return;
        BallMergeLock.SetGlobalLocked(false);
        Refresh();
    }

    private void Refresh()
    {
        bool locked = BallMergeLock.GlobalLocked;
        if (background != null) background.color = locked ? onColor : offColor;
        if (label != null) label.text = locked ? onText : offText;
    }
}
