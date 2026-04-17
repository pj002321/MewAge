using System.Collections.Generic;
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
    [SerializeField] private Image           gradeImage;
    [SerializeField] private Image           iconImage;
    [SerializeField] private List<GameObject> starObjects; // 0=1성, 1=2성, 2=3성, 3=4성
    [SerializeField] private Image           weaponTypeImg;
    [SerializeField] private TextMeshProUGUI gradeText;   // 예: "레어"
    [SerializeField] private TextMeshProUGUI weaponTypeText;
    [SerializeField] private TextMeshProUGUI synergyTagText;
    [SerializeField] private TextMeshProUGUI nameText;

    [Header("스탯 — 메인값")]
    [SerializeField] private TextMeshProUGUI attackText;
    [SerializeField] private TextMeshProUGUI speedText;   // 공격속도 (회/s)
    [SerializeField] private TextMeshProUGUI rangeText;
    [SerializeField] private TextMeshProUGUI critText;
    [SerializeField] private TextMeshProUGUI critDmgText;
    [SerializeField] private TextMeshProUGUI bleedText;
    [SerializeField] private TextMeshProUGUI lifeStealText;
    [SerializeField] private TextMeshProUGUI knockbackText;

    [Header("성 강화 보너스 설명")]
    [SerializeField] private TextMeshProUGUI starDesc;   // "공격력 3 / 5 / 7 % 증가" 통합 표시

    [Header("스토리 설명")]
    [SerializeField] private TextMeshProUGUI storyText;

    [Header("시너지 태그 아이콘")]
    [SerializeField] private Image synergyIcon;

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

        // ── 프레임 / 등급 / 아이콘 ──────────────────────────────────────
        if (frameImage)
        {
            var gradeSprite = WeaponVisualConfig.Shared?.GetGradeSprite(data.grade);
            if (gradeSprite != null)
            {
                frameImage.sprite = gradeSprite;
                frameImage.color  = Color.white;
            }
        }

        if (gradeImage)
            gradeImage.color = WeaponGradeColors.Get(data.grade);

        if (iconImage)
        {
            iconImage.sprite  = data.icon;
            iconImage.enabled = data.icon != null;
        }

        if (weaponTypeImg)
        {
            weaponTypeImg.sprite  = WeaponVisualConfig.Shared?.GetTypeSprite(data.weaponType);
            weaponTypeImg.enabled = weaponTypeImg.sprite != null;
            weaponTypeImg.color   = WeaponGradeColors.Get(data.grade);
        }

        // ── 이름 / 별 / 등급 ─────────────────────────────────────
        SetStarObject(starObjects, data.star);
        if (nameText)  nameText.text  = data.weaponName;
        if (gradeText)
            gradeText.text = GradeName(data.grade);

        if (weaponTypeText)
        {
            weaponTypeText.text = data.weaponType switch
            {
                WeaponType.Melee     => "근거리",
                WeaponType.Ranged    => "원거리",
                WeaponType.Magic     => "마법",
                WeaponType.Boomerang => "부메랑",
                WeaponType.Spin      => "회전",
                _                    => data.weaponType.ToString(),
            };
        }

        if (synergyTagText)
        {
            synergyTagText.text = SynergyHelper.GetDisplayName(data.tag1);
        }

        // ── 스탯 메인값 ───────────────────────────────────────────
        // 공격력: 정수
        if (attackText) attackText.text = $"{Mathf.RoundToInt(data.attackDamage)}";

        // 공격속도: 쿨다운(초) → "회/s" 로 표기, 소수점 1자리 (AtkSpeed 증강 배율 포함)
        // pct=100 → 1x, pct=110 → 1.1x (WeaponPoint.SetStatSpeedPct와 동일 공식)
        if (speedText)
        {
            float reduction   = PlayerStatManager.Shared?.GetFinal(StatType.AtkSpeed) ?? 0f;
            float effCooldown = data.attackCooldown > 0f
                ? Mathf.Max(0.1f, data.attackCooldown - reduction)
                : 0f;
            speedText.text = effCooldown > 0f ? $"{1f / effCooldown:0.#}" : "-";
        }

        // 사거리: 소수점 1자리
        if (rangeText) rangeText.text = $"{data.attackRange:0.#}";

        // 치명타확률: 소수점 2자리 %
        if (critText)
            critText.text = data.criticalRate > 0f
                ? $"{data.criticalRate * 100f:0.##}%"
                : "-";

        // 치명타 데미지: 소수점 1자리 %
        if (critDmgText)
            critDmgText.text = data.criticalDamage > 1f
                ? $"{(data.criticalDamage - 1f) * 100f:0.#}%"
                : "-";

        // 출혈(지속피해): 소수점 1자리
        if (bleedText)
            bleedText.text = data.bleedDamage > 0f
                ? $"{data.bleedDamage:0.#}"
                : "-";

        // 흡혈: 소수점 2자리 %
        if (lifeStealText)
            lifeStealText.text = data.lifeSteal > 0f
                ? $"{data.lifeSteal * 100f:0.##}%"
                : "-";

        // 넉백 지수: 소수점 1자리
        if (knockbackText)
            knockbackText.text = data.knockbackPower > 0f
                ? $"{data.knockbackPower:0.#}"
                : "-";

        // ── 성별 강화 설명 (공격력 3 / 5 / 7 % 증가 형태) ────────────
        SetCombinedStarDesc(starDesc, null, null,
            data.bonus2Star, data.bonus3Star, data.bonus4Star, data.star);

        // ── 스토리 설명 ───────────────────────────────────────────
        if (storyText) storyText.text = data.storyDescription ?? string.Empty;

        // ── 시너지 태그 아이콘 ────────────────────────────────────
        int chapter = UpgradeManager.Shared?.CurrentChapter ?? 1;
        SetSynergyIcon(synergyIcon, data.tag1, chapter);

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

    /// <summary>정수형 서브텍스트. 강화로 추가된 양을 정수로 표시합니다.</summary>
    private static void SetSubTextInt(TextMeshProUGUI label, float current, float baseVal)
    {
        if (label == null) return;
        int delta = Mathf.RoundToInt(current - baseVal);
        if (delta == 0) { label.text = string.Empty; return; }
        label.text = delta > 0
            ? $"<color=#4FC3F7>+{delta}</color>"
            : $"<color=#EF9A9A>{delta}</color>";
    }

    /// <summary>소수형 서브텍스트. 강화로 추가된 양을 소수점으로 표시합니다.</summary>
    private static void SetSubTextDec(TextMeshProUGUI label, float current, float baseVal,
                                      string fmt = "0.##", string suffix = "")
    {
        if (label == null) return;
        float delta = current - baseVal;
        if (Mathf.Approximately(delta, 0f)) { label.text = string.Empty; return; }
        string s = delta.ToString(fmt);
        label.text = delta > 0f
            ? $"<color=#4FC3F7>+{s}{suffix}</color>"
            : $"<color=#EF9A9A>{s}{suffix}</color>";
    }

    /// <summary>공격속도 서브값 — 1성 기준(증강 배율 포함) 대비 현재 APS 델타.</summary>
    private static void SetSpeedSubText(TextMeshProUGUI speedSub, WeaponData data)
    {
        if (speedSub == null) return;

        float reduction  = PlayerStatManager.Shared?.GetFinal(StatType.AtkSpeed) ?? 0f;

        float baseAps    = data.baseAttackCooldown > 0f
            ? 1f / Mathf.Max(0.1f, data.baseAttackCooldown - reduction) : 0f;
        float currentAps = data.attackCooldown > 0f
            ? 1f / Mathf.Max(0.1f, data.attackCooldown - reduction) : 0f;
        float apsDelta   = currentAps - baseAps;

        if (Mathf.Approximately(apsDelta, 0f)) { speedSub.text = string.Empty; return; }
        speedSub.text = apsDelta > 0f
            ? $"<color=#4FC3F7>+{apsDelta:0.#}</color>"
            : $"<color=#EF9A9A>{apsDelta:0.#}</color>";
    }

    /// <summary>
    /// 성 강화 설명을 "공격력 3 / 5 / 7 % 증가" 형식으로 star2Desc 한 곳에 표시합니다.
    ///
    /// 하이라이트 규칙:
    ///   · currentStar &lt; 2  → 전체 회색
    ///   · currentStar &gt;= 2 → 숫자를 제외한 텍스트(스탯명, /, % 증가)는 흰색
    ///                         현재 성에 해당하는 숫자만 금색, 나머지 숫자는 회색
    /// star3Desc, star4Desc는 사용하지 않아 빈 문자열로 초기화합니다.
    /// </summary>
    private static void SetCombinedStarDesc(
        TextMeshProUGUI labelDesc, TextMeshProUGUI label3, TextMeshProUGUI label4,
        WeaponStarBonus b2, WeaponStarBonus b3, WeaponStarBonus b4,
        int currentStar)
    {
        if (label3 != null) label3.text = string.Empty;
        if (label4 != null) label4.text = string.Empty;
        if (labelDesc == null) return;

        // 보너스가 하나도 없으면 숨김
        if (b2.IsEmpty && b3.IsEmpty && b4.IsEmpty)
        {
            labelDesc.text = string.Empty;
            return;
        }

        // 스탯 레이블: 첫 번째 비어있지 않은 보너스의 statId 기준
        string statId    = !b2.IsEmpty ? b2.statId : (!b3.IsEmpty ? b3.statId : b4.statId);
        string statLabel = StatIdToLabel(statId);
        string unit      = StatIdToUnit(statId);

        // 수치 문자열 (정수이면 정수로, 소수면 소수점 1자리)
        string v2 = FormatBonusVal(b2);
        string v3 = FormatBonusVal(b3);
        string v4 = FormatBonusVal(b4);

        // 색상 상수
        const string Gold  = "#FFD740";
        const string White = "#FFFFFF";
        const string Gray  = "#888888";

        bool bright = currentStar >= 2;   // 숫자 외 텍스트 밝기 여부
        string tc   = bright ? White : Gray;

        // 현재 성에 해당하는 숫자만 금색, 나머지는 회색
        string c2 = (currentStar == 2) ? Gold : Gray;
        string c3 = (currentStar == 3) ? Gold : Gray;
        string c4 = (currentStar >= 4) ? Gold : Gray;

        if (bright)
        {
            labelDesc.text =
                $"<color={tc}>{statLabel} </color>" +
                $"<color={c2}>{v2}</color>" +
                $"<color={tc}> / </color>" +
                $"<color={c3}>{v3}</color>" +
                $"<color={tc}> / </color>" +
                $"<color={c4}>{v4}</color>" +
                $"<color={tc}> {unit} 증가</color>";
        }
        else
        {
            // 2성 미만: 전체 회색
            labelDesc.text = $"<color={Gray}>{statLabel} {v2} / {v3} / {v4} {unit} 증가</color>";
        }
    }

    /// <summary>WeaponStarBonus 수치를 표시용 문자열로 변환. 보너스가 없으면 "-".</summary>
    private static string FormatBonusVal(WeaponStarBonus b)
    {
        if (b.IsEmpty) return "-";
        return Mathf.Approximately(b.value % 1f, 0f)
            ? $"{Mathf.RoundToInt(b.value)}"
            : $"{b.value:0.#}";
    }

    /// <summary>statId → 한글 스탯 레이블.</summary>
    private static string StatIdToLabel(string statId) => statId switch
    {
        "ATK_ADD"    => "공격력",
        "CD_RED"     => "쿨타임",
        "RNG_ADD"    => "사거리",
        "DMG_RNG"    => "타격범위",
        "CRIT_ADD"   => "치명타",
        "CRT_RATE"   => "치명타확률",
        "DOT_AD"     => "출혈",
        "HP_STL_ADD" => "흡혈",
        "NUK_ADD"    => "넉백",
        "HEAL_ADD"   => "자가치유",
        _            => statId,
    };

    /// <summary>statId → 단위.</summary>
    private static string StatIdToUnit(string statId) => statId switch
    {
        "ATK_ADD"    => "%",
        "CD_RED"     => "%",
        "RNG_ADD"    => "%",
        "DMG_RNG"    => "",
        "CRIT_ADD"   => "%",
        "CRT_RATE"   => "%",
        "DOT_AD"     => "",
        "HP_STL_ADD" => "%",
        "NUK_ADD"    => "",
        "HEAL_ADD"   => "%",
        _            => "%",
    };

    public static void SetStarObject(List<GameObject> objs, int star)
    {
        if (objs == null) return;
        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null) objs[i].SetActive(i == star - 1 && star > 0);
    }

    public static void SetSynergyIcon(Image img, WeaponSynergyTag tag, int chapter)
    {
        if (img == null) return;
        var sprite = WeaponVisualConfig.Shared?.GetSynergySprite(tag);
        img.sprite  = sprite;
        img.enabled = sprite != null && tag != WeaponSynergyTag.None;
        if (img.enabled) img.color = SynergyHelper.ColorBronze;
    }

    public static void SetTypeIcon(Image img, WeaponType type, WeaponGrade grade)
    {
        if (img == null) return;
        var sprite = WeaponVisualConfig.Shared?.GetTypeSprite(type);
        img.sprite  = sprite;
        img.enabled = sprite != null;
        img.color   = WeaponGradeColors.Get(grade);
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
