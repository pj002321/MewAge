using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using Mewage.DB;

enum WareSortMode { None, ByGrade, ByEra, ByRole }

/// <summary>
/// 홈 기물 패널.
/// 상단: 프리셋 슬롯 4개 (WareSlotView)
/// 하단: 탭(전체 / 등급순 / 시대순 / 특성순) + 보유 기물 스크롤 목록
///
/// Inspector 연결:
///   presetSlots[4]   — 장착 슬롯 WareSlotView 4개
///   tabAll / tabByGrade / tabByEra / tabByRole — 탭 버튼
///   inventoryContent — 인벤 그리드 Content
///   slotPrefab       — WareSlotView 프리팹
///   detailPopup      — WareDetailPopup
/// </summary>
public class HomeWarePanel : MonoBehaviour
{
    [Header("프리셋 슬롯 (0~3)")]
    public WareSlotView[] presetSlots = new WareSlotView[4];

    [Header("인벤토리")]
    public Transform    inventoryContent;
    public WareSlotView slotPrefab;

    [Header("팝업")]
    public WareDetailPopup detailPopup;

    [Header("탭 버튼")]
    public Button tabAll;
    public Button tabByGrade;
    public Button tabByEra;
    public Button tabByRole;

    [Header("탭 색상")]
    public Color tabActiveColor   = new Color(1f,    0.80f, 0.20f, 1f);
    public Color tabInactiveColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    WareSortMode _sortMode = WareSortMode.None;

    void Awake()
    {
        if (StructureWareManager.Shared == null)
            MonoBehaviourSingleton<StructureWareManager>.Create("StructureWareManager");
    }

    void Start()
    {
        tabAll?.onClick.AddListener(()     => SetSort(WareSortMode.None));
        tabByGrade?.onClick.AddListener(() => SetSort(WareSortMode.ByGrade));
        tabByEra?.onClick.AddListener(()   => SetSort(WareSortMode.ByEra));
        tabByRole?.onClick.AddListener(()  => SetSort(WareSortMode.ByRole));
        SetSort(WareSortMode.None);
    }

    void SetSort(WareSortMode mode)
    {
        _sortMode = mode;
        SetTabColor(tabAll,     mode == WareSortMode.None);
        SetTabColor(tabByGrade, mode == WareSortMode.ByGrade);
        SetTabColor(tabByEra,   mode == WareSortMode.ByEra);
        SetTabColor(tabByRole,  mode == WareSortMode.ByRole);
        RefreshInventory();
    }

    void SetTabColor(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? tabActiveColor : tabInactiveColor;
    }

    void OnEnable()
    {
        RefreshAll();
        var mgr = StructureWareManager.Shared;
        if (mgr != null) mgr.OnChanged += RefreshAll;
    }

    void OnDisable()
    {
        var mgr = StructureWareManager.Shared;
        if (mgr != null) mgr.OnChanged -= RefreshAll;
    }

    void RefreshAll()
    {
        RefreshPreset();
        RefreshInventory();
    }

    void RefreshPreset()
    {
        var mgr = StructureWareManager.Shared;
        for (int i = 0; i < presetSlots.Length; i++)
        {
            if (presetSlots[i] == null) continue;
            presetSlots[i].PresetSlotIndex = i; // 드래그 앤 드롭용 슬롯 인덱스 설정
            string id = mgr?.GetPreset(i);
            if (!string.IsNullOrEmpty(id))
                presetSlots[i].Bind(id, OnPresetSlotClicked);
            else
                presetSlots[i].BindEmpty(i);
        }
    }

    void RefreshInventory()
    {
        if (inventoryContent == null || slotPrefab == null) return;

        foreach (Transform child in inventoryContent)
            Destroy(child.gameObject);

        var mgr = StructureWareManager.Shared;
        if (mgr == null) return;

        foreach (var id in GetSortedIds(_sortMode))
        {
            var slot = Instantiate(slotPrefab, inventoryContent);
            slot.PresetSlotIndex = -1; // 인벤토리 슬롯은 드롭 대상 아님
            slot.Bind(id, OnBagSlotClicked);
        }
    }

    IEnumerable<string> GetSortedIds(WareSortMode mode)
    {
        var mgr = StructureWareManager.Shared;
        if (mgr == null) return System.Linq.Enumerable.Empty<string>();

        var ids = mgr.GetOwnedIds();
        switch (mode)
        {
            case WareSortMode.ByGrade:
                return ids.OrderByDescending(id => GetGradeOrder(id));
            case WareSortMode.ByEra:
                return ids.OrderByDescending(id => GetEraOrder(id));
            case WareSortMode.ByRole:
                return ids.OrderBy(id => GetRoleOrder(id));
            default:
                return ids;
        }
    }

    int GetGradeOrder(string id)
    {
        var data = Mewage.DB.DB_기물.FindEntity(r => r.f_기물ID == id);
        if (data == null) return 0;
        return data.f_등급 switch
        {
            "LEGEND" => 5,
            "EPIC"   => 4,
            "ELITE"  => 3,
            "RARE"   => 2,
            _        => 1, // NORMAL
        };
    }

    int GetEraOrder(string id)
    {
        string era = StructureWareManager.EraOf(id);
        return era switch
        {
            "C7" => 7,
            "C6" => 6,
            "C5" => 5,
            "C4" => 4,
            "C3" => 3,
            "C2" => 2,
            _    => 1, // C1
        };
    }

    int GetRoleOrder(string id)
    {
        var data = Mewage.DB.DB_기물.FindEntity(r => r.f_기물ID == id);
        if (data == null) return 0;
        return data.f_역할 switch
        {
            "CC"     => 1,
            "버스트" => 2,
            "지속"   => 3,
            "버프"   => 4,
            _        => 5,
        };
    }

    void OnBagSlotClicked(string id) => detailPopup?.Show(id);

    void OnPresetSlotClicked(string id) => detailPopup?.Show(id);
}
