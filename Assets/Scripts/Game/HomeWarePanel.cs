using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mewage.DB;

enum WareSortMode { None, ByGrade, ByEra, ByRole }

/// <summary>
/// 홈 기물 패널.
///
/// [프리셋 탭]
///   상단: 프리셋 슬롯 4개
///   하단: 정렬 탭 + 보유 기물 인벤토리 그리드
///
/// [합성 탭]
///   상단: 재료 슬롯 10개 (클릭으로 채우기/비우기) + 합성 버튼
///   하단: 보유 기물 인벤토리 그리드 (클릭 시 재료 슬롯에 추가)
///
/// Inspector 연결:
///   presetArea       — 프리셋 슬롯 4개를 감싸는 부모 GameObject
///   presetSlots[4]   — 프리셋 WareSlotView 4개
///
///   synthArea            — 합성 슬롯 영역 부모 GameObject
///   synthSlotsParent     — WareSlotView 10개를 생성할 Transform
///   synthConfirmButton   — 합성 실행 버튼 (3장 이상 시 활성화)
///   synthConfirmLabel    — "합성 (N장 → M개)" 레이블
///   synthPreviewText     — 합성 결과 미리보기 텍스트
///   synthEraFilterText   — 현재 선택된 시대 표시
///
///   modeToggleButton — 프리셋 ↔ 합성 전환 버튼
///   modeToggleLabel  — 버튼 레이블 ("합성 탭" / "프리셋 탭")
///
///   tabAll / tabByGrade / tabByEra / tabByRole — 인벤 정렬 탭
///   inventoryContent — 인벤 그리드 Content
///   slotPrefab       — WareSlotView 프리팹
///   detailPopup      — WareDetailPopup
/// </summary>
public class HomeWarePanel : MonoBehaviour
{
    // ── 프리셋 ────────────────────────────────────────────────────────
    [Header("프리셋 영역")]
    public GameObject    presetArea;
    public WareSlotView[] presetSlots = new WareSlotView[4];

    // ── 합성 ──────────────────────────────────────────────────────────
    [Header("합성 영역")]
    public GameObject    synthArea;
    public Transform     synthSlotsParent;
    public Button        synthConfirmButton;
    public TMP_Text      synthConfirmLabel;
    public TMP_Text      synthPreviewText;
    public TMP_Text      synthEraFilterText;

    // ── 공통 ──────────────────────────────────────────────────────────
    [Header("모드 전환")]
    public Button   modeToggleButton;
    public TMP_Text modeToggleLabel;

    [Header("인벤토리")]
    public Transform    inventoryContent;
    public WareSlotView slotPrefab;

    [Header("팝업")]
    public WareDetailPopup      detailPopup;
    public WareSynthResultPopup synthResultPopup;

    [Header("정렬 탭 버튼")]
    public Button tabAll;
    public Button tabByGrade;
    public Button tabByEra;
    public Button tabByRole;

    [Header("탭 색상")]
    public Color tabActiveColor   = new Color(1f,    0.80f, 0.20f, 1f);
    public Color tabInactiveColor = new Color(0.25f, 0.25f, 0.25f, 1f);

    // ── 내부 상태 ─────────────────────────────────────────────────────

    WareSortMode _sortMode  = WareSortMode.None;
    bool         _synthMode = false;

    /// <summary>합성 재료로 선택된 카드 ID 목록 (중복 허용, 최대 10)</summary>
    readonly List<string> _synthMaterials = new();

    /// <summary>합성 슬롯 뷰 (런타임 생성)</summary>
    readonly List<WareSlotView> _synthSlotViews = new();

    const int MaxSynthSlots = 10;

    // ── 초기화 ────────────────────────────────────────────────────────

    void Awake()
    {
        if (StructureWareManager.Shared == null)
            MonoBehaviourSingleton<StructureWareManager>.Create("StructureWareManager");

        modeToggleButton?.onClick.AddListener(ToggleMode);
        synthConfirmButton?.onClick.AddListener(OnSynthConfirm);

        tabAll?.onClick.AddListener(()     => SetSort(WareSortMode.None));
        tabByGrade?.onClick.AddListener(() => SetSort(WareSortMode.ByGrade));
        tabByEra?.onClick.AddListener(()   => SetSort(WareSortMode.ByEra));
        tabByRole?.onClick.AddListener(()  => SetSort(WareSortMode.ByRole));

        BuildSynthSlots();
    }

    void Start() => SetSort(WareSortMode.None);

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

