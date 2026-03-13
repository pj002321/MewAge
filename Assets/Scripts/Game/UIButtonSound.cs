using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 버튼에 붙여서 Hover / Click 사운드를 재생합니다.
/// </summary>
public class UIButtonSound : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [SerializeField] private SoundKey hoverKey = SoundKey.UI_Hover;
    [SerializeField] private SoundKey clickKey = SoundKey.UI_Click;

    public void OnPointerEnter(PointerEventData eventData)
    {
        SoundManager.Shared?.Play(hoverKey);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        SoundManager.Shared?.Play(clickKey);
    }
}
