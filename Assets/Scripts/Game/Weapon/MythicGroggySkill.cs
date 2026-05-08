using UnityEngine;

/// <summary>
/// 신화 스킬: 보스 타격 시 일정 확률로 그로기 상태 강제 발동.
/// </summary>
public class MythicGroggySkill : WeaponMythicSkill
{
    [SerializeField] private float chance       = 0.15f;
    [SerializeField] private float grogySeconds = 2f;

    public override void OnBossHit(BossController boss, Vector2 hitDir)
    {
        if (boss == null || boss.IsGrogy) return;
        if (Random.value > chance) return;
        boss.TriggerGroggy(grogySeconds);
    }
}
