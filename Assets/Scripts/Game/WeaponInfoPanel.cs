using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 무기 정보 표시 전용 패널 (정보만, 액션 버튼 없음).
/// InvenSlot / EquipSlot / UpgradeCard의 정보 버튼이 Show()를 호출합니다.
///
/// Inspector 연결:
///   panelRoot   — 패널 루트 GameObject
///   iconImage   — 무기 아이콘 Image
///   starText    — 별 개수 TMP  ("★★★")
///   nameText    — 무기 이름 TMP
///   attackText  — 공격력 TMP
///   speedText   — 공격속도 TMP
///   rangeText   — 사정거리 TMP
///   critText    — 크리티컬 지수 TMP
///   dotText     — 도트 데미지 TMP
///   closeButton — 닫기 버튼 (옵션)
/// </summary>
public class WeaponInfoPanel : MonoBehaviourSingleton<WeaponInfoPanel>
{
    [Header("패널 루트")]
    [SerializeField] private GameObject panelRoot;

    [Header("무기 정보 UI")]
    [SerializeField] private Image           frameImage;
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI starText;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI rangeText;
    [SerializeField] private TextMeshProUGUI critText;
    [SerializeField] private TextMeshProUGUI dotText;

    [Header("시너지 태그 아이콘")]
    [SerializeField] private Image synergyIcon1;
    [SerializeField] private Image synergyIcon2;

    [Header("닫기")]
    [SerializeField] private Button closeButton;

    protected override bool DontDestroy => false;

    protected override void Awake()
    {
        base.Awake();
        closeButton?.onClick.AddListener(Hide);
        if (panelRoot) panelRoot.SetActive(false);
    }

    public void Show(WeaponData data)
    {
        if (data == null) return;

        if (frameImage) frameImage.color = WeaponGradeColors.Get(data.grade);

        if (iconImage)
        {
            iconImage.sprite  = data.icon;
            iconImage.enabled = data.icon != null;
        }

        if (starText)   starText.text   = new string('★', data.star);
        if (nameText)   nameText.text   = data.weaponName;
        if (attackText) attackText.text = $"{data.attackDamage:0.#}";

        if (speedText)
        {
            float aps = data.attackCooldown > 0f ? 1f / data.attackCooldown : 0f;
            speedText.text = $"{aps:0.##}/s";
        }

        if (rangeText) rangeText.text = $"{data.attackRange:0.#}";
        if (critText)  critText.text  = data.criticalRate > 0f ? $"{data.criticalRate:0.#}x" : "-";
        if (dotText)   dotText.text   = data.dotDamage    > 0f ? $"{data.dotDamage:0.#}"    : "-";

        // 시너지 태그 아이콘
        int chapter = UpgradeManager.Shared?.CurrentChapter ?? 1;
        SetSynergyIcon(synergyIcon1, data.tag1, chapter);
        SetSynergyIcon(synergyIcon2, data.tag2, chapter);

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    private static void SetSynergyIcon(Image img, WeaponSynergyTag tag, int chapter)
    {
        if (img == null) return;
        var sprite = SynergyHelper.GetIcon(tag, chapter);
        img.sprite  = sprite;
        img.enabled = sprite != null && tag != WeaponSynergyTag.None;
    }
}
