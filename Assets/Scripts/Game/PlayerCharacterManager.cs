using Mewage.DB;
using UnityEngine;

/// <summary>
/// 장착된 캐릭터의 기초 스탯 + 증강 보너스를 PlayerController에 적용합니다.
///
/// ■ 흐름
///   홈 씬  : CharacterSlotView.OnEquip → EquipCharacter(row)  → PlayerPrefs에 저장
///   게임 씬: GameManager.Start / ImprovManager 초기화 전  → ApplyCharacterStats()
///
/// ■ 증강 구조 (DB_캐릭터)
///   Tier 1 : f_속성1ID + f_S1  → 속성 보너스 1단계
///   Tier 2 : f_속성2ID + f_S2  → 속성 보너스 2단계 (Tier 1에 누적)
///   Tier 3 : f_속성3ID + f_S3  → 속성 보너스 3단계 (Tier 1+2에 누적)
///   Tier 4 : f_스킬ID / f_능력치설명  → 고유 능력 (별도 스킬 시스템 연동)
///
/// ■ 등급별 기초 스탯 (f_등급 → 기초 체력 / 방어력 / 공격력)
///   C → 100 / 10 / 10   B → 150 / 20 / 20
///   A → 200 / 30 / 30   S → 250 / 50 / 50   SS → 300 / 70 / 70
///
/// ■ 지원 Stat ID
///   STAT_ATK       : 공격력 flat +      (기초공격력 × value%)
///   STAT_DEF       : 방어력 flat +      (기초방어력 × value%)
///   STAT_HP        : 최대 체력 flat +   (기초체력 × value%)
///   STAT_SPD_ATK   : 무기 공격속도 +value%
///   STAT_SPD_MOV   : 이동속도 flat +    (기초이동속도 × value%)
///   STAT_CRI_RATE  : 치명타 확률 +value% (회피 슬롯 미사용 — WeaponPoint 연동 필요)
///   STAT_EVA_RATE  : 회피 확률 +value%
///   STAT_ATK_RNG   : 무기 사거리 +value% (WeaponPoint.MultiplyRange 필요)
///   STAT_CUR_REWARD: 골드 픽업 보너스 flat (PlayerStats.GoldBonus 누적)
///   STAT_CUR_EXP   : 경험치 픽업 보너스 flat (PlayerStats.ExpBonus 누적)
/// </summary>
public class PlayerCharacterManager : MonoBehaviourSingleton<PlayerCharacterManager>
{
    private const string PrefKeyChar = "equipped_char";
    private const string PrefKeyTier = "char_tier_{0}"; // 캐릭터별 증강 단계 (1~3)

    private Sprite _cachedSprite;
    private string _cachedSpriteCharId;

    protected override bool DontDestroy => true; // 홈→게임 씬 이동 시 유지

    // ─────────────────────────────────────────────────────────────
    // 장착 / 조회
    // ─────────────────────────────────────────────────────────────

    /// <summary>캐릭터를 장착합니다. (CharacterSlotView.OnEquip에서 호출)</summary>
    public void EquipCharacter(DB_캐릭터 row)
    {
        if (row == null || !row.f_보유여부) return;
        PlayerPrefs.SetString(PrefKeyChar, row.f_캐릭ID);
        PlayerPrefs.Save();
        Debug.Log($"[CharMgr] 장착: {row.f_이름} (ID={row.f_캐릭ID})");
    }

    /// <summary>현재 장착된 캐릭터 ID (f_캐릭ID)</summary>
    public string EquippedCharID => PlayerPrefs.GetString(PrefKeyChar, "");

    /// <summary>캐릭터별 현재 증강 단계 (1~3). 미보유이거나 미설정이면 0.</summary>
    public int GetAugmentTier(string charID)
        => PlayerPrefs.GetInt(string.Format(PrefKeyTier, charID), 1);

