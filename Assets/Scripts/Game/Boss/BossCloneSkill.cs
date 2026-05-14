using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// 보스 스킬: 분신 소환.
///
/// 보스 위치에서 분신을 한꺼번에 소환한 뒤, 양옆으로 펼쳐지는 연출을 재생합니다.
/// cloneSkills 에 같은 오브젝트의 BossSkillBase 컴포넌트를 드래그하면
/// 분신은 그 스킬들만 실행합니다. 비워두면 보스의 모든 스킬 그대로 사용.
/// </summary>
public class BossCloneSkill : BossSkillBase
{
    [Header("분신 스킬 선택")]
    [Tooltip("분신이 실행할 스킬 컴포넌트를 드래그. 비워두면 보스의 모든 스킬 사용.")]
    [SerializeField] private BossSkillBase[] cloneSkills;

    [Header("분신 설정")]
    [SerializeField] private int   cloneCount      = 2;
    [SerializeField] private float cloneHP         = 80f;
    [SerializeField] private float cloneContactDmg = 10f;
    [SerializeField] private float cloneMoveSpeed  = 3f;
    [SerializeField] private float cloneLifetime   = 15f;

    [Header("펼치기 연출")]
    [Tooltip("분신 사이 간격 (좌우 대칭으로 배치됩니다)")]
    [SerializeField] private float spreadSpacing  = 2.5f;
    [Tooltip("보스 위치에서 최종 위치까지 이동하는 시간 (초)")]
    [SerializeField] private float spreadDuration = 0.35f;

    [Header("분신 사망 VFX")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private float      deathVfxLifetime = 1f;

    protected override IEnumerator OnExecute()
    {
        if (Boss == null) yield break;

        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        // 분신이 사용할 스킬 타입 목록 (null = 모두)
        System.Type[] allowedTypes = cloneSkills != null && cloneSkills.Length > 0
            ? cloneSkills.Where(s => s != null).Select(s => s.GetType()).ToArray()
            : null;

        Vector3 bossPos = Boss.transform.position;

        // 최종 목표 위치 — 보스 X 기준 좌우 대칭 배치
        var targets = new Vector3[cloneCount];
        for (int i = 0; i < cloneCount; i++)
            targets[i] = bossPos + Vector3.right * (i - (cloneCount - 1) * 0.5f) * spreadSpacing;

        // 분신을 모두 보스 위치에 소환
        var cloneTransforms = new Transform[cloneCount];
        var clones          = new List<BossClone>(cloneCount);

        for (int i = 0; i < cloneCount; i++)
        {
            BossController.NextIsClone         = true;
            BossController.NextCloneSkillTypes = allowedTypes;

            var go      = Instantiate(Boss.gameObject, bossPos, Boss.transform.rotation);
            var cloneBC = go.GetComponent<BossController>();
            cloneBC?.ApplyCloneStats(cloneHP, cloneContactDmg, cloneMoveSpeed);

            var clone = go.AddComponent<BossClone>();
            clone.Init(cloneLifetime, deathVfxPrefab, deathVfxLifetime);

            cloneTransforms[i] = go.transform;
            clones.Add(clone);
        }

        // 보스 위치 → 목표 위치로 펼치기
        yield return SpreadRoutine(cloneTransforms, targets, spreadDuration);

        // 모든 분신이 사망할 때까지 대기 — 다 죽어야 다음 소환 가능
        yield return new WaitUntil(() => Boss == null || clones.TrueForAll(c => c == null));
    }

    private IEnumerator SpreadRoutine(Transform[] transforms, Vector3[] targets, float duration)
    {
        var starts = new Vector3[transforms.Length];
        for (int i = 0; i < transforms.Length; i++)
            starts[i] = transforms[i] != null ? transforms[i].position : targets[i];

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            for (int i = 0; i < transforms.Length; i++)
                if (transforms[i] != null)
                    transforms[i].position = Vector3.Lerp(starts[i], targets[i], t);
            yield return null;
        }

        for (int i = 0; i < transforms.Length; i++)
            if (transforms[i] != null)
                transforms[i].position = targets[i];
    }
}
