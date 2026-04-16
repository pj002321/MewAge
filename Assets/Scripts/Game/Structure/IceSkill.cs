using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 얼음 기물 스킬.
/// 설치 위치 radius 범위 내 적을 subValue초 동안 빙결(정지)시킵니다.
/// 빙결 중 적의 SpriteRenderer가 파란색으로 변하고,
/// 빙결 해제 시 mainValue 피해를 입힙니다.
/// 각 적 위치에 VFX를 스폰하며, 빙결이 끝날 때 VFX를 역재생 후 파괴합니다.
///
/// 빙결 효과는 다른 기물에도 재사용할 수 있습니다:
///   IceSkill.FreezeEnemy(enemy, duration, damage) 정적 메서드 사용.
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. radius   : 빙결 범위
///   3. subValue : 빙결 지속시간(초)
///   4. mainValue: 빙결 해제 후 피해량
///   5. tickInterval: 재발동 간격(0이면 1회성)
///   6. freezeVfxPrefab: 적 위치에 스폰할 VFX 프리팹 (Animator 포함)
/// </summary>
public class IceSkill : StructureSkillBase
{
    [Header("얼음 설정")]
    [SerializeField] private Color  freezeColor   = new Color(0.4f, 0.7f, 1f, 1f);
    [SerializeField] private bool   repeat        = false;  // true면 tickInterval마다 반복

    [Header("VFX 설정")]
    [SerializeField] private GameObject freezeVfxPrefab;
    [Tooltip("적 pivot 기준 VFX 스폰 오프셋 (유닛)")]
    [SerializeField] private Vector2 vfxOffset = Vector2.zero;

    public override void Activate()
    {
        StartCoroutine(IceRoutine());
        if (repeat) StartCoroutine(RepeatRoutine());
    }

    IEnumerator IceRoutine()
    {
        var enemies = EnemyController.All;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            enemy.Freeze(subValue, mainValue, freezeColor);

            // 적 자식으로 VFX 스폰 (위치 추적) → 부모 회전은 WorldRotationLock으로 무시
            if (freezeVfxPrefab != null)
                StartCoroutine(FreezeVfxRoutine(enemy.transform, subValue));
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

    /// <summary>적 자식으로 VFX를 스폰하고 duration 후 역재생하며 파괴합니다.</summary>
    IEnumerator FreezeVfxRoutine(Transform enemyTransform, float duration)
    {
        var vfxObj = Instantiate(freezeVfxPrefab, enemyTransform.position + (Vector3)vfxOffset, Quaternion.identity, enemyTransform);
        vfxObj.AddComponent<WorldRotationLock>();   // 부모 회전 무시
        var anim   = vfxObj.GetComponent<Animator>();

        yield return new WaitForSeconds(duration);

        if (vfxObj == null) yield break;

        // VFX 역재생 (현재 상태 끝→처음)
        if (anim != null)
        {
            var state = anim.GetCurrentAnimatorStateInfo(0);
            anim.Play(state.shortNameHash, 0, 1f);
            anim.speed = -1f;

            yield return null;
            float clipLength = anim.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(clipLength);
        }

        if (vfxObj != null) Destroy(vfxObj);
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
