using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// 버튼을 일정 시간 꾹 누르면 onLongPress를 부른다.
// 길게 누른 뒤 손을 떼면 Button.onClick도 같이 불리므로, 클릭 처리 쪽에서 LongPressTriggered를 확인해 무시한다.
public class LongPressButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("이 시간(초) 동안 누르고 있으면 길게 누르기로 본다.")]
    [SerializeField, Min(0.1f)] private float holdSeconds = 0.6f;

    public UnityEvent onLongPress = new UnityEvent();

    // 마지막 누르기가 길게 누르기였으면 true. 다음에 누를 때 false로 돌아간다.
    public bool LongPressTriggered { get; private set; }

    private bool pressing;
    private float timer;

    public void OnPointerDown(PointerEventData eventData)
    {
        pressing = true;
        timer = 0f;
        LongPressTriggered = false;
    }

    public void OnPointerUp(PointerEventData eventData) => pressing = false;

    public void OnPointerExit(PointerEventData eventData) => pressing = false;

    private void OnDisable() => pressing = false;

    private void Update()
    {
        if (!pressing) return;

        timer += Time.unscaledDeltaTime;
        if (timer < holdSeconds) return;

        pressing = false;
        LongPressTriggered = true;
        onLongPress.Invoke();
    }
}
