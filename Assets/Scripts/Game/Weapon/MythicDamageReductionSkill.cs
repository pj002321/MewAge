using UnityEngine;

/// <summary>
/// 신화 스킬 (패시브): 장착 시 보스에게 받는 피해를 줄입니다.
/// PlayerController.TakeDamage에서 ActiveReduction을 참조합니다.
/// </summary>
public class MythicDamageReductionSkill : WeaponMythicSkill
{
    [SerializeField] private float reductionPct = 0.2f; // 20% 피해 감소

    public static float ActiveReduction { get; private set; }

    public override void OnEquip()  => ActiveReduction = reductionPct;
    private void OnDestroy()        => ActiveReduction = 0f;

    // 보스 타격 시 별도 발동 없음 (패시브)
    public override void OnBossHit(BossController boss, Vector2 hitDir) { }
}
