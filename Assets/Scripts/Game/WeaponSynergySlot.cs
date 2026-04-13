using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSynergySlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI synergyfieldText;
    [SerializeField] private Image           synergyIconImg;
    [SerializeField] private TextMeshProUGUI synergyNameText;
    [SerializeField] private TextMeshProUGUI countText;          // "(보유/6)" 표기

    [Header("레퍼런스 무기 슬롯 프리팹")]
    [SerializeField] private GameObject synergyRefSlotPrefab;
    [SerializeField] private Transform  refSlotContainer;

    private readonly List<SynergyRefSlot> _refSlots = new();

    /// <summary>시너지 정보와 해당 시너지에 기여하는 장착 무기를 바인딩합니다.</summary>
    public void Bind(SynergyHelper.SynergyEntry entry, int equippedCount)
    {
        var cfg = WeaponVisualConfig.Shared;

        // 배너 아이콘 (UpgradePanel 슬롯과 동일한 이미지)
        if (synergyIconImg != null)
        {
            var banner = cfg?.GetSynergyBanner(entry.Tag);
            synergyIconImg.sprite  = banner;
            synergyIconImg.enabled = banner != null;
        }

        // 시너지 이름
        if (synergyNameText != null)
            synergyNameText.text = entry.Name;

        // 설명 — "레이블 +value단위" 형식, 수치는 현재 등급에 맞춰 녹색 표기
        if (synergyfieldText != null)
        {
            int activeValue = GetActiveValue(entry.StatValues, equippedCount);
            if (activeValue > 0)
            {
                string numStr = $"<color=#55FF88>+{activeValue}{entry.Unit}</color>";
                synergyfieldText.text = $"{entry.Label} {numStr}";
            }
            else
            {
                synergyfieldText.text = entry.Label;
            }
        }

        // 보유 개수 "(n/6)"
        if (countText != null)
            countText.text = $"({equippedCount}/6)";

        // 레퍼런스 무기 슬롯 갱신
        RefreshRefSlots(entry);
    }

    private void RefreshRefSlots(SynergyHelper.SynergyEntry entry)
    {
        if (synergyRefSlotPrefab == null || refSlotContainer == null) return;

        var equipped = SynergyHelper.GetEquippedWeaponsForTag(entry.Tag);
        var banner   = WeaponVisualConfig.Shared?.GetSynergyBanner(entry.Tag);

        // 슬롯 재사용 / 생성
        for (int i = 0; i < equipped.Count; i++)
        {
            SynergyRefSlot slot;
            if (i < _refSlots.Count)
            {
                slot = _refSlots[i];
                slot.gameObject.SetActive(true);
            }
            else
            {
                var go = Instantiate(synergyRefSlotPrefab, refSlotContainer);
                slot   = go.GetComponent<SynergyRefSlot>();
                if (slot == null) slot = go.AddComponent<SynergyRefSlot>();
                _refSlots.Add(slot);
            }
            slot.Bind(equipped[i], banner);
        }

        // 남은 슬롯 비활성화
        for (int i = equipped.Count; i < _refSlots.Count; i++)
            _refSlots[i].gameObject.SetActive(false);
    }

    /// <summary>장착 수에 따른 현재 활성 수치를 반환합니다. (동=index0, 은=index1, 금=index2)</summary>
    private static int GetActiveValue(List<int> values, int count)
    {
        if (values == null || values.Count == 0) return 0;
        if (count >= 5 && values.Count >= 3) return values[2];
        if (count >= 3 && values.Count >= 2) return values[1];
        if (count >= 1 && values.Count >= 1) return values[0];
        return 0;
    }
}
