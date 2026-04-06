using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class WeaponSynergySlot : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI synergyfieldText;
    [SerializeField] private Image           synergyIconImg;
    [SerializeField] private TextMeshProUGUI synergyNameText;
    [SerializeField] private Image[]         referenceWeaponIconImg;

    /// <summary>시너지 정보와 해당 시너지에 기여하는 장착 무기 아이콘들을 바인딩합니다.</summary>
    public void Bind(SynergyHelper.SynergyEntry entry, int equippedCount)
    {
        // 아이콘
        if (synergyIconImg != null)
        {
            synergyIconImg.sprite  = entry.Icon;
            synergyIconImg.enabled = entry.Icon != null;
            synergyIconImg.color   = SynergyHelper.GetTierColor(equippedCount);
        }

        // 이름
        if (synergyNameText != null)
            synergyNameText.text = entry.Name;

        // 설명
        if (synergyfieldText != null)
            synergyfieldText.text = entry.Description;

        // 참조 무기 아이콘: 시너지 태그와 일치하는 장착 무기 스프라이트 수집
        var icons = SynergyHelper.GetEquippedIconsForTag(entry.Tag);

        if (referenceWeaponIconImg == null) return;

        for (int i = 0; i < referenceWeaponIconImg.Length; i++)
        {
            var img = referenceWeaponIconImg[i];
            if (img == null) continue;

            bool hasIcon = i < icons.Count && icons[i] != null;

            // 이미지와 부모 오브젝트 활성화 여부
            img.transform.parent.gameObject.SetActive(hasIcon);
            img.sprite  = hasIcon ? icons[i] : null;
            img.enabled = hasIcon;
        }
    }
}
