using UnityEngine;

/// <summary>
/// 떼거지형 — 우측 상단/하단 코너에서 원형으로 뭉쳐 스폰되어
/// 플레이어를 향해 빠르게 돌진합니다.
/// 맵 경계를 벗어나면 사라집니다.
/// </summary>
public class HordeBehaviour : EnemyBehaviourBase
{
    [Header("떼거지 설정")]
    [Tooltip("한 번에 등장하는 총 개수 (원본 포함)")]
    [SerializeField] private int   splitCount    = 6;
    [Tooltip("원형 클러스터 반지름")]
    [SerializeField] private float clusterRadius = 0.6f;
    [Tooltip("맵 경계 바깥에서 스폰되는 여유 거리")]
    [SerializeField] private float spawnEdge     = 1f;
    [Tooltip("맵 경계 바깥으로 이 거리 이상 벗어나면 제거됩니다.")]
    [SerializeField] private float destroyMargin = 2f;

    private bool isClone;

    public void SetAsClone()
    {
        isClone = true;
    }

    // ── Start: 원본만 분열 ──────────────────────────────────────
    private void Start()
    {
        if (isClone) return;
        Burst();
    }

    private void Burst()
    {
        Vector3 targetScale = Owner.SpawnScale;

        var     spawner = EnemySpawner.Shared;
        Vector2 aCenter = spawner != null ? spawner.AreaCenter : Vector2.zero;
        Vector2 aHalf   = spawner != null ? spawner.AreaSize * 0.5f : new Vector2(5f, 9f);

        // 우측 상단 또는 우측 하단 코너 중 랜덤 선택
        bool isUpper = Random.value > 0.5f;
        Vector2 cornerPos = isUpper
            ? new Vector2(aCenter.x + aHalf.x + spawnEdge, aCenter.y + aHalf.y + spawnEdge)
            : new Vector2(aCenter.x + aHalf.x + spawnEdge, aCenter.y - aHalf.y - spawnEdge);

        for (int i = 0; i < splitCount; i++)
        {
            // 원형으로 균등 배치
            float   angle    = (float)i / splitCount * 360f * Mathf.Deg2Rad;
            Vector2 offset   = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * clusterRadius;
            Vector2 spawnPos = cornerPos + offset;

            var clone = Instantiate(Owner.gameObject,
                                    new Vector3(spawnPos.x, spawnPos.y, Owner.transform.position.z),
                                    Quaternion.identity);

            clone.transform.localScale = targetScale;

            if (clone.TryGetComponent<EnemyController>(out var ec))
                ec.enabled = true;

            if (clone.TryGetComponent<HordeBehaviour>(out var hb))
                hb.SetAsClone();
        }

        Destroy(Owner.gameObject);
    }

    // ── 이동: 클론만 동작 — 플레이어를 향해 직진 ───────────────
    public override void OnMoveFixedUpdate()
    {
        if (!isClone) return;

        Owner.MoveTowardPlayer(Owner.MoveSpeed);

        // 떼거지 스프라이트 기본 방향이 반대이므로 flipX 반전
        var sr = Owner.GetComponent<UnityEngine.SpriteRenderer>();
        if (sr != null) sr.flipX = !sr.flipX;

        // 맵 경계 + 여유 거리를 벗어나면 제거
        var spawner = EnemySpawner.Shared;
        if (spawner == null) return;

        Vector2 pos    = Owner.Rb.position;
        Vector2 center = spawner.AreaCenter;
        Vector2 half   = spawner.AreaSize * 0.5f + Vector2.one * destroyMargin;

        if (pos.x < center.x - half.x || pos.x > center.x + half.x ||
            pos.y < center.y - half.y || pos.y > center.y + half.y)
        {
            Destroy(Owner.gameObject);
        }
    }
}
