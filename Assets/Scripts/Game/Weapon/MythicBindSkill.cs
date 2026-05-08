using UnityEngine;

/// <summary>
/// 신화 스킬: 보스 타격 시 일정 확률로 이동을 묶습니다 (그로기 아님).
/// 보스는 공격 가능 상태이지만 이동이 불가능합니다.
/// </summary>
public class MythicBindSkill : WeaponMythicSkill
{
    [SerializeField] private float chance       = 0.2f;
    [SerializeField] private float bindDuration = 2f;

    public override void OnBossHit(BossController boss, Vector2 hitDir)
    {
        if (boss == null || boss.IsMoveBound || boss.IsGrogy) return;
        if (Random.value > chance) return;
        boss.BindMovement(bindDuration);
    }
}
