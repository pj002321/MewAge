using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 챕터 시너지 4개를 한 번에 보여주는 패널.
/// UpgradePanel의 시너지 슬롯 버튼 어느 것을 눌러도 Show()를 호출합니다.
///
/// Inspector 연결:
///   panelRoot  — 패널 루트 GameObject
///   rows[0~3]  — 각 시너지 행 (SynergyRow) 4개
///     ├ icon      — 아이콘 Image
///     ├ nameText  — "태그이름" TMP
///     └ descText  — "설명" TMP
///   closeButton — 닫기 버튼 (선택)
/// </summary>
public class SynergyInfoPanel : MonoBehaviourSingleton<SynergyInfoPanel>
{
    [System.Serializable]
    public class SynergyRow
    {
        public Image           icon;
        public TextMeshProUGUI nameText;
        public TextMeshProUGUI descText;
    }

    [Header("패널 루트")]
    [SerializeField] private GameObject panelRoot;

    [Header("시너지 행 (4개)")]
    [SerializeField] private SynergyRow[] rows = new SynergyRow[4];

    [Header("닫기")]
    [SerializeField] private Button closeButton;

    protected override bool DontDestroy => false;

    private int _currentChapter = -1;

    protected override void Awake()
    {
        base.Awake();
        closeButton?.onClick.AddListener(Hide);
        if (panelRoot) panelRoot.SetActive(false);
    }

    /// <summary>해당 챕터의 시너지 4개를 모두 표시합니다.</summary>
    public void Show(int chapter)
    {
        _currentChapter = chapter;
        Debug.Log($"[SynergyInfoPanel] Show chapter={chapter}, panelRoot={panelRoot}");

        var synergies = SynergyHelper.GetChapterSynergies(chapter);
        Debug.Log($"[SynergyInfoPanel] synergies count={synergies.Count}");

        for (int i = 0; i < rows.Length; i++)
        {
            var row = rows[i];
            if (row == null) continue;

            if (i < synergies.Count)
            {
                var entry = synergies[i];
                int count = SynergyHelper.CountEquippedTag(entry.Tag);
                var color = SynergyHelper.GetTierColor(count);

                if (row.icon)
                {
                    row.icon.sprite  = entry.Icon;
                    row.icon.enabled = entry.Icon != null;
                    row.icon.color   = color;
                }

                if (row.nameText) row.nameText.text = entry.Name;
                if (row.descText) row.descText.text = entry.Description;
            }
            else
            {
                // 빈 행 숨김
                if (row.icon)     row.icon.enabled    = false;
                if (row.nameText) row.nameText.text   = string.Empty;
                if (row.descText) row.descText.text   = string.Empty;
            }
        }

        if (panelRoot) panelRoot.SetActive(true);
    }

    /// <summary>열려 있을 때 장착 수 변경을 반영합니다.</summary>
    public void Refresh()
    {
        if (panelRoot != null && panelRoot.activeSelf && _currentChapter >= 0)
            Show(_currentChapter);
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }
}
