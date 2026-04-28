using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 유물 수집 패널.
///
/// Inspector 연결:
///   contentsRoot   — ScrollRect.content Transform
///   rowGroupPrefab — RelicRowGroup 컴포넌트가 붙은 행 묶음 프리팹
///   infoPopup      — RelicInfoPopup 참조
/// </summary>
public class HomeRelicPanel : MonoBehaviour
{
    [Header("UI 연결")]
    [SerializeField] private Transform      contentsRoot;
    [SerializeField] private RelicRowGroup  rowGroupPrefab;
    [SerializeField] private RelicInfoPopup infoPopup;

    private readonly List<RelicRowGroup> _groups = new();

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    private void Start()
    {
        BuildGroups();
    }

    private void OnEnable()
    {
        RelicInventory.OnChanged -= RefreshAll;
        RelicInventory.OnChanged += RefreshAll;
        RefreshAll();
    }

    private void OnDisable()
    {
        RelicInventory.OnChanged -= RefreshAll;
    }

    // ── 그룹 빌드 ────────────────────────────────────────────────────────
    private void BuildGroups()
    {
        foreach (Transform child in contentsRoot) Destroy(child.gameObject);
        _groups.Clear();

        var inv = RelicInventory.Shared;
        if (inv == null)
        {
            Debug.LogWarning("[HomeRelicPanel] RelicInventory.Shared 가 null — 씬에 RelicInventory GameObject가 있는지 확인하세요.");
            return;
        }

        var relics = inv.GetAll();
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

        if (contentsRoot == null)
        {
            Debug.LogError("[HomeRelicPanel] contentsRoot 가 할당되지 않았습니다.");
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
    }

    // ── 전체 새로고침 ─────────────────────────────────────────────────────
    public void RefreshAll()
    {
        var inv = RelicInventory.Shared;
        if (inv == null) return;
        var relics = inv.GetAll();
        for (int g = 0; g < _groups.Count; g++)
            _groups[g].Refresh(relics, g * 4);
    }


}
