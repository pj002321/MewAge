using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 장비 패널.
/// 상단: 장착 슬롯 6개 (EquipmentSlotView) — 좌3 / 우3
/// 하단: 탭(전체 / 등급순 / 문명순) + 보유 장비 스크롤 목록
///
/// Inspector 연결:
///   equipSlots[6]    — 장착 슬롯 EquipmentSlotView 6개
///   tabAll / tabByGrade / tabByCiv — 탭 버튼
///   inventoryContent — 인벤 그리드 Content
///   slotPrefab       — EquipmentSlotView 프리팹
///   detailPopup      — EquipmentDetailPopup
/// </summary>
public class HomeEquipPanel : MonoBehaviour
{
    [Header("장착 슬롯 (0~2=왼쪽, 3~5=오른쪽)")]
    public EquipmentSlotView[] equipSlots = new EquipmentSlotView[6];

    [Header("탭 버튼")]
    public Button tabAll;
    public Button tabByGrade;
    public Button tabByCiv;

    [Header("탭 색상")]
    public Color tabActiveColor   = new Color(1f,    0.80f, 0.20f, 1f);
    public Color tabInactiveColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    [Header("인벤토리")]
    public Transform            inventoryContent;
    public EquipmentSlotView    slotPrefab;
    public EquipmentDetailPopup detailPopup;

    EquipSortMode _sortMode = EquipSortMode.None;

    void Awake()
    {
        if (EquipmentInventory.Shared == null)
            MonoBehaviourSingleton<EquipmentInventory>.Create("EquipmentInventory");
    }

    void Start()
    {
        tabAll?.onClick.AddListener(()     => SetSort(EquipSortMode.None));
        tabByGrade?.onClick.AddListener(() => SetSort(EquipSortMode.ByGrade));
        tabByCiv?.onClick.AddListener(()   => SetSort(EquipSortMode.ByCivilization));
        SetSort(EquipSortMode.None);
    }

    void OnEnable()
    {
        RefreshAll();
        var inv = EquipmentInventory.Shared;
        if (inv != null) inv.OnChanged += RefreshAll;
    }

    void OnDisable()
    {
        var inv = EquipmentInventory.Shared;
        if (inv != null) inv.OnChanged -= RefreshAll;
    }

    void SetSort(EquipSortMode mode)
    {
        _sortMode = mode;
        SetTabColor(tabAll,     mode == EquipSortMode.None);
        SetTabColor(tabByGrade, mode == EquipSortMode.ByGrade);
        SetTabColor(tabByCiv,   mode == EquipSortMode.ByCivilization);
        RefreshInventory();
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? tabActiveColor : tabInactiveColor;
    }

    void RefreshAll()
    {
        RefreshEquipSlots();
        RefreshInventory();
    }

    void RefreshEquipSlots()
    {
        var inv = EquipmentInventory.Shared;
        for (int i = 0; i < equipSlots.Length; i++)
        {
            var slot = equipSlots[i];
            if (slot == null) continue;
            var data = inv != null && i < inv.Equipped.Count ? inv.Equipped[i] : null;
            slot.Bind(data, OnEquipSlotClicked);
        }
    }

    void RefreshInventory()
    {
        if (inventoryContent == null || slotPrefab == null) return;

        foreach (Transform child in inventoryContent)
            Destroy(child.gameObject);

        var inv = EquipmentInventory.Shared;
        if (inv == null) return;

        foreach (var data in inv.GetSortedBag(_sortMode))
        {
            var slot = Instantiate(slotPrefab, inventoryContent);
            slot.Bind(data, OnBagSlotClicked);
        }
    }

    void OnBagSlotClicked(EquipmentSlotView slot)
    {
        detailPopup?.ShowForBagItem(slot.Data);
    }

    void OnEquipSlotClicked(EquipmentSlotView slot)
    {
        if (slot.Data == null) return;
        detailPopup?.ShowForEquippedItem(slot.Data);
    }
}