    /// <summary>증강 단계를 변경합니다. (증강 버튼에서 호출)</summary>
    public void SetAugmentTier(string charID, int tier)
    {
        PlayerPrefs.SetInt(string.Format(PrefKeyTier, charID), Mathf.Clamp(tier, 1, 3));
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────────────────────────────
    // 스탯 적용 (게임 씬 시작 시 호출)
    // ─────────────────────────────────────────────────────────────

    /// <summary>장착된 캐릭터의 기초 스탯 + 증강 보너스를 PlayerController에 적용합니다.</summary>
    public void ApplyCharacterStats()
    {
        string charID = EquippedCharID;
        if (string.IsNullOrEmpty(charID))
        {
            Debug.LogWarning("[CharMgr] 장착된 캐릭터 없음 (PlayerPrefs 비어있음)");
            return;
        }

        var row = DB_캐릭터.FindEntity(r => r.f_캐릭ID == charID);
        if (row == null || !row.f_보유여부)
        {
            Debug.LogWarning($"[CharMgr] 장착 캐릭터를 찾을 수 없음: {charID}");
            return;
        }

        GetBaseStats(row.f_등급,
            out float baseHp, out float baseDef, out float baseAtk);

        var player = PlayerController.Instance;
        var stats  = PlayerStats.Shared;
        var sm     = PlayerStatManager.Shared;
        if (player == null)
        {
            Debug.LogWarning("[CharMgr] PlayerController.Instance 가 null — 스탯 적용 실패");
            return;
        }

        // 0. 캐릭터 스프라이트 교체 — 동일 캐릭터면 캐시 재사용, 변경 시에만 Resources.Load
        if (!string.IsNullOrEmpty(row.f_캐릭ID))
        {
            if (_cachedSprite == null || _cachedSpriteCharId != row.f_캐릭ID)
            {
                string spritePath = $"Character/{row.f_캐릭ID.ToLower()}";
                _cachedSprite     = Resources.Load<Sprite>(spritePath);
                _cachedSpriteCharId = row.f_캐릭ID;
            }
            if (_cachedSprite != null)
            {
                var sr = player.GetComponentInChildren<SpriteRenderer>();
                if (sr != null) sr.sprite = _cachedSprite;
            }
        }

        // 1. 기초 스탯 적용 — PlayerStatManager를 통해 설정하면 StatPanelUI에도 반영됨
        if (sm != null)
        {
            sm.SetBase(StatType.MaxHP, baseHp);
            sm.SetBase(StatType.DEF,   baseDef);
            sm.SetBase(StatType.ATK,   baseAtk);
        }
        else
        {
            // fallback: PlayerStatManager 없을 때 직접 적용
            player.IncreaseMaxHP(baseHp - 100f);
            player.IncreaseDefense(baseDef);
            player.IncreaseAttack(baseAtk);
        }

        // 2. 증강 보너스 적용 (현재 단계까지 누적)
        int tier = GetAugmentTier(row.f_캐릭ID);
        if (tier >= 1) ApplyStatBonus(row.f_속성1ID, row.f_S1, baseHp, baseDef, baseAtk, player, stats, sm);
        if (tier >= 2) ApplyStatBonus(row.f_속성2ID, row.f_S2, baseHp, baseDef, baseAtk, player, stats, sm);
        if (tier >= 3) ApplyStatBonus(row.f_속성3ID, row.f_S3, baseHp, baseDef, baseAtk, player, stats, sm);

        // 3. 캐릭터 고유 스킬 등록
        string skillId = "";
        try { skillId = row.f_스킬ID ?? ""; }
        catch { Debug.LogWarning("[CharMgr] f_스킬ID 필드를 읽을 수 없습니다. BansheeGz에서 필드를 추가하고 코드를 재생성하세요."); }
        CharacterSkillManager.Shared?.Initialize(skillId);

        Debug.Log($"[CharMgr] 스탯 적용: {row.f_이름} (Tier {tier}, 스킬: {skillId})");
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 유틸
    // ─────────────────────────────────────────────────────────────

    /// <summary>등급 문자열 → 기초 체력 / 방어력 / 공격력</summary>
    public static void GetBaseStats(string grade,
        out float hp, out float def, out float atk)
    {
        switch (grade)
        {
            case "SS": hp = 300; def = 70; atk = 70; break;
            case "S":  hp = 250; def = 50; atk = 50; break;
            case "A":  hp = 200; def = 30; atk = 30; break;
            case "B":  hp = 150; def = 20; atk = 20; break;
            default:   hp = 100; def = 10; atk = 10; break; // C
        }
    }

    /// <summary>
    /// statId + valueStr을 PlayerController / PlayerStats에 적용합니다.
    /// 값은 기초 스탯 대비 퍼센트(%)로 해석합니다.
    /// </summary>
    private static void ApplyStatBonus(string statId, string valueStr,
        float baseHp, float baseDef, float baseAtk,
        PlayerController player, PlayerStats stats, PlayerStatManager sm)
    {
        if (string.IsNullOrEmpty(statId)) return;
        if (!float.TryParse(valueStr, out float pct)) return;

        switch (statId)
        {
            case "STAT_ATK":
                // SM을 통하면 StatPanelUI에도 반영됨
                if (sm != null) sm.AddPctMod(StatType.ATK, pct);
                else player.IncreaseAttack(baseAtk * pct / 100f);
                break;
            case "STAT_DEF":
                if (sm != null) sm.AddPctMod(StatType.DEF, pct);
                else player.IncreaseDefense(baseDef * pct / 100f);
                break;
            case "STAT_HP":
                if (sm != null) sm.AddPctMod(StatType.MaxHP, pct);
                else player.IncreaseMaxHP(baseHp * pct / 100f);
                break;
            case "STAT_SPD_ATK":
                player.IncreaseAttackSpeed(pct);          // % 직접 전달
                break;
            case "STAT_SPD_MOV":
                player.IncreaseMoveSpeed(player.MoveSpeedBase * pct / 100f);
                break;
            case "STAT_EVA_RATE":
                player.IncreaseDodge(pct / 100f);         // fraction
                break;
            case "STAT_CUR_REWARD":
                stats?.SetSynergyBonus(stats.GoldBonus + (int)pct, stats.ExpBonus);
                break;
            case "STAT_CUR_EXP":
                stats?.SetSynergyBonus(stats.GoldBonus, stats.ExpBonus + (int)pct);
                break;
            // 아래 항목들은 WeaponPoint/WeaponData 직접 연동이 필요합니다 (TODO)
            case "STAT_CRI_RATE":   // 치명타 확률 → WeaponPoint
            case "STAT_CRI_DMG":    // 치명타 데미지 → WeaponPoint
            case "STAT_ATK_RNG":    // 공격사거리 → WeaponPoint
            case "STAT_STUN":       // 적 둔화 → EnemyController
            case "STAT_DOT_DMG":    // 출혈 데미지 → WeaponPoint
            case "STAT_SKL_CD":     // 스킬 쿨다운 → 스킬 시스템
                Debug.Log($"[CharMgr] {statId} +{pct}% (WeaponPoint 연동 미구현)");
                break;
        }
    }
}
