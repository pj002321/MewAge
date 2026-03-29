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
        Vector2 startPos = (Vector2)transform.position + Vector2.left  * (rollDistance * 0.5f);
        Vector2 endPos   = (Vector2)transform.position + Vector2.right * (rollDistance * 0.5f);

        var go       = Instantiate(stonePrefab, startPos, Quaternion.identity);
        float travel = 0f;
        var hitSet   = new System.Collections.Generic.HashSet<EnemyController>();

        while (travel < rollDistance)
        {
            float step = rollSpeed * Time.deltaTime;
            go.transform.Translate(Vector2.right * step);
            travel += step;

            // 충돌 판정
            var cols = Physics2D.OverlapCircleAll(go.transform.position, radius);
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<EnemyController>(out var enemy)) continue;
                if (hitSet.Contains(enemy)) continue;  // 중복 히트 방지
                hitSet.Add(enemy);

                enemy.TakeDamage(mainValue);

                // 오른쪽으로 넉백
                var rb = enemy.GetComponent<Rigidbody2D>();
                if (rb != null)
                    rb.AddForce(Vector2.right * subValue, ForceMode2D.Impulse);
            }

            yield return null;
        }

        Destroy(go);
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
