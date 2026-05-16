using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 스킬: 분신 소환.
///
/// 보스 위치에서 분신을 한꺼번에 소환한 뒤 양옆으로 펼치는 연출을 재생합니다.
/// 모든 분신이 사망할 때까지 대기 후 스킬이 종료됩니다.
///
/// cloneSkillTypeNames: 분신이 사용할 스킬 클래스 이름을 입력합니다.
///   예) "BossProjectileSkill", "BossChargeSkill"
///   비워두면 보스의 모든 스킬을 그대로 사용합니다.
/// </summary>
[System.Serializable]
public class BossCloneSkill : BossSkillBase
{
    [Header("분신 스킬 선택")]
    [Tooltip("분신이 실행할 스킬 클래스 이름. 비워두면 보스의 모든 스킬 사용.\n예: BossProjectileSkill")]
    [SerializeField] private string[] cloneSkillTypeNames = new string[0];

    [Header("분신 설정")]
    [SerializeField] private int   cloneCount      = 2;
    [SerializeField] private float cloneHP         = 80f;
    [SerializeField] private float cloneContactDmg = 10f;
    [SerializeField] private float cloneMoveSpeed  = 3f;
    [SerializeField] private float cloneLifetime   = 15f;

    [Header("펼치기 연출")]
    [Tooltip("분신 사이 간격 (좌우 대칭 배치)")]
    [SerializeField] private float spreadSpacing  = 2.5f;
    [Tooltip("보스 위치 → 최종 위치 이동 시간")]
    [SerializeField] private float spreadDuration = 0.35f;

    [Header("분신 사망 VFX")]
    [SerializeField] private GameObject deathVfxPrefab;
    [SerializeField] private float      deathVfxLifetime = 1f;

    protected override async UniTask OnExecuteAsync(CancellationToken ct)
    {
        if (Boss == null) return;

        Boss.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);

        // 분신이 실행할 스킬 타입 목록 (null = 제한 없음)
        System.Type[] allowedTypes = cloneSkillTypeNames.Length > 0
            ? cloneSkillTypeNames
                .Select(n => System.Type.GetType(n))
                .Where(t => t != null)
                .ToArray()
            : null;

        Vector3 bossPos = Boss.transform.position;

        // 좌우 대칭 목표 위치 계산
        var targets = new Vector3[cloneCount];
        for (int i = 0; i < cloneCount; i++)
            targets[i] = bossPos + Vector3.right * (i - (cloneCount - 1) * 0.5f) * spreadSpacing;

        // 분신 소환 (모두 보스 위치에서 시작)
        var cloneTransforms = new Transform[cloneCount];
        var clones          = new List<BossClone>(cloneCount);

        for (int i = 0; i < cloneCount; i++)
        {
            BossController.NextIsClone         = true;
            BossController.NextCloneSkillTypes = allowedTypes;

            var go      = Object.Instantiate(Boss.gameObject, bossPos, Boss.transform.rotation);
            var cloneBC = go.GetComponent<BossController>();
            cloneBC?.ApplyCloneStats(cloneHP, cloneContactDmg, cloneMoveSpeed);

            var clone = go.AddComponent<BossClone>();
            clone.Init(cloneLifetime, deathVfxPrefab, deathVfxLifetime);

            cloneTransforms[i] = go.transform;
            clones.Add(clone);
        }

        // 보스 위치 → 목표 위치로 부드럽게 펼치기
        await SpreadAsync(cloneTransforms, targets, spreadDuration, ct);

        // 모든 분신이 사망할 때까지 대기 (다 죽어야 다음 소환 가능)
        await UniTask.WaitUntil(
            () => Boss == null || clones.TrueForAll(c => c == null),
            cancellationToken: ct)
            .SuppressCancellationThrow();
    }

    private async UniTask SpreadAsync(
        Transform[] transforms, Vector3[] targets, float duration, CancellationToken ct)
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

            bool cancelled = await UniTask.Yield(ct).SuppressCancellationThrow();
            if (cancelled) return;
        }

        for (int i = 0; i < transforms.Length; i++)
            if (transforms[i] != null)
                transforms[i].position = targets[i];
    }
}
