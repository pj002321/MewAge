using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 무기 정보 표시 전용 패널 (정보만, 액션 버튼 없음).
/// InvenSlot / EquipSlot / UpgradeCard의 정보 버튼이 Show()를 호출합니다.
///
/// Inspector 연결:
///   panelRoot        — 패널 루트 GameObject
///   bgButton         — 빈 배경 Button (누르면 Hide)
///   frameImage       — 무기 프레임 Image
///   iconImage        — 무기 아이콘 Image
///   starText         — 별 개수 TMP  ("★★★")
///   gradeText        — 등급 TMP  ("레어")
///   nameText         — 무기 이름 TMP
///
///   스탯 (메인값 / 서브값):
///     attackText / attackSubText   — 공격력 / 강화된 공격력 델타
///     speedText  / speedSubText    — 공격속도 (회/s, 1/쿨다운)
///     rangeText  / rangeSubText    — 사거리
///     critText       / critSubText      — 크리티컬
///     bleedText      / bleedSubText     — 출혈 데미지
///     lifeStealText  / lifeStealSubText — 흡혈
///
///   성 강화 설명:
///     star2Desc / star3Desc / star4Desc — 2/3/4성 보너스 설명 TMP
///
///   storyText        — 무기 스토리 설명 TMP
///   synergyIcon1/2   — 시너지 태그 아이콘 Image
///   closeButton      — 닫기 버튼 (옵션)
/// </summary>
public class WeaponInfoPanel : MonoBehaviourSingleton<WeaponInfoPanel>
{
    [Header("패널 루트")]
    [SerializeField] private GameObject panelRoot;

    [Header("배경 클릭으로 닫기")]
    [SerializeField] private Button bgButton;

