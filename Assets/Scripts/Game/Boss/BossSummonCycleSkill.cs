using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 주기적 소환 사이클.
/// BossProjectileCycleSkill과 동일하게 executeInPattern=false로 독립 루프 실행.
///
/// summonInterval초마다 보스 주변에 summonCount개의 소환체를 스폰합니다.
/// 소환체(BossSummonMinion)는 minionDuration초 동안 플레이어를 추격하다 소멸합니다.
/// </summary>
public class BossSummonCycleSkill : BossSkillBase
{
    [Header("소환 프리팹")]
    [Tooltip("BossSummonMinion 컴포넌트가 없으면 자동으로 추가합니다.")]
    [SerializeField] private GameObject minionPrefab;

    [Header("소환 설정")]
    [SerializeField] private int   summonCount         = 2;    // 1회 소환 수
    [SerializeField] private float summonInterval      = 8f;   // 소환 주기 (초)
    [SerializeField] private float minionDuration      = 10f;  // 소환체 생존 시간 (초)
    [SerializeField] private float minionMoveSpeed     = 2.5f; // 소환체 이동 속도
    [SerializeField] private float minionContactDamage = 5f;   // 소환체 접촉 피해
    [SerializeField] private float spawnRadius         = 3f;   // 보스 주변 소환 반경

    [Header("애니메이션")]
    [SerializeField] private string summonTrigger = "tSummon"; // 소환 시 Animator 트리거

    public override void Init(BossController boss)
    {
        base.Init(boss);
        executeInPattern = false; // 독립 루프로 실행
        StartCoroutine(SummonLoop());
    }

    protected override System.Collections.IEnumerator OnExecute()
    {
        yield break;
    }

    private System.Collections.IEnumerator SummonLoop()
    {
        // 보스 낙하 연출 대기
        yield return new WaitUntil(() => Boss == null || !Boss.IsDropping);

        while (true)
        {
            // 게임오버 중엔 대기
            if (GameManager.Shared != null && GameManager.Shared.IsGameOver)
            {
                yield return new WaitUntil(() =>
                    GameManager.Shared == null || !GameManager.Shared.IsGameOver);
            }

            if (Boss == null) yield break;

            yield return new WaitForSeconds(summonInterval);

            if (Boss == null) yield break;

            SpawnMinions();
        }
    }

    private void SpawnMinions()
    {
        if (minionPrefab == null || Boss == null) return;

        Owner.EnemyAnimator?.SetTrigger(summonTrigger);

        float dmgMult = Boss.IsEnraged ? Boss.EnrageDamageMult : 1f;

        for (int i = 0; i < summonCount; i++)
        {
            float   angle  = Random.Range(0f, 360f);
            Vector2 offset = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad),
                                         Mathf.Sin(angle * Mathf.Deg2Rad)) * spawnRadius;
            Vector3 pos    = Boss.transform.position + (Vector3)offset;

            var go     = Instantiate(minionPrefab, pos, Quaternion.identity);
            var minion = go.GetComponent<BossSummonMinion>();
            if (minion == null) minion = go.AddComponent<BossSummonMinion>();
            minion.Init(minionDuration, minionMoveSpeed, minionContactDamage * dmgMult);
        }
    }
}
