using Mewage.DB;
using UnityEngine;

/// <summary>희귀도 — 상점 뽑기 확률 결정. DB_무기.f_등급에서 읽습니다.</summary>
public enum WeaponGrade { Common, Rare, Epic, Legendary, Mythic }

/// <summary>
/// 무기 한 종류의 런타임 데이터.
/// DB_무기에서 FromDB()로 생성합니다.
/// </summary>
public class WeaponData : ScriptableObject
{
    [Header("기본 정보")]
    public string       weaponName;
    public Sprite       icon;
    public WeaponType   weaponType;
    public MagicElement magicElement; // Magic 타입일 때만 유효
    public GameObject   prefab;

    [Header("희귀도 (상점 뽑기 확률)")]
    public WeaponGrade grade;

    [Header("성 단계 (합성으로 증가, 프레임 색 결정)")]
    public int star = 1;        // 1~5성

    [Header("가격 & 출현 조건")]
    public int price   = 20;
    public int chapter = 1;

    [Header("스토리 설명")]
    public string storyDescription;

    // ────────────────────────────────────────────────────────────────
    // 기본 스탯 (1성 기준, DB_무기에서 로드. 합성 중 변하지 않음)
    // ────────────────────────────────────────────────────────────────
    [Header("기본 스탯 (1성 기준, DB)")]
    public float baseAttackDamage;
    public float baseAttackCooldown;  // DB f_공격속도(0~100) / 100 = 실제 쿨타임(초)
    public float baseAttackRange;
    public float baseAreaHitRadius;   // DB f_주변범위
    public float baseCriticalRate;
    public float baseCriticalDamage;  // 치명타 데미지 배율
    public float baseBleedDamage;     // 출혈
    public float baseLifeSteal;       // 흡혈
    public float baseKnockbackPower;  // 넉백 지수
    public int   baseMaxBounces;      // 부메랑 튕김 횟수
    public float baseBounceRange;     // 부메랑 튕김 범위

    // ────────────────────────────────────────────────────────────────
    // 현재 스탯 (성 보너스 적용 후)
    // ────────────────────────────────────────────────────────────────
    [Header("현재 스탯 (성 보너스 적용 후)")]
    public float attackDamage;
    public float attackCooldown;
    public float attackRange;
    public float areaHitRadius;
    public float criticalRate;
    public float criticalDamage; // 치명타 데미지 배율
    public float bleedDamage;    // 출혈
    public float lifeSteal;      // 흡혈
    public float knockbackPower; // 넉백 지수
    public int   maxBounces;     // 부메랑 튕김 횟수
    public float bounceRange;    // 부메랑 튕김 범위

    // ────────────────────────────────────────────────────────────────
    // 성별 강화 보너스
    // ────────────────────────────────────────────────────────────────
    [Header("성별 강화 보너스")]
    public WeaponStarBonus bonus2Star;
    public WeaponStarBonus bonus3Star;
    public WeaponStarBonus bonus4Star;

    // ────────────────────────────────────────────────────────────────
    // 합성 — 같은 무기를 복사하고 성 1단계 올립니다
    // ────────────────────────────────────────────────────────────────
    public WeaponData Upgraded() => UpgradedTo(star + 1);

    /// <summary>이 무기를 targetStar 성으로 복제합니다. 보너스는 해당 성 이하 전부 적용됩니다.</summary>
    public WeaponData UpgradedTo(int targetStar)
    {
        var next              = CreateInstance<WeaponData>();
        next.weaponName       = weaponName;
        next.icon             = icon;
        next.weaponType       = weaponType;
        next.magicElement     = magicElement;
        next.prefab           = prefab;
        next.grade            = grade;
        next.price            = price;
        next.chapter          = chapter;
        next.star             = Mathf.Clamp(targetStar, 1, 5);
        next.storyDescription = storyDescription;
        next.bonus2Star       = bonus2Star;
        next.bonus3Star       = bonus3Star;
        next.bonus4Star       = bonus4Star;

        next.baseAttackDamage   = baseAttackDamage;
        next.baseAttackCooldown = baseAttackCooldown;
        next.baseAttackRange    = baseAttackRange;
        next.baseAreaHitRadius  = baseAreaHitRadius;
        next.baseCriticalRate   = baseCriticalRate;
        next.baseCriticalDamage = baseCriticalDamage;
        next.baseBleedDamage    = baseBleedDamage;
        next.baseLifeSteal      = baseLifeSteal;
        next.baseKnockbackPower = baseKnockbackPower;
        next.baseMaxBounces     = baseMaxBounces;
        next.baseBounceRange    = baseBounceRange;

        next.ResetToBase();
        next.ApplyBonusesUpToStar();

        return next;
    }