    // ── 모드 전환 ─────────────────────────────────────────────────────

    void ToggleMode()
    {
        _synthMode = !_synthMode;
        _synthMaterials.Clear();

        if (presetArea) presetArea.SetActive(!_synthMode);
        if (synthArea)  synthArea.SetActive(_synthMode);

        if (modeToggleLabel)
            modeToggleLabel.text = _synthMode ? "프리셋 탭" : "합성 탭";

        RefreshSynthSlots();
        RefreshSynthUI();
        RefreshInventory();
    }

    // ── 전체 갱신 ─────────────────────────────────────────────────────

    void RefreshAll()
    {
        if (_synthMode) { RefreshSynthSlots(); RefreshSynthUI(); }
        else             RefreshPreset();
        RefreshInventory();
    }

    // ── 프리셋 ────────────────────────────────────────────────────────

    void RefreshPreset()
    {
        var mgr = StructureWareManager.Shared;
        for (int i = 0; i < presetSlots.Length; i++)
        {
            if (presetSlots[i] == null) continue;
            presetSlots[i].PresetSlotIndex = i;
            string id = mgr?.GetPreset(i);
            if (!string.IsNullOrEmpty(id))
                presetSlots[i].Bind(id, OnPresetSlotClicked);
            else
                presetSlots[i].BindEmpty(i);
        }
    }

    void OnPresetSlotClicked(string id) => detailPopup?.Show(id);

    // ── 인벤토리 ─────────────────────────────────────────────────────

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

    void RefreshInventory()
    {
        if (inventoryContent == null || slotPrefab == null) return;

        foreach (Transform child in inventoryContent)
            Destroy(child.gameObject);

        var mgr = StructureWareManager.Shared;
        if (mgr == null) return;

        foreach (var id in GetSortedIds(_sortMode))
        {
            // 합성 모드: 재료로 사용 중인 카드는 남은 수량만 표시
            int count = mgr.GetCount(id);
            int usedInMat = _synthMaterials.Count(m => m == id);
            int remaining = count - usedInMat;
            if (remaining <= 0 && _synthMode) continue; // 재료로 모두 사용된 카드 숨김

            var slot = Instantiate(slotPrefab, inventoryContent);
            slot.PresetSlotIndex = -1;
            slot.Bind(id, OnInventorySlotClicked, countOverride: remaining > 0 ? remaining : count);
        }
    }

    void OnInventorySlotClicked(string id)
    {
        if (_synthMode)
        {
            TryAddSynthMaterial(id);
        }
        else
        {
            detailPopup?.Show(id);
        }
    }

    IEnumerable<string> GetSortedIds(WareSortMode mode)
    {
        var mgr = StructureWareManager.Shared;
        if (mgr == null) return System.Linq.Enumerable.Empty<string>();

        var ids = mgr.GetOwnedIds();
        return mode switch
        {
            WareSortMode.ByGrade => ids.OrderByDescending(id => GetGradeOrder(id)),
            WareSortMode.ByEra   => ids.OrderByDescending(id => GetEraOrder(id)),
            WareSortMode.ByRole  => ids.OrderBy(id => GetRoleOrder(id)),
            _                    => ids,
        };
    }

    int GetGradeOrder(string id)
    {
        var data = DB_기물.FindEntity(r => r.f_기물ID == id);
        return data?.f_등급 switch { "S" => 6, "LEGEND" => 5, "EPIC" => 4, "ELITE" => 3, "RARE" => 2, _ => 1 };
    }

    int GetEraOrder(string id)
    {
        return StructureWareManager.EraOf(id) switch
        {
            "C7" => 7, "C6" => 6, "C5" => 5, "C4" => 4,
            "C3" => 3, "C2" => 2, _ => 1,
        };
    }

    int GetRoleOrder(string id)
    {
        var data = DB_기물.FindEntity(r => r.f_기물ID == id);
        return data?.f_역할 switch { "CC" => 1, "버스트" => 2, "지속" => 3, "버프" => 4, _ => 5 };
    }

    // ── 합성 슬롯 생성 ────────────────────────────────────────────────

    void BuildSynthSlots()
    {
        if (synthSlotsParent == null || slotPrefab == null) return;

        for (int i = 0; i < MaxSynthSlots; i++)
        {
            var slot = Instantiate(slotPrefab, synthSlotsParent);
            slot.PresetSlotIndex = -1;
            int slotIdx = i;
            // 빈 슬롯 클릭 시 아무 동작 없음, 채워진 슬롯 클릭 시 재료 제거
            slot.Bind(null, _ => OnSynthSlotClicked(slotIdx));
            slot.BindEmpty(-1);
            _synthSlotViews.Add(slot);
        }
    }

