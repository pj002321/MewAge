using System.Collections;
using UnityEngine;

/// <summary>
/// 메테오 기물 스킬.
/// 설치 위치를 기준으로 사선(우상→좌하)으로 meteorsCount개의 메테오 프리팹을 떨어뜨립니다.
/// 메테오가 착지 시 mainValue 피해를 radius 범위 내 적에게 줍니다.
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. meteorPrefab : 사선으로 날아가는 돌 프리팹 (SpriteRenderer + Rigidbody2D Kinematic).
///   3. impactRadius : 착지 충격 범위.
///   4. meteorInterval : 메테오 사이 간격(초).
///   5. meteorsCount : 총 메테오 수.
///   6. spreadRange : 착지 지점 좌우 랜덤 범위.
/// </summary>
public class MeteorSkill : StructureSkillBase
{
    [Header("메테오 설정")]
    [SerializeField] private GameObject meteorPrefab;
    [SerializeField] private int        meteorsCount   = 5;
    [SerializeField] private float      meteorInterval = 0.4f;
    [SerializeField] private float      impactRadius   = 1.2f;
    [SerializeField] private float      spreadRange    = 3f;     // 착지 지점 좌우 분산
    [SerializeField] private float      fallHeight     = 8f;     // 출발 높이 오프셋
    [SerializeField] private float      fallDuration   = 0.5f;   // 각 메테오 낙하 시간

    public override void Activate() => StartCoroutine(MeteorRoutine());

    IEnumerator MeteorRoutine()
    {
        if (meteorPrefab == null)
        {
            this.DError("[MeteorSkill] meteorPrefab이 연결되지 않았습니다.");
            yield break;
        }

        var wait = new WaitForSeconds(meteorInterval);
        for (int i = 0; i < meteorsCount; i++)
        {
            // 착지 목표: MovementBounds 내 랜덤 위치 (없으면 기물 주변 fallback)
            Vector2 landPos = GetRandomPointInBounds();
            // 출발 위치: 착지 지점 우상단
            Vector2 startPos = landPos + new Vector2(fallHeight * 0.7f, fallHeight);

            StartCoroutine(FallRoutine(startPos, landPos));
            yield return wait;
        }
    }

    Vector2 GetRandomPointInBounds()
    {
        var bounds = MovementBounds.Shared;
        if (bounds == null || bounds.PointCount < 3)
            return (Vector2)transform.position + new Vector2(Random.Range(-spreadRange, spreadRange), 0f);

        // Bounds의 AABB를 구한 뒤 rejection sampling으로 폴리곤 내부 점 선택
        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        for (int i = 0; i < bounds.PointCount; i++)
        {
            Vector2 w = bounds.GetWorld(i);
            if (w.x < minX) minX = w.x;
            if (w.x > maxX) maxX = w.x;
            if (w.y < minY) minY = w.y;
            if (w.y > maxY) maxY = w.y;
        }

        for (int attempt = 0; attempt < 30; attempt++)
        {
            Vector2 candidate = new(Random.Range(minX, maxX), Random.Range(minY, maxY));
            if (bounds.IsInside(candidate)) return candidate;
        }

        // 30번 시도 실패 시 중심 반환
        return new Vector2((minX + maxX) * 0.5f, (minY + maxY) * 0.5f);
    }

    IEnumerator FallRoutine(Vector2 from, Vector2 to)
    {
        var go = Instantiate(meteorPrefab, from, Quaternion.identity);
        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            go.transform.position = Vector2.Lerp(from, to, t);
            yield return null;
        }

        go.transform.position = to;

        // 착지 충격
        var hits = Physics2D.OverlapCircleAll(to, impactRadius);
        foreach (var col in hits)
            if (col.TryGetComponent<EnemyController>(out var enemy))
                enemy.TakeDamage(mainValue);

        Destroy(go);
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, spreadRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, impactRadius);
    }
}
