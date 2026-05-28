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

    [Header("모델 데칼")]
    [SerializeField] private GameObject decalPrefab;

    private GameObject _currentPlayerModel;

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

    /// <summary>장착된 캐릭터가 없으면 첫 번째 보유 캐릭터를 자동 장착합니다.</summary>
    public void EnsureDefaultCharacter()
    {
        if (!string.IsNullOrEmpty(EquippedCharID)) return;

        DB_캐릭터 first = null;
        DB_캐릭터.ForEachEntity(row =>
        {
            if (first == null && row.f_보유여부)
                first = row;
        });

        if (first != null)
        {
            PlayerPrefs.SetString(PrefKeyChar, first.f_캐릭ID);
            PlayerPrefs.Save();
            Debug.Log($"[CharMgr] 기본 캐릭터 자동 장착: {first.f_이름}");
        }
        else
        {
            Debug.LogWarning("[CharMgr] 보유 캐릭터가 없어 기본 장착 불가");
        }
    }

    public const int MaxAugmentTier = 3;

    /// <summary>캐릭터별 현재 증강 단계 (0~3). 0 = 모두 잠금.</summary>
    public int GetAugmentTier(string charID)
        => PlayerPrefs.GetInt(string.Format(PrefKeyTier, charID), 0);

    /// <summary>증강 단계를 변경합니다.</summary>
    public void SetAugmentTier(string charID, int tier)
    {
        PlayerPrefs.SetInt(string.Format(PrefKeyTier, charID), Mathf.Clamp(tier, 0, MaxAugmentTier));
        PlayerPrefs.Save();
    }

    // ─────────────────────────────────────────────────────────────
    // 스탯 적용 (게임 씬 시작 시 호출)
    // ─────────────────────────────────────────────────────────────

    /// <summary>장착된 캐릭터의 기초 스탯 + 증강 보너스를 PlayerController에 적용합니다.</summary>
    public void ApplyCharacterStats()
    {
        string skillId = "";  // 캐릭터 미장착/오류 시에도 Initialize는 반드시 호출

        EnsureDefaultCharacter(); // 장착 캐릭터 없으면 첫 보유 캐릭터 자동 장착

        string charID = EquippedCharID;
        if (string.IsNullOrEmpty(charID))
        {
            Debug.LogWarning("[CharMgr] 장착된 캐릭터 없음 (보유 캐릭터 없음)");
            CharacterSkillManager.Shared?.Initialize(skillId);
            return;
        }

        var row = DB_캐릭터.FindEntity(r => r.f_캐릭ID == charID);
        if (row == null || !row.f_보유여부)
        {
            Debug.LogWarning($"[CharMgr] 장착 캐릭터를 찾을 수 없음: {charID}");
            CharacterSkillManager.Shared?.Initialize(skillId);
            return;
        }

        float baseHp  = row.f_기초체력;
        float baseDef = row.f_기초방어력;
        float baseAtk = row.f_기초공격력;

        var player = PlayerController.Instance;
        var stats  = PlayerStats.Shared;
        var sm     = PlayerStatManager.Shared;
        if (player == null)
        {
            Debug.LogWarning("[CharMgr] PlayerController.Instance 가 null — 스탯 적용 실패");
            CharacterSkillManager.Shared?.Initialize(skillId);
            return;
        }

        // 0. 인게임 모델 교체 (Resources/Character/{charId}_model.prefab)
        SwapPlayerModel(row.f_캐릭ID, player);

        // 1. 기초 스탯 적용 — PlayerStatManager를 통해 설정하면 StatPanelUI에도 반영됨
        if (sm != null)
        {
            sm.SetBase(StatType.MaxHP,     baseHp);
            sm.SetBase(StatType.DEF,       baseDef);
            sm.SetBase(StatType.ATK,       baseAtk);
            sm.SetBase(StatType.MoveSpeed, row.f_기초이속);
            sm.SetBase(StatType.AtkSpeed,  row.f_기초공속);
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

        // 3. 캐릭터 고유 스킬 등록 (항상 호출)
        try { skillId = row.f_스킬ID ?? ""; }
        catch { Debug.LogWarning("[CharMgr] f_스킬ID 필드를 읽을 수 없습니다. BansheeGz에서 필드를 추가하고 코드를 재생성하세요."); }
        CharacterSkillManager.Shared?.Initialize(skillId);

        Debug.Log($"[CharMgr] 스탯 적용: {row.f_이름} (Tier {tier}, 스킬: {skillId})");
    }

    // ─────────────────────────────────────────────────────────────
    // 모델 교체
    // ─────────────────────────────────────────────────────────────

    /// <summary>
    /// 인게임 Player의 VisualSlot 자식에 캐릭터 모델 프리팹을 교체합니다.
    /// 프리팹 경로: Resources/Character/{charId}_model
    /// 프리팹 구성: SpriteRenderer + Animator (AnimatorController 캐릭터별)
    /// </summary>
    void SwapPlayerModel(string charId, PlayerController player)
    {
        // VisualSlot 확보 — Inspector 할당 우선, 없으면 이름 탐색, 그래도 없으면 생성
        var visualSlot = player.VisualSlot
                      ?? player.transform.Find("VisualSlot");
        if (visualSlot == null)
        {
            var go = new GameObject("VisualSlot");
            go.transform.SetParent(player.transform, false);
            visualSlot = go.transform;
        }

        // 기존 모델 제거
        if (_currentPlayerModel != null) Destroy(_currentPlayerModel);
        foreach (Transform child in visualSlot) Destroy(child.gameObject);

        // 모델 프리팹 로드 및 배치
        var prefab = Resources.Load<GameObject>($"Character/{charId.ToLower()}_model");
        if (prefab == null)
        {
            Debug.LogWarning($"[CharMgr] 모델 프리팹 없음: Character/{charId.ToLower()}_model");
            return;
        }

        _currentPlayerModel = Instantiate(prefab, visualSlot);
        _currentPlayerModel.transform.localPosition = Vector3.zero;
        _currentPlayerModel.transform.localScale    = Vector3.one;

        if (decalPrefab != null)
        {
            var decal = Instantiate(decalPrefab, _currentPlayerModel.transform);
            decal.transform.localScale = Vector3.one;

            var col = player.GetComponent<Collider2D>()
                   ?? player.GetComponentInChildren<Collider2D>();
            if (col != null)
            {
                float worldMinY = col.bounds.min.y;
                float localY    = _currentPlayerModel.transform.InverseTransformPoint(0f, worldMinY, 0f).y;
                decal.transform.localPosition = new Vector3(0f, localY, 0f);
            }
            else
            {
                decal.transform.localPosition = Vector3.zero;
            }
        }

        // PlayerController 참조 갱신
        player.RefreshVisualReferences();
        var anim = _currentPlayerModel.GetComponent<Animator>()
                ?? _currentPlayerModel.GetComponentInChildren<Animator>();
        if (anim != null) player.SetAnimator(anim);

        // 모델 교체 후 SlimeBobAnimation 재초기화 (Awake 시점엔 모델이 없었을 수 있음)
        var slimeBob = visualSlot.GetComponentInChildren<SlimeBobAnimation>();
        slimeBob?.Reinitialize();
    }

    // ─────────────────────────────────────────────────────────────
    // 내부 유틸
    // ─────────────────────────────────────────────────────────────


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
            case "STAT_CUR_EXP":
                // 시너지 골드/경험치 보너스 시스템 제거됨 — TODO: 새 시너지 효과 적용
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
