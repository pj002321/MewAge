using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 챕터 시너지 4개를 한 번에 보여주는 패널.
/// Inspector 연결:
///   panelRoot   — 패널 루트 GameObject
///   slots[0~3]  — WeaponSynergySlot 컴포넌트 4개
///   closeButton — 닫기 버튼 (선택)
/// </summary>
public class SynergyInfoPanel : MonoBehaviourSingleton<SynergyInfoPanel>
{
    [Header("패널 루트")]
    [SerializeField] private GameObject panelRoot;

    [Header("시너지 슬롯 (4개)")]
    [SerializeField] private WeaponSynergySlot[] slots = new WeaponSynergySlot[4];

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

        var synergies = SynergyHelper.GetChapterSynergies(chapter);

        for (int i = 0; i < slots.Length; i++)
        {
            var slot = slots[i];
            if (slot == null) continue;

            if (i < synergies.Count)
            {
                var entry = synergies[i];
                int count = SynergyHelper.CountEquippedTag(entry.Tag);
                slot.gameObject.SetActive(true);
                slot.Bind(entry, count);
            }
            else
            {
                slot.gameObject.SetActive(false);
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
