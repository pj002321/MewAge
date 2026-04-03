using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 무기 정보 + 액션 패널.
/// Show(data, onEquip, onSell, onUnequip, onBuy) — null 콜백의 버튼은 자동으로 숨겨집니다.
///   InvenSlot  → onEquip + onSell
///   EquipSlot  → onUnequip + onSell
///   UpgradeCard → onBuy
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

    [Header("액션 버튼")]
    [SerializeField] private Button equipButton;
    [SerializeField] private Button sellButton;
    [SerializeField] private Button unequipButton;
    [SerializeField] private Button buyButton;

    private System.Action _onEquip;
    private System.Action _onSell;
    private System.Action _onUnequip;
    private System.Action _onBuy;

    protected override bool DontDestroy => false;

    protected override void Awake()
    {
        base.Awake();
        bgButton?.onClick.AddListener(Hide);
        closeButton?.onClick.AddListener(Hide);
        equipButton?.onClick.AddListener(OnEquipClicked);
        sellButton?.onClick.AddListener(OnSellClicked);
        unequipButton?.onClick.AddListener(OnUnequipClicked);
        buyButton?.onClick.AddListener(OnBuyClicked);
        if (panelRoot) panelRoot.SetActive(false);
    }

    public void Show(WeaponData data,
        System.Action onEquip   = null, System.Action onSell    = null,
        System.Action onUnequip = null, System.Action onBuy     = null)
    {
        if (data == null) return;

        _onEquip   = onEquip;
        _onSell    = onSell;
        _onUnequip = onUnequip;
        _onBuy     = onBuy;

        equipButton?.gameObject.SetActive(onEquip   != null);
        sellButton?.gameObject.SetActive(onSell     != null);
        unequipButton?.gameObject.SetActive(onUnequip != null);
        buyButton?.gameObject.SetActive(onBuy       != null);

        // ── 프레임 / 아이콘 ──────────────────────────────────────
        if (frameImage)
        {
            frameImage.sprite = Resources.Load<Sprite>("Weapons/grade/" + data.grade.ToString().ToLower());
            frameImage.color  = Color.white;
        }

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
            speedText.text = $"{data.attackCooldown:0.##}s";

        if (rangeText) rangeText.text = $"{data.attackRange:0.#}";
        if (critText)      critText.text      = data.criticalRate > 0f ? $"{data.criticalRate:0.#}x" : "-";
        if (bleedText)     bleedText.text     = data.bleedDamage  > 0f ? $"{data.bleedDamage:0.#}"  : "-";
        if (lifeStealText) lifeStealText.text = data.lifeSteal    > 0f ? $"{data.lifeSteal * 100f:0.#}%" : "-";

        // ── 스탯 서브값 (기본 스탯 대비 강화된 양) ────────────────
        SetSubText(attackSubText,    data.attackDamage,  data.baseAttackDamage);
        SetSubText(rangeSubText,     data.attackRange,   data.baseAttackRange);
        SetSubText(critSubText,      data.criticalRate * 100f,  data.baseCriticalRate * 100f, isAbsolute: true);
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
    // 액션 버튼 핸들러
    // ────────────────────────────────────────────────────────
    private void OnEquipClicked()   { _onEquip?.Invoke();   Hide(); }
    private void OnSellClicked()    { _onSell?.Invoke();    Hide(); }
    private void OnUnequipClicked() { _onUnequip?.Invoke(); Hide(); }
    private void OnBuyClicked()     { _onBuy?.Invoke();     Hide(); }

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
