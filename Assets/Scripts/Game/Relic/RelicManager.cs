using UnityEngine;

/// <summary>
/// 게임 씬 전용. 장착된 유물의 효과를 PlayerStatManager와 캐시 프로퍼티에 반영합니다.
/// GameManager.InitAndStartRoutine()의 BeginBatch 블록 안에서 ApplyRelicStats()를 호출하세요.
/// </summary>
public class RelicManager : MonoBehaviourSingleton<RelicManager>
{
    protected override bool DontDestroy => false;

    // ── 무기 타입별 공격력 보너스 (%) ────────────────────────────────
    public float MeleeAtkBonusPct     { get; private set; }
    public float RangedAtkBonusPct    { get; private set; }
    public float MagicAtkBonusPct     { get; private set; }
    public float BoomerangAtkBonusPct { get; private set; }
    public float SpinAtkBonusPct      { get; private set; }

    // ── 보스 / 엘리트 관련 (0~1 범위) ────────────────────────────────
    public float BossDmgReduceTotal  { get; private set; }  // 받는 피해 감소
    public float BossAtkBonusPct     { get; private set; }  // 주는 피해 증가 (%)
    public float EliteDmgReduceTotal { get; private set; }  // 받는 피해 감소

    // ── 적용 ─────────────────────────────────────────────────────────

    /// <summary>
    /// 유물 효과를 PlayerStatManager(배치 중)와 캐시에 반영합니다.
    /// GameManager의 BeginBatch / EndBatch 사이에서 호출해야 합니다.
    /// </summary>
    public void ApplyRelicStats()
    {
        MeleeAtkBonusPct     = 0f;
        RangedAtkBonusPct    = 0f;
        MagicAtkBonusPct     = 0f;
        BoomerangAtkBonusPct = 0f;
        SpinAtkBonusPct      = 0f;
        BossDmgReduceTotal   = 0f;
        BossAtkBonusPct      = 0f;
        EliteDmgReduceTotal  = 0f;

        var inv = RelicInventory.Shared;
        if (inv == null) return;

        foreach (var relic in inv.GetAllOwned())
        {
            int level = inv.GetLevel(relic.relicId);
            ApplyEffect(relic.primaryEffect,   relic.GetPrimaryValue(level));
            ApplyEffect(relic.secondaryEffect, relic.GetSecondaryValue(level));
        }

        // 피해 감소 최대 75% 제한
        BossDmgReduceTotal  = Mathf.Clamp(BossDmgReduceTotal,  0f, 0.75f);
        EliteDmgReduceTotal = Mathf.Clamp(EliteDmgReduceTotal, 0f, 0.75f);
    }

    private void ApplyEffect(RelicEffectType effect, float value)
    {
        if (effect == RelicEffectType.NONE || value == 0f) return;

        var statType = RelicData.EffectToStatType(effect);
        if (statType.HasValue)
        {
            PlayerStatManager.Shared?.AddPctMod(statType.Value, value);
            return;
        }

        switch (effect)
        {
            case RelicEffectType.MELEE_ATK:     MeleeAtkBonusPct     += value; break;
            case RelicEffectType.RANGED_ATK:    RangedAtkBonusPct    += value; break;
            case RelicEffectType.MAGIC_ATK:     MagicAtkBonusPct     += value; break;
            case RelicEffectType.BOOMERANG_ATK: BoomerangAtkBonusPct += value; break;
            case RelicEffectType.SPIN_ATK:      SpinAtkBonusPct      += value; break;
            case RelicEffectType.BOSS_DMG:      BossDmgReduceTotal   += value / 100f; break;
            case RelicEffectType.BOSS_ATK:      BossAtkBonusPct      += value; break;
            case RelicEffectType.ELITE_DMG:     EliteDmgReduceTotal  += value / 100f; break;
        }
    }
}
