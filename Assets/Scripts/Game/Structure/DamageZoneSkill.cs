using System.Collections;
using UnityEngine;

/// <summary>
/// 콜라이더 영역을 밟는 적에게 즉시 피해를 줍니다. (덩쿨 기물)
///
/// 처음 몬스터에 닿는 순간 Animator bool "bTriggerOn"을 true로 설정합니다.
/// 수명이 다하면 "bTriggerOn"을 false로 되돌리고 idle 클립을 역재생한 뒤 파괴됩니다.
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. _animator : 덩쿨 Animator 컴포넌트 연결.
///   3. _idleStateName : Animator 상태 이름 (기본값 "idle").
/// </summary>
public class DamageZoneSkill : StructureSkillBase
{
    [Header("사운드")]
    [SerializeField] private AudioClip activateSound;

    [Header("덩쿨 애니메이션")]
    [SerializeField] private Animator _animator;
    [SerializeField] private string   _idleStateName = "idle";

    private bool _triggered;

    public override void Activate() { }  // 트리거 충돌로 처리

    private void OnTriggerEnter2D(Collider2D other)
    {
        bool hit = false;

        if (other.GetComponentInParent<EnemyController>() is EnemyController enemy)
        {
            enemy.TakeDamage(mainValue);
            hit = true;
        }
        else if (other.GetComponentInParent<BossController>() is BossController boss)
        {
            boss.TakeDamage(mainValue);
            hit = true;
        }

        // 처음 몬스터에 닿을 때만 애니메이션 트리거 + 사운드
        if (hit && !_triggered)
        {
            _triggered = true;
            SoundManager.Shared?.PlayClip(activateSound);
            if (_animator != null)
                _animator.SetBool("bTriggerOn", true);
        }
    }

    /// <summary>수명 만료 시 idle 클립을 역재생하고 끝나면 파괴됩니다.</summary>
    public override IEnumerator OnPreDestroy()
    {
        if (_animator == null || !_triggered) yield break;

        // idle 상태로 복귀 신호
        _animator.SetBool("bTriggerOn", false);

        // idle 상태 끝(normalizedTime=1)에서 역방향 재생 시작
        _animator.Play(_idleStateName, 0, 1f);
        _animator.speed = -1f;

        // 한 프레임 대기 후 클립 길이 취득
        yield return null;
        float clipLength = _animator.GetCurrentAnimatorStateInfo(0).length;
        yield return new WaitForSeconds(clipLength);

        _animator.speed = 1f;
    }
}
