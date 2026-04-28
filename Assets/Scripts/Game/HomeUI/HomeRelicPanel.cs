using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유물 수집 패널.
///
/// Inspector 연결:
///   contentsRoot      — ScrollRect.content Transform
///   rowGroupPrefab    — RelicRowGroup 컴포넌트가 붙은 행 묶음 프리팹
///   infoPopup         — RelicInfoPopup 참조
///   sortByGradeButton — 등급 순 정렬 탭 버튼
///   sortByStarButton  — 성급 순 정렬 탭 버튼
/// </summary>
public class HomeRelicPanel : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Transform      contentsRoot;
    [SerializeField] private RelicRowGroup  rowGroupPrefab;
    [SerializeField] private RelicInfoPopup infoPopup;

    [Header("정렬 탭")]
    [SerializeField] private Button sortByGradeButton;
    [SerializeField] private Button sortByStarButton;

    private enum SortMode { ByGrade, ByStar }
    private SortMode _sortMode = SortMode.ByGrade;

    private readonly List<RelicRowGroup> _groups = new();

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    private void Start()
    {
        sortByGradeButton?.onClick.AddListener(() => SetSort(SortMode.ByStar));
        sortByStarButton?.onClick.AddListener(()  => SetSort(SortMode.ByGrade));
        RebuildGroups();
    }

    private void OnEnable()
    {
        RelicInventory.OnChanged -= RebuildGroups;
        RelicInventory.OnChanged += RebuildGroups;
        RebuildGroups();
    }

    private void OnDisable()
    {
        RelicInventory.OnChanged -= RebuildGroups;
    }

    // ── 정렬 탭 ──────────────────────────────────────────────────────────
    private void SetSort(SortMode mode)
    {
        _sortMode = mode;
        UpdateTabVisuals();
        RebuildGroups();
    }

    private void UpdateTabVisuals()
    {
        bool isGrade = _sortMode == SortMode.ByGrade;
        sortByGradeButton?.gameObject.SetActive(isGrade);
        sortByStarButton?.gameObject.SetActive(!isGrade);
    }

    // ── 정렬된 목록 ──────────────────────────────────────────────────────
    private List<RelicData> GetSortedRelics()
    {
        var inv = RelicInventory.Shared;
        if (inv == null) return new List<RelicData>();

        var list = new List<RelicData>(inv.GetAll());

        if (_sortMode == SortMode.ByGrade)
            list.Sort((a, b) =>
                RelicVisualConfig.ParseGrade(b.grade).CompareTo(
                RelicVisualConfig.ParseGrade(a.grade)));
        else
            list.Sort((a, b) =>
                inv.GetStarLevel(b.relicId).CompareTo(
                inv.GetStarLevel(a.relicId)));

        return list;
    }

    // ── 그룹 빌드 ────────────────────────────────────────────────────────
    private void RebuildGroups()
    {
        foreach (Transform child in contentsRoot) Destroy(child.gameObject);
        _groups.Clear();

        var inv = RelicInventory.Shared;
        if (inv == null)
        {
            Debug.LogWarning("[HomeRelicPanel] RelicInventory.Shared 가 null — 씬에 RelicInventory GameObject가 있는지 확인하세요.");
            return;
        }

        var relics = GetSortedRelics();
        if (relics.Count == 0)
        {
            Debug.LogWarning("[HomeRelicPanel] RelicInventory.GetAll() 결과가 0 — DB_유물 데이터를 확인하세요.");
            return;
        }

        if (rowGroupPrefab == null)
        {
            Debug.LogError("[HomeRelicPanel] rowGroupPrefab 이 할당되지 않았습니다.");
            return;
        }

        int groupCount = Mathf.CeilToInt(relics.Count / 4f);

        var grid = contentsRoot.GetComponent<UnityEngine.UI.GridLayoutGroup>();
        if (grid != null) grid.constraintCount = groupCount;

        for (int g = 0; g < groupCount; g++)
        {
            var group = Instantiate(rowGroupPrefab, contentsRoot);
            group.Populate(relics, g * 4, data => infoPopup?.Show(data));
            _groups.Add(group);
        }

        UpdateTabVisuals();
    }

    // ── 슬롯 비주얼만 새로고침 (재빌드 없이) ──────────────────────────────
    public void RefreshAll()
    {
        var relics = GetSortedRelics();
        for (int g = 0; g < _groups.Count; g++)
            _groups[g].Refresh(relics, g * 4);
    }
}
