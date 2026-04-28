using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 유물 상세 정보 팝업.
///
/// Inspector 연결:
///   iconImage       — 유물 아이콘 (잠금 시 어둡게)
///   gradeImage      — 등급 배경 이미지 (등급 색 적용)
///   nameText        — 유물 이름
///   gradeText       — 등급 레이블 ("일반" / "신화" / "잠금")
///   attrText        — 주속성 ("근접 공격력 +5.0")
///   specialAttrText — 특수속성 ("방어력 +3.0")
///   descriptionText — 설명
///   fragmentText    — 조각 수 / 다음 임계값
///   levelText       — 현재 레벨 ("Lv.3 / 10")
///   closeButton     — 닫기 버튼
/// </summary>
public class RelicInfoPopup : MonoBehaviour
{
    [Header("아이콘")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image iconGradeImage;   // 아이콘 등급 프레임
    [SerializeField] private Image bannerImage;      // 등급 배너

    [Header("정보 텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI gradeText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI attrText;
    [SerializeField] private TextMeshProUGUI specialAttrText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI fragmentText;

    [Header("닫기")]
    [SerializeField] private Button closeButton;

    private static readonly Color LockedTint = new Color(0.4f, 0.4f, 0.4f, 1f);

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    private void Awake()
    {
        closeButton?.onClick.AddListener(Hide);
        gameObject.SetActive(false);
    }

    // ── 공개 API ──────────────────────────────────────────────────────────
    public void Show(RelicData data)
    {
        if (data == null) return;
        gameObject.SetActive(true);

        var  inv    = RelicInventory.Shared;
        var  cfg    = RelicVisualConfig.Shared;
        bool locked = inv == null || !inv.IsUnlocked(data.relicId);
        int  level  = inv?.GetLevel(data.relicId) ?? 0;
        int  frags  = inv?.GetFragments(data.relicId) ?? 0;
        int  nextT  = inv?.GetNextThreshold(data.relicId) ?? RelicInventory.FragThresholds[0];
        var  grade  = locked ? RelicVisualConfig.Grade.Common
                             : RelicVisualConfig.ParseGrade(data.grade);

        // 아이콘: config에서 표시 시점에 로드
        if (iconImage)
        {
            iconImage.sprite = cfg?.GetRelicIcon(data.relicId);
            iconImage.color  = locked ? LockedTint : Color.white;
        }

        // 아이콘 등급 프레임
        if (iconGradeImage)
        {
            iconGradeImage.sprite = locked ? null : cfg?.GetIconSprite(grade);
            iconGradeImage.color  = Color.white;
        }

        // 등급 배너
        if (bannerImage)
        {
            bannerImage.sprite = locked ? null : cfg?.GetBannerSprite(grade);
            bannerImage.color  = Color.white;
        }

        // 등급 텍스트
        if (gradeText) gradeText.text = locked ? "잠금" : inv.GetGradeLabel(data.relicId);

        // 이름
        if (nameText) nameText.text = data.relicName;

        // 레벨
        if (levelText)
            levelText.text = locked
                ? $"Lv.0 / {RelicInventory.MaxLevel}"
                : $"Lv.{level} / {RelicInventory.MaxLevel}";

        // 주속성
        if (attrText)
            attrText.text = BuildAttrLine(data.primaryEffect, data.GetPrimaryValue(level), locked);

        // 특수속성
        if (specialAttrText)
            specialAttrText.text = BuildAttrLine(data.secondaryEffect, data.GetSecondaryValue(level), locked);

        // 설명
        if (descriptionText) descriptionText.text = data.description;

        // 조각 수
        if (fragmentText)
        {
            fragmentText.text = nextT > 0
                ? $"조각  {frags} / {nextT}"
                : $"조각  {frags}  (최대 등급)";
        }
    }

    public void Hide() => gameObject.SetActive(false);

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────
    private static string BuildAttrLine(RelicEffectType effect, float value, bool locked)
    {
        if (effect == RelicEffectType.NONE) return "-";
        if (locked) return $"{EffectLabel(effect)}  (잠금 해제 후 확인)";
        return $"{EffectLabel(effect)}  +{value:0.#}";
    }

    private static string EffectLabel(RelicEffectType e) => e switch
    {
        RelicEffectType.MELEE_ATK     => "근접 공격력",
        RelicEffectType.RANGED_ATK    => "원거리 공격력",
        RelicEffectType.MAGIC_ATK     => "마법 공격력",
        RelicEffectType.BOOMERANG_ATK => "부메랑 공격력",
        RelicEffectType.SPIN_ATK      => "스핀 공격력",
        RelicEffectType.ATK           => "전체 공격력",
        RelicEffectType.DEF           => "방어력",
        RelicEffectType.MAX_HP        => "최대 체력",
        RelicEffectType.MOV_SPD       => "이동속도",
        RelicEffectType.ATK_SPD       => "공격속도",
        RelicEffectType.EVA           => "회피",
        RelicEffectType.CRT_RATE      => "치명타 확률",
        RelicEffectType.CRT_DMG       => "치명타 데미지",
        RelicEffectType.BLEED         => "출혈 데미지",
        RelicEffectType.HP_REGEN      => "체력 재생",
        RelicEffectType.MAGNET        => "흡수 반경",
        RelicEffectType.KNOCKBACK     => "넉백",
        RelicEffectType.HEAL_KILL     => "처치 회복",
        RelicEffectType.BOSS_DMG      => "보스 피해 감소",
        RelicEffectType.BOSS_ATK      => "보스 피해 증가",
        RelicEffectType.ELITE_DMG     => "엘리트 피해 감소",
        _                             => e.ToString(),
    };
}