    // ── 합성 재료 로직 ────────────────────────────────────────────────

    void TryAddSynthMaterial(string id)
    {
        if (_synthMaterials.Count >= MaxSynthSlots) return;

        var mgr = StructureWareManager.Shared;
        if (mgr == null) return;

        // 이미 같은 카드를 재료로 사용 중인 수 확인
        int alreadyUsed = _synthMaterials.Count(m => m == id);
        if (alreadyUsed >= mgr.GetCount(id)) return; // 보유량 초과 방지

        // 시대 필터: 첫 카드가 기준이 됨
        if (_synthMaterials.Count > 0)
        {
            string filterEra = StructureWareManager.EraOf(_synthMaterials[0]);
            string clickedEra = StructureWareManager.EraOf(id);
            if (clickedEra != filterEra)
            {
                // 다른 시대 카드는 추가 불가 → 시각 피드백 (텍스트 갱신만)
                if (synthEraFilterText)
                    synthEraFilterText.text =
                        $"{StructureWareManager.EraName(filterEra)} 카드만 추가 가능";
                return;
            }
        }

        _synthMaterials.Add(id);
        RefreshSynthSlots();
        RefreshSynthUI();
        RefreshInventory();
    }

    void OnSynthSlotClicked(int slotIdx)
    {
        if (slotIdx >= _synthMaterials.Count) return;
        _synthMaterials.RemoveAt(slotIdx);
        RefreshSynthSlots();
        RefreshSynthUI();
        RefreshInventory();
    }

    // ── 합성 슬롯 표시 갱신 ──────────────────────────────────────────

    void RefreshSynthSlots()
    {
        for (int i = 0; i < _synthSlotViews.Count; i++)
        {
            if (i < _synthMaterials.Count)
            {
                int slotIdx = i;
                _synthSlotViews[i].Bind(_synthMaterials[i],
                    _ => OnSynthSlotClicked(slotIdx), countOverride: 1);
            }
            else
            {
                _synthSlotViews[i].BindEmpty(-1);
            }
        }
    }

    // ── 합성 UI 갱신 (버튼/미리보기) ─────────────────────────────────

    void RefreshSynthUI()
    {
        int n = _synthMaterials.Count;
        bool canSynth = n >= 3;

        if (synthConfirmButton) synthConfirmButton.interactable = canSynth;

        // 합성 결과 미리보기
        if (synthConfirmLabel || synthPreviewText)
        {
            int outputCount = n >= 10 ? 5 : n >= 5 ? 2 : 1;
            string label, preview;

            if (n == 0)
            {
                label   = "합성";
                preview = "카드를 3~10장 선택하세요\n(같은 시대 카드만 가능)";
            }
            else
            {
                string era     = StructureWareManager.EraOf(_synthMaterials[0]);
                string eraName = StructureWareManager.EraName(era);
                string nextEra = StructureWareManager.NextEra(era);
                string nextName = era == nextEra
                    ? $"같은 시대({eraName})"
                    : StructureWareManager.EraName(nextEra);

                label   = canSynth ? $"합성  ({n}장 → {nextName} {outputCount}개)" : $"합성 ({n}/3장)";
                preview = canSynth
                    ? $"{eraName} 카드 {n}장 → {nextName} 카드 {outputCount}개"
                    : $"{eraName} 카드 {n}/3장 — {3 - n}장 더 필요";
            }

            if (synthConfirmLabel) synthConfirmLabel.text = label;
            if (synthPreviewText)  synthPreviewText.text  = preview;
        }

        // 시대 필터 표시
        if (synthEraFilterText)
        {
            if (n == 0)
                synthEraFilterText.text = "시대 제한 없음";
            else
            {
                string era = StructureWareManager.EraOf(_synthMaterials[0]);
                synthEraFilterText.text = $"{StructureWareManager.EraName(era)} 카드 선택 중";
            }
        }
    }

    // ── 합성 실행 ────────────────────────────────────────────────────

    void OnSynthConfirm()
    {
        if (_synthMaterials.Count < 3) return;

        var results = StructureWareManager.Shared?.TrySynthesize(new List<string>(_synthMaterials));
        if (results == null) return;

        _synthMaterials.Clear();
        RefreshSynthSlots();
        RefreshSynthUI();
        RefreshInventory();

        synthResultPopup?.Show(results);
    }
}