    // ────────────────────────────────────────────────────────────────
    // BGDatabase → WeaponData 변환
    // ────────────────────────────────────────────────────────────────
    public static WeaponData FromDB(DB_무기 row)
    {
        WeaponStatRegistry.EnsureLoaded();

        var data              = CreateInstance<WeaponData>();
        data.weaponName       = row.f_이름;
        data.grade            = ParseGrade(row.f_등급);
        data.star             = 1;
        data.weaponType       = ParseWeaponType(row.f_타입);
        data.price            = row.f_가격;
        // weaponId 접두어(ch3_ → 3)에서 챕터 파싱 — f_챕터 오입력 방어
        {
            int parsed = row.f_챕터;
            string wid = row.f_무기ID;
            if (!string.IsNullOrEmpty(wid))
            {
                var p0 = wid.ToLower().Split('_')[0];
                if (p0.StartsWith("ch") && int.TryParse(p0.Substring(2), out int n))
                    parsed = n;
            }
            data.chapter = parsed;
        }
        data.storyDescription = row.f_스토리설명;
        data.bonus2Star = WeaponStatRegistry.Build(row.f_속성2성ID, row.f_S1);
        data.bonus3Star = WeaponStatRegistry.Build(row.f_속성3성ID, row.f_S2);
        data.bonus4Star = WeaponStatRegistry.Build(row.f_속성4성ID, row.f_S3);

        // DB에서 직접 기본 스탯 로드
        // f_공격속도는 float (초 단위 쿨타임) — 0.8 → 0.8초에 한 번 공격
        data.magicElement = ParseMagicElement(row.f_타입);

        data.baseAttackDamage   = row.f_기본공격력;
        data.baseAttackCooldown = row.f_공격속도 > 0 ? row.f_공격속도 : 1f;
        data.baseAttackRange    = row.f_탐지범위;
        data.baseAreaHitRadius  = row.f_주변범위;
        data.baseMaxBounces     = row.f_튕김횟수;
        data.baseBounceRange    = data.weaponType == WeaponType.Boomerang ? 5f : 0f;

        // DB에서 직접 로드 (무기 프리팹 Inspector 의존 제거)
        // 아래 필드는 BansheeGz DB_무기에 컬럼을 추가한 후 사용하세요:
        // f_넉백(float), f_치명타(float, 0~1), f_출혈(float), f_흡혈(float)
        data.baseCriticalRate   = row.f_치명타 / 100f; // DB: 0~100 → 내부: 0~1
        data.baseCriticalDamage = 2f;            // 치명타 배율 고정 2배
        data.baseBleedDamage    = row.f_출혈;
        data.baseLifeSteal      = row.f_흡혈;
        data.baseKnockbackPower = row.f_넉백;

        // 무기ID 기반으로 Resources에서 프리팹 로드 (예: Resources/Weapons/Ch1/ch1_n_00)
        string weaponId = row.f_무기ID;
        if (!string.IsNullOrEmpty(weaponId))
        {
            string path = $"Weapons/Ch{data.chapter}/{weaponId.ToLower()}";
            data.prefab = Resources.Load<GameObject>(path);
            if (data.prefab == null)
                Debug.LogWarning($"[WeaponData] 프리팹 없음: Resources/{path}");
        }

        if (data.prefab != null)
        {
            data.icon = data.prefab.GetComponent<SpriteRenderer>()?.sprite
                     ?? data.prefab.GetComponentInChildren<SpriteRenderer>()?.sprite;
        }

        data.ResetToBase();
        return data;
    }

    private static WeaponGrade ParseGrade(string s)
    {
        if (System.Enum.TryParse<WeaponGrade>(s, true, out var g)) return g;
        var result = s switch
        {
            "일반" => WeaponGrade.Common,
            "레어" => WeaponGrade.Rare,
            "희귀" => WeaponGrade.Epic,
            "전설" => WeaponGrade.Legendary,
            "신화" => WeaponGrade.Mythic,
            _                             => (WeaponGrade)(-1),
        };
        if ((int)result < 0)
        {
            Debug.LogWarning($"[WeaponData] ParseGrade: 알 수 없는 등급 값 '{s}' → Common으로 대체. BansheeGz DB의 f_등급 필드를 확인하세요.");
            return WeaponGrade.Common;
        }
        return result;
    }

    private static WeaponType ParseWeaponType(string s)
    {
        if (System.Enum.TryParse<WeaponType>(s, true, out var t)) return t;
        return s switch
        {
            "근거리"                          => WeaponType.Melee,
            "원거리"                          => WeaponType.Ranged,
            "마법" or "번개" or "불" or "얼음" => WeaponType.Magic,
            "부메랑"                          => WeaponType.Boomerang,
            "돌리기"                          => WeaponType.Spin,
            "슬래시" or "베기"                => WeaponType.Slash,
            _                                 => WeaponType.Melee,
        };
    }

    private static MagicElement ParseMagicElement(string s) => s switch
    {
        "번개" or "Lightning" => MagicElement.Lightning,
        "불"   or "Fire"      => MagicElement.Fire,
        "얼음" or "Ice"       => MagicElement.Ice,
        _                     => MagicElement.None,
    };

    // ────────────────────────────────────────────────────────────────
    // 내부 헬퍼
    // ────────────────────────────────────────────────────────────────

    private void ResetToBase()
    {
        attackDamage   = baseAttackDamage;
        attackCooldown = baseAttackCooldown;
        attackRange    = baseAttackRange;
        areaHitRadius  = baseAreaHitRadius;
        criticalRate   = baseCriticalRate;
        criticalDamage = baseCriticalDamage;
        bleedDamage    = baseBleedDamage;
        lifeSteal      = baseLifeSteal;
        knockbackPower = baseKnockbackPower;
        maxBounces     = baseMaxBounces;
        bounceRange    = baseBounceRange;
    }

    private void ApplyBonusesUpToStar()
    {
        if (star >= 2) WeaponStatRegistry.ApplyBonus(this, bonus2Star);
        if (star >= 3) WeaponStatRegistry.ApplyBonus(this, bonus3Star);
        if (star >= 4) WeaponStatRegistry.ApplyBonus(this, bonus4Star);

        // 부메랑 전용: 성마다 튕김 횟수 +2
        if (weaponType == WeaponType.Boomerang)
        {
            if (star >= 2) maxBounces += 2;
            if (star >= 3) maxBounces += 2;
            if (star >= 4) maxBounces += 2;
        }
    }
}
