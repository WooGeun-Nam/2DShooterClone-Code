using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class MobileHoldButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
{
    [Tooltip("이 동작을 적용할 대상(플레이어)의 PlayerMover2D")]
    public PlayerMover2D target;

    [Tooltip("버튼을 누르고 있는 동안 줄 입력 값 (-1=왼쪽, 1=오른쪽)")]
    public float holdValue = -1f;

    private bool _pressed;

    public void OnPointerDown(PointerEventData eventData)
    {
        _pressed = true;
        if (target != null) target.SetInput(holdValue);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        _pressed = false;
        if (target != null) target.SetInput(0f);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        // 손가락이 버튼 밖으로 나가도 안전하게 정지
        if (_pressed)
        {
            _pressed = false;
            if (target != null) target.SetInput(0f);
        }
    }

    void OnDisable()
    {
        // 씬 전환/비활성화 시 '눌린 채'로 남지 않게 방지
        if (target != null) target.SetInput(0f);
        _pressed = false;
    }
}