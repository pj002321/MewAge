using System.Collections;
using UnityEngine;

/// <summary>
/// 돌벼락 기물 스킬.
/// 돌 프리팹을 설치 위치 왼쪽 끝에서 오른쪽으로 굴립니다.
/// 굴러가는 방향으로 맞은 적들이 밀려납니다(Rigidbody2D AddForce).
/// mainValue : 피해량
/// subValue  : 넉백 힘
/// radius    : 돌 충돌 감지 반경 (OverlapCircle 반경)
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. stonePrefab : 굴러가는 돌 프리팹 (SpriteRenderer만 있어도 됨, Rigidbody2D 불필요).
///   3. rollSpeed   : 굴러가는 속도(units/s).
///   4. rollDistance: 총 이동 거리.
/// </summary>
public class StoneThunderSkill : StructureSkillBase
{
    [Header("돌벼락 설정")]
    [SerializeField] private GameObject stonePrefab;
    [SerializeField] private float      rollSpeed    = 6f;
    [SerializeField] private float      rollDistance = 12f;

    public override void Activate()
    {
        if (stonePrefab == null)
        {
            this.DError("[StoneThunderSkill] stonePrefab이 연결되지 않았습니다.");
            return;
        }
        StartCoroutine(RollRoutine());
    }

    IEnumerator RollRoutine()
    {
        // 보이지 않는 스윕 포인트가 왼→오 이동하며 맞은 적 위에 stonePrefab을 자식으로 스폰.
        // 적이 죽으면 자식 오브젝트도 함께 소멸됨.
        Vector2 sweepPos = (Vector2)transform.position + Vector2.left * (rollDistance * 0.5f);
        float   travel   = 0f;
        var     hitSet   = new System.Collections.Generic.HashSet<EnemyController>();

        while (travel < rollDistance)
        {
            float step = rollSpeed * Time.deltaTime;
            sweepPos += Vector2.right * step;
            travel   += step;

            // 충돌 판정
            var cols = Physics2D.OverlapCircleAll(sweepPos, radius);
            bool bossHit = false;
            foreach (var col in cols)
            {
                var enemy = col.GetComponentInParent<EnemyController>();
                if (enemy != null)
                {
                    if (hitSet.Contains(enemy)) continue;
                    hitSet.Add(enemy);

                    enemy.TakeDamage(mainValue);

                    var hitGo = Instantiate(stonePrefab, enemy.transform.position, Quaternion.identity);
                    hitGo.transform.SetParent(enemy.transform, true);
                    Destroy(hitGo, 2f);

                    var rb = enemy.GetComponent<Rigidbody2D>();
                    if (rb != null)
                        rb.AddForce(Vector2.right * subValue, ForceMode2D.Impulse);
                    continue;
                }

                if (!bossHit && col.GetComponentInParent<BossController>() is BossController boss)
                {
                    bossHit = true;
                    boss.TakeDamage(mainValue);
                }
            }

            yield return null;
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.6f, 0.4f, 0.1f, 0.5f);
        Vector3 left  = transform.position + Vector3.left  * rollDistance * 0.5f;
        Vector3 right = transform.position + Vector3.right * rollDistance * 0.5f;
        Gizmos.DrawLine(left, right);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
