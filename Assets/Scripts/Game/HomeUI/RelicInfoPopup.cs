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
    public static RelicInfoPopup Shared { get; private set; }

    [Header("아이콘")]
    [SerializeField] private Image iconImage;
    [SerializeField] private Image iconGradeImage;   // 아이콘 등급 프레임
    [SerializeField] private Image bannerImage;      // 등급 배너
    [SerializeField] private Image relicpieceIcon;
    [SerializeField] private Image enhanceMaterial;
    
    [Header("정보 텍스트")]
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI gradeText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI attrText;
    [SerializeField] private TextMeshProUGUI specialAttrText;
    [SerializeField] private TextMeshProUGUI descriptionText;
    [SerializeField] private TextMeshProUGUI fragmentText;
    [SerializeField] private TextMeshProUGUI relicpieceCountText;
    [SerializeField] private TextMeshProUGUI enhanceMaterialText;

    [Header("등급 태그 / 성급")]
    [SerializeField] private Image tagGradeImg;
    [SerializeField] private Image starImg;

    [Header("버튼")]
    [SerializeField] private Button        closeButton;
    [SerializeField] private Button        closeButton2;
    [SerializeField] private Button        breakthroughButton;

    [Header("애니메이션")]
    [SerializeField] private float    animDuration    = 0.18f;
    [SerializeField] private Animator upgradeAnimator;

    private static readonly Color LockedTint = new Color(0.4f, 0.4f, 0.4f, 1f);
    private const string HexGreen = "#00C800";
    private const string HexRed   = "#FF3232";

    private Coroutine _anim;
    private RelicData _currentData;
    private bool      _isAnimating;
    private bool      _suppressInventoryAnim;
    private int       _lastLevel;
    private int       _lastStarLevel;

    // ── 라이프사이클 ──────────────────────────────────────────────────────
    private void Awake()
    {
        Shared = this;
        closeButton?.onClick.AddListener(Hide);
        closeButton2?.onClick.AddListener(Hide);
        breakthroughButton?.onClick.AddListener(OnBreakthroughClicked);
        gameObject.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Shared == this) Shared = null;
    }

    private void OnEnable()
    {
        RelicInventory.OnChanged += OnInventoryChanged;
    }

    private void OnDisable()
    {
        RelicInventory.OnChanged -= OnInventoryChanged;
    }

    private void OnInventoryChanged()
    {
        if (_currentData == null) return;
        int newLevel     = RelicInventory.Shared?.GetLevel(_currentData.relicId) ?? 0;
        int newStarLevel = RelicInventory.Shared?.GetStarLevel(_currentData.relicId) ?? 1;

        if (!_suppressInventoryAnim && (newLevel > _lastLevel || newStarLevel > _lastStarLevel))
        {
            SoundManager.Shared?.Play(SoundKey.Relic_LevelUp);
            if (upgradeAnimator != null)
            {
                upgradeAnimator.enabled = false;
                upgradeAnimator.enabled = true;
                upgradeAnimator.SetTrigger("tLevelUp");
            }
        }

        _lastLevel     = newLevel;
        _lastStarLevel = newStarLevel;
        RefreshUI(_currentData);
    }

    // ── 공개 API ──────────────────────────────────────────────────────────
    public void Show(RelicData data)
    {
        if (data == null) return;
        _currentData   = data;
        _lastLevel     = RelicInventory.Shared?.GetLevel(data.relicId) ?? 0;
        _lastStarLevel = RelicInventory.Shared?.GetStarLevel(data.relicId) ?? 1;
        gameObject.SetActive(true);
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(ScaleTo(Vector3.zero, Vector3.one));
        RefreshUI(data);
    }

    private void RefreshUI(RelicData data)
    {
        var  inv        = RelicInventory.Shared;
        var  cfg        = RelicVisualConfig.Shared;
        bool locked     = inv == null || !inv.IsUnlocked(data.relicId);
        int  level      = inv?.GetLevel(data.relicId) ?? 0;
        int  frags      = inv?.GetFragments(data.relicId) ?? 0;
        int  nextT      = inv?.GetNextThreshold(data.relicId) ?? RelicInventory.FragThresholds[0];
        int  gradeIndex = locked ? -1 : (inv?.GetGradeIndex(data.relicId) ?? -1);
        int  starLevel  = inv?.GetStarLevel(data.relicId) ?? 1;
        var  grade      = locked ? RelicVisualConfig.Grade.Common
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

        // 등급 텍스트 (f_등급 기반)
        if (gradeText) gradeText.text = locked ? "잠금" : GradeLabel(grade);

        // 이름
        if (nameText) nameText.text = data.relicName;

        // 레벨
        if (levelText)
            levelText.text = locked
                ? $"Lv.0 / {RelicInventory.MaxLevel}"
                : $"Lv.{level} / {RelicInventory.MaxLevel}";

        // 주속성
        if (attrText)
            attrText.text = BuildAttrLine(data.primaryEffect, data.GetPrimaryValue(starLevel), locked);

        // 특수속성
        if (specialAttrText)
            specialAttrText.text = data.secondaryEffect == RelicEffectType.NONE
                ? "특수한 기운은 느껴지지 않습니다"
                : BuildAttrLine(data.secondaryEffect, data.GetSecondaryValue(starLevel), locked);

        // 설명
        if (descriptionText) descriptionText.text = data.description;

        // 조각 수 (frags/nextT, 달성 여부에 따라 색상)
        if (fragmentText)
        {
            if (nextT > 0)
            {
                string c = frags >= nextT ? HexGreen : HexRed;
                fragmentText.text = $"<color={c}>{frags}</color>/{nextT}";
            }
            else
            {
                fragmentText.text = $"{frags} (최대 등급)";
            }
        }

        // 유물강화권 개수 (보유/비용, 달성 여부에 따라 색상)
        if (enhanceMaterialText)
        {
            int mats = RelicInventory.Shared?.GetEnhanceMaterial() ?? 0;
            int cost = RelicInventory.BreakThroughMatCost;
            string mc = mats >= cost ? HexGreen : HexRed;
            enhanceMaterialText.text = $"<color={mc}>{mats}</color>/{cost}";
        }

        // 유물 조각 아이콘 / 조각 개수
        if (relicpieceIcon)
        {
            relicpieceIcon.sprite = cfg?.GetRelicIcon(data.relicId);
            relicpieceIcon.color  = locked ? LockedTint : Color.white;
        }
        // 유물 조각 개수 (돌파 비용 기준, 달성 여부에 따라 색상)
        if (relicpieceCountText)
        {
            int fragCost = inv?.GetBreakThroughFragCost(data.relicId) ?? RelicInventory.BreakThroughFragCosts[0];
            string fc = frags >= fragCost ? HexGreen : HexRed;
            relicpieceCountText.text = $"<color={fc}>{frags}</color>/{fragCost}";
        }

        // 등급 태그 이미지 (f_등급 기반)
        if (tagGradeImg)
        {
            var tagSprite = locked ? null : cfg?.GetTagGradeSprite(grade);
            tagGradeImg.sprite  = tagSprite;
            tagGradeImg.enabled = tagSprite != null;
        }

        // 성급 이미지 (등급과 무관, 1~4성 독립)
        if (starImg)
        {
            starImg.gameObject.SetActive(!locked);
            if (!locked)
            {
                starImg.sprite = cfg?.GetStarSprite(starLevel);
                starImg.SetNativeSize();
            }
        }

        // 성급 돌파 버튼 — 4성 미만이면 항상 클릭 가능 (조건 불충족 시 떨림으로 피드백)
        if (breakthroughButton)
            breakthroughButton.interactable = !locked && (inv?.GetStarLevel(data.relicId) ?? RelicInventory.MaxStar) < RelicInventory.MaxStar;
    }

    public void Hide()
    {
        if (!gameObject.activeSelf) return;
        if (_isAnimating) return;
        if (_anim != null) StopCoroutine(_anim);
        _anim = StartCoroutine(ScaleTo(transform.localScale, Vector3.zero, deactivateOnDone: true));
    }

    public void HideImmediate()
    {
        if (_anim != null) { StopCoroutine(_anim); _anim = null; }
        _isAnimating = false;
        transform.localScale = Vector3.zero;
        gameObject.SetActive(false);
    }

    // ── 성급 돌파 ─────────────────────────────────────────────────────────
    private void OnBreakthroughClicked()
    {
        if (_currentData == null || _isAnimating) return;

        var inv = RelicInventory.Shared;
        if (inv == null || !inv.CanBreakThrough(_currentData.relicId))
        {
            int frags    = inv?.GetFragments(_currentData.relicId) ?? 0;
            int fragCost = inv?.GetBreakThroughFragCost(_currentData.relicId) ?? 0;
            int mats     = inv?.GetEnhanceMaterial() ?? 0;

                if (frags < fragCost && relicpieceCountText != null)
                StartCoroutine(ShakeText(relicpieceCountText.rectTransform));
            if (mats < RelicInventory.BreakThroughMatCost && enhanceMaterialText != null)
                StartCoroutine(ShakeText(enhanceMaterialText.rectTransform));
            PopupBaseNoti.Shared?.Show("성급 돌파 재료 부족");
            return;
        }

        StartCoroutine(BreakThroughCoroutine());
    }

    private System.Collections.IEnumerator BreakThroughCoroutine()
    {
        _isAnimating = true;
        _suppressInventoryAnim = true;
        SetCloseInteractable(false);
        if (breakthroughButton) breakthroughButton.interactable = false;

        // 즉시 처리 — OnInventoryChanged의 중복 애니메이션은 플래그로 억제
        RelicInventory.Shared?.TryBreakThrough(_currentData.relicId);
        _suppressInventoryAnim = false;
        RefreshUI(_currentData);

        // 시각 효과
        if (upgradeAnimator != null)
        {
            SoundManager.Shared?.Play(SoundKey.Relic_LevelUp);
            upgradeAnimator.Play("relic_star_upgrade");
            yield return null;
            yield return null;
            float timer = 0f;
            while (timer < 10f)
            {
                var info = upgradeAnimator.GetCurrentAnimatorStateInfo(0);
                if (info.normalizedTime >= 1f && !upgradeAnimator.IsInTransition(0)) break;
                timer += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        _isAnimating = false;
        SetCloseInteractable(true);
    }

    private void SetCloseInteractable(bool value)
    {
        if (closeButton)  closeButton.interactable  = value;
        if (closeButton2) closeButton2.interactable = value;
    }

    private System.Collections.IEnumerator ShakeText(RectTransform rt)
    {
        Vector3 origin  = rt.localPosition;
        float   elapsed = 0f;
        const float duration  = 0.4f;
        const float frequency = 50f;
        const float magnitude = 8f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float x = Mathf.Sin(elapsed * frequency) * magnitude * (1f - elapsed / duration);
            rt.localPosition = new Vector3(origin.x + x, origin.y, origin.z);
            yield return null;
        }
        rt.localPosition = origin;
    }

    // ── 애니메이션 ────────────────────────────────────────────────────────
    private System.Collections.IEnumerator ScaleTo(Vector3 from, Vector3 to, bool deactivateOnDone = false)
    {
        transform.localScale = from;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / animDuration;
            transform.localScale = Vector3.LerpUnclamped(from, to, EaseOutBack(Mathf.Clamp01(t)));
            yield return null;
        }
        transform.localScale = to;
        if (deactivateOnDone) gameObject.SetActive(false);
        _anim = null;
    }

    private static float EaseOutBack(float t)
    {
        const float c = 1.70158f;
        return 1f + (c + 1f) * Mathf.Pow(t - 1f, 3f) + c * Mathf.Pow(t - 1f, 2f);
    }

    // ── 내부 헬퍼 ─────────────────────────────────────────────────────────
    private static string GradeLabel(RelicVisualConfig.Grade grade) => grade switch
    {
        RelicVisualConfig.Grade.Rare      => "고급",
        RelicVisualConfig.Grade.Epic      => "희귀",
        RelicVisualConfig.Grade.Legendary => "전설",
        RelicVisualConfig.Grade.Mythic    => "신화",
        _                                 => "일반",
    };

    private static string BuildAttrLine(RelicEffectType effect, float value, bool locked)
    {
        if (effect == RelicEffectType.NONE) return "-";
        if (locked) return $"{EffectLabel(effect)}  (잠금 해제 후 확인)";
        return $"{EffectLabel(effect)}  +{value:0.#}";
    }

    private static string EffectLabel(RelicEffectType e) => e switch
    {
        RelicEffectType.MELEE_ATK     => "근접무기 공격력 증가",
        RelicEffectType.RANGED_ATK    => "원거리무기 공격력 증가",
        RelicEffectType.MAGIC_ATK     => "마법무기 공격력 증가",
        RelicEffectType.BOOMERANG_ATK => "부메랑무기 공격력 증가",
        RelicEffectType.SPIN_ATK      => "스핀무기 공격력 증가",
        RelicEffectType.ATK           => "전체 공격력 증가",
        RelicEffectType.DEF           => "방어력 증가",
        RelicEffectType.MAX_HP        => "최대 체력 증가",
        RelicEffectType.MOV_SPD       => "이동속도 증가",
        RelicEffectType.ATK_SPD       => "공격속도 증가",
        RelicEffectType.EVA           => "회피 증가",
        RelicEffectType.CRT_RATE      => "치명타 확률 증가",
        RelicEffectType.CRT_DMG       => "치명타 데미지 증가",
        RelicEffectType.BLEED         => "출혈 데미지 증가",
        RelicEffectType.HP_REGEN      => "지속 회복량 증가",
        RelicEffectType.MAGNET        => "흡수 반경 증가",
        RelicEffectType.KNOCKBACK     => "넉백 증가",
        RelicEffectType.HEAL_KILL     => "처치 회복 증가",
        RelicEffectType.BOSS_DMG      => "보스 데미지 감소",
        RelicEffectType.BOSS_ATK      => "보스 공격피해 증가",
        RelicEffectType.ELITE_DMG     => "엘리트 피해 감소",
        _                             => e.ToString(),
    };
}
