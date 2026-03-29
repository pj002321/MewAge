using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 얼음 기물 스킬.
/// 설치 위치 radius 범위 내 적을 subValue초 동안 빙결(정지)시킵니다.
/// 빙결 중 적의 SpriteRenderer가 파란색으로 변하고,
/// 빙결 해제 시 mainValue 피해를 입힙니다.
///
/// 빙결 효과는 다른 기물에도 재사용할 수 있습니다:
///   IceSkill.FreezeEnemy(enemy, duration, damage) 정적 메서드 사용.
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. radius  : 빙결 범위
///   3. subValue: 빙결 지속시간(초)
///   4. mainValue: 빙결 해제 후 피해량
///   5. tickInterval: 재발동 간격(0이면 1회성)
/// </summary>
public class IceSkill : StructureSkillBase
{
    [Header("얼음 설정")]
    [SerializeField] private Color  freezeColor   = new Color(0.4f, 0.7f, 1f, 1f);
    [SerializeField] private bool   repeat        = false;  // true면 tickInterval마다 반복

    public override void Activate()
    {
        StartCoroutine(IceRoutine());
        if (repeat) StartCoroutine(RepeatRoutine());
    }

    IEnumerator IceRoutine()
    {
        var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.Freeze(subValue, mainValue, freezeColor);
            }
        }
        yield break;
    }

    IEnumerator RepeatRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;
            StartCoroutine(IceRoutine());
        }
    }



    // ── 외부 재사용 API ─────────────────────────────────────────────
    /// <summary>다른 스킬에서도 호출할 수 있는 단일 적 빙결 코루틴 래퍼.</summary>
    public static void FreezeEnemy(MonoBehaviour caller, EnemyController enemy,
        float duration, float damageOnThaw, Color color)
    {
        if (enemy == null) return;
        enemy.Freeze(duration, damageOnThaw, color);
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
