using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 기물 사용 버튼 UI.
/// Inspector: button, iconImage, countText 연결
/// </summary>
public class StructureButton : MonoBehaviour
{
    [SerializeField] private Button   button;
    [SerializeField] private Image    iconImage;
    [SerializeField] private TMP_Text countText;

    private bool subscribed;

    private void Awake()
    {
        button?.onClick.AddListener(OnClick);
        // 초기 상태: 아이콘 숨김
        if (iconImage != null) iconImage.enabled = false;
        if (countText != null) countText.text    = "";
        if (button    != null) button.interactable = false;
    }

    private void Update()
    {
        // StructureInventory가 늦게 초기화될 수 있어 매 프레임 구독 시도
        if (!subscribed && StructureInventory.Shared != null)
        {
            StructureInventory.Shared.OnChanged += Refresh;
            subscribed = true;
            Refresh();
        }
    }

    private void OnDisable()
    {
        if (subscribed && StructureInventory.Shared != null)
            StructureInventory.Shared.OnChanged -= Refresh;
        subscribed = false;
    }

    private void Refresh()
    {
        var inv   = StructureInventory.Shared;
        int count = inv?.Count ?? 0;
        var top   = inv?.Peek;

        Sprite icon = top?.f_설치프리팹 != null
            ? top.f_설치프리팹.GetComponentInChildren<SpriteRenderer>()?.sprite
            : null;

        if (iconImage != null)
        {
            iconImage.sprite  = icon;
            iconImage.enabled = icon != null;
        }

        if (countText != null)
            countText.text = count > 0 ? count.ToString() : "";

        if (button != null)
            button.interactable = count > 0;
    }

    private void OnClick()
    {
        SoundManager.Shared?.Play(SoundKey.UI_Click);
        StructureInventory.Shared?.UseTop();
    }
}