    [Header("무기 기본 정보")]
    [SerializeField] private Image           frameImage;
    [SerializeField] private Image           iconImage;
    [SerializeField] private TextMeshProUGUI starText;
    [SerializeField] private TextMeshProUGUI gradeText;   // 예: "레어"
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("스탯 — 메인값")]
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI speedText;   // 공격속도 (회/s)
    [SerializeField] private TextMeshProUGUI rangeText;
    [SerializeField] private TextMeshProUGUI critText;
    [SerializeField] private TextMeshProUGUI bleedText;
    [SerializeField] private TextMeshProUGUI lifeStealText;

    [Header("스탯 — 서브값 (성 강화 델타)")]
    [SerializeField] private TextMeshProUGUI attackSubText;
    [SerializeField] private TextMeshProUGUI speedSubText;
    [SerializeField] private TextMeshProUGUI rangeSubText;
    [SerializeField] private TextMeshProUGUI critSubText;
    [SerializeField] private TextMeshProUGUI bleedSubText;
    [SerializeField] private TextMeshProUGUI lifeStealSubText;

    [Header("성 강화 보너스 설명 (2 / 3 / 4성)")]
    [SerializeField] private TextMeshProUGUI star2Desc;
    [SerializeField] private TextMeshProUGUI star3Desc;
    [SerializeField] private TextMeshProUGUI star4Desc;

    [Header("스토리 설명")]
    [SerializeField] private TextMeshProUGUI storyText;

    [Header("시너지 태그 아이콘")]
    [SerializeField] private Image synergyIcon1;
    [SerializeField] private Image synergyIcon2;

    [Header("닫기 버튼")]
    [SerializeField] private Button closeButton;

    protected override bool DontDestroy => false;

    protected override void Awake()
    {
        base.Awake();
        bgButton?.onClick.AddListener(Hide);
        closeButton?.onClick.AddListener(Hide);
        if (panelRoot) panelRoot.SetActive(false);
    }

    public void Show(WeaponData data)
    {
        if (data == null) return;

        // ── 프레임 / 아이콘 ──────────────────────────────────────
        if (frameImage) frameImage.color = WeaponGradeColors.Get(data.grade);

        if (iconImage)
        {
            iconImage.sprite  = data.icon;
            iconImage.enabled = data.icon != null;
        }

        // ── 이름 / 별 / 등급 ─────────────────────────────────────
        if (starText)  starText.text  = new string('★', data.star);
        if (nameText)  nameText.text  = data.weaponName;
        if (gradeText)
        {
            gradeText.text  = GradeName(data.grade);
            gradeText.color = WeaponGradeColors.Get(data.grade);
        }

        // ── 스탯 메인값 ───────────────────────────────────────────
        if (attackText) attackText.text = $"{data.attackDamage:0.#}";

        if (speedText)
        {
            float aps = data.attackCooldown > 0f ? 1f / data.attackCooldown : 0f;
            speedText.text = $"{aps:0.##}/s";
        }

        if (rangeText) rangeText.text = $"{data.attackRange:0.#}";
        if (critText)      critText.text      = data.criticalRate > 0f ? $"{data.criticalRate:0.#}x" : "-";
        if (bleedText)     bleedText.text     = data.bleedDamage  > 0f ? $"{data.bleedDamage:0.#}"  : "-";
        if (lifeStealText) lifeStealText.text = data.lifeSteal    > 0f ? $"{data.lifeSteal * 100f:0.#}%" : "-";

        // ── 스탯 서브값 (기본 스탯 대비 강화된 양) ────────────────
        SetSubText(attackSubText,    data.attackDamage,  data.baseAttackDamage);
        SetSubText(rangeSubText,     data.attackRange,   data.baseAttackRange);
        SetSubText(critSubText,      data.criticalRate,  data.baseCriticalRate, isAbsolute: true);
        SetSubText(bleedSubText,     data.bleedDamage,   data.baseBleedDamage);
        SetSubText(lifeStealSubText, data.lifeSteal,     data.baseLifeSteal,    isAbsolute: true);
        SetSpeedSubText(speedSubText, data);

        // ── 성별 강화 설명 ────────────────────────────────────────
        SetStarDesc(star2Desc, data.bonus2Star, 2, data.star);
        SetStarDesc(star3Desc, data.bonus3Star, 3, data.star);
        SetStarDesc(star4Desc, data.bonus4Star, 4, data.star);

        // ── 스토리 설명 ───────────────────────────────────────────
        if (storyText) storyText.text = data.storyDescription ?? string.Empty;

        // ── 시너지 태그 아이콘 ────────────────────────────────────
        int chapter = UpgradeManager.Shared?.CurrentChapter ?? 1;
        SetSynergyIcon(synergyIcon1, data.tag1, chapter);
        SetSynergyIcon(synergyIcon2, data.tag2, chapter);

        if (panelRoot) panelRoot.SetActive(true);
    }

    public void Hide()
    {
        if (panelRoot) panelRoot.SetActive(false);
    }

    // ────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ────────────────────────────────────────────────────────

    /// <summary>서브텍스트에 기본값 대비 델타를 표시합니다. 차이가 없으면 숨깁니다.</summary>
    private static void SetSubText(TextMeshProUGUI label, float current, float baseVal, bool isAbsolute = false)
    {
        if (label == null) return;
        float delta = current - baseVal;
        if (Mathf.Approximately(delta, 0f)) { label.text = string.Empty; return; }

        if (isAbsolute)
            label.text = delta > 0f ? $"<color=#4FC3F7>+{delta:0.##}</color>" : $"<color=#EF9A9A>{delta:0.##}</color>";
        else
            label.text = delta > 0f ? $"<color=#4FC3F7>+{delta:0.#}</color>" : $"<color=#EF9A9A>{delta:0.#}</color>";
    }

    /// <summary>공격속도(aps) 서브값 — 쿨다운이 줄면 aps가 오르므로 양수가 좋음.</summary>
    private static void SetSpeedSubText(TextMeshProUGUI speedSub, WeaponData data)
    {
        if (speedSub == null) return;
        float baseAps    = data.baseAttackCooldown > 0f ? 1f / data.baseAttackCooldown : 0f;
        float currentAps = data.attackCooldown     > 0f ? 1f / data.attackCooldown     : 0f;
        float apsDelta   = currentAps - baseAps;

        if (Mathf.Approximately(apsDelta, 0f)) { speedSub.text = string.Empty; return; }
        speedSub.text = apsDelta > 0f
            ? $"<color=#4FC3F7>+{apsDelta:0.##}</color>"
            : $"<color=#EF9A9A>{apsDelta:0.##}</color>";
    }

    /// <summary>성 강화 설명 텍스트를 설정합니다. 현재 성 이상이면 활성(밝게), 미달이면 흐리게 표시합니다.</summary>
    private static void SetStarDesc(TextMeshProUGUI label, WeaponStarBonus bonus, int requiredStar, int currentStar)
    {
        if (label == null) return;

        if (bonus.IsEmpty)
        {
            label.text = string.Empty;
            return;
        }

        bool unlocked  = currentStar >= requiredStar;
        string prefix  = $"{new string('★', requiredStar)} ";
        string content = bonus.description;

        label.text  = unlocked
            ? $"<color=#FFD740>{prefix}</color>{content}"
            : $"<color=#888888>{prefix}{content}</color>";
    }

    private static void SetSynergyIcon(Image img, WeaponSynergyTag tag, int chapter)
    {
        if (img == null) return;
        var sprite  = SynergyHelper.GetIcon(tag, chapter);
        img.sprite  = sprite;
        img.enabled = sprite != null && tag != WeaponSynergyTag.None;
    }

    /// <summary>WeaponGrade → 한글 이름</summary>
    private static string GradeName(WeaponGrade grade) => grade switch
    {
        WeaponGrade.Normal  => "일반",
        WeaponGrade.Rare    => "레어",
        WeaponGrade.Elite   => "엘리트",
        WeaponGrade.Epic    => "에픽",
        WeaponGrade.Legend  => "신화",
        _                   => grade.ToString(),
    };
}
