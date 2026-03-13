using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 슬롯 하나. 클릭 시 확대 하이라이트 + 옆에 장착/팔기 패널 표시.
/// 장착 슬롯에서 드래그된 무기를 받을 수 있습니다.
/// </summary>
public class InvenSlot : MonoBehaviour, IPointerClickHandler, IDropHandler
{
    [SerializeField] private Image frameImg;   // 등급 색상 적용
    [SerializeField] private Image iconImg;    // 무기 아이콘

    [Header("선택 확대")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float normalScale   = 1.0f;

    public int SlotIndex { get; set; }

    private UpgradePanel panel;

    public void Init(UpgradePanel upgradePanel) => panel = upgradePanel;

    // ────────────────────────────────────────────────────────────────
    // 슬롯 갱신
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData data, bool selected)
    {
        bool has = data != null;
        if (iconImg)  iconImg.enabled = has;
        if (frameImg) frameImg.color = has ? WeaponGradeColors.Get(data.grade) : Color.white;
        if (has && iconImg && data.icon) iconImg.sprite = data.icon;

        // 선택 시 확대, 해제 시 원래 크기
        float s = (has && selected) ? selectedScale : normalScale;
        transform.localScale = Vector3.one * s;
    }

    // ────────────────────────────────────────────────────────────────
    // 클릭
    // ────────────────────────────────────────────────────────────────
    public void OnPointerClick(PointerEventData eventData)
    {
        Debug.Log($"[InvenSlot] OnPointerClick: index={SlotIndex}, panel={(panel != null ? "OK" : "NULL")}");
        panel?.OnInvenSlotClicked(SlotIndex, transform);
    }

    // ────────────────────────────────────────────────────────────────
    // 드롭 (장착 슬롯 → 인벤 슬롯 이동/교체)
    // ────────────────────────────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        if (EquipSlot.Dragging == null) return;
        PlayerInventory.Shared?.MoveEquipToInven(EquipSlot.Dragging.SlotIndex, SlotIndex);
    }
}
