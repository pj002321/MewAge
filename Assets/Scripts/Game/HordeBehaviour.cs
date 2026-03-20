using UnityEngine;

/// <summary>
/// 떼거지형 — 맵 한쪽 끝에서 N개가 일렬로 나타나 반대쪽으로 직진합니다.
/// 맵 경계를 벗어나면 사라집니다.
/// 접촉 피해는 EnemyController 의 OnTrigger 에서 처리됩니다.
/// </summary>
public class HordeBehaviour : EnemyBehaviourBase
{
    [Header("떼거지 설정")]
    [Tooltip("한 번에 등장하는 총 개수 (원본 포함)")]
    [SerializeField] private int   splitCount    = 6;
    [Tooltip("맵 경계 바깥에서 스폰되는 여유 거리")]
    [SerializeField] private float spawnEdge     = 1f;
    [Tooltip("맵 경계 바깥으로 이 거리 이상 벗어나면 제거됩니다.")]
    [SerializeField] private float destroyMargin = 2f;

    private Vector2 moveDirection;
    private bool    isClone;

    public void SetAsClone(Vector2 dir)
    {
        isClone       = true;
        moveDirection = dir.normalized;
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

        var     spawner    = EnemySpawner.Shared;
        Vector2 aCenter    = spawner != null ? spawner.AreaCenter : Vector2.zero;
        Vector2 aHalf      = spawner != null ? spawner.AreaSize * 0.5f : new Vector2(5f, 9f);

        // 상/하/좌/우 중 랜덤으로 한 방향 선택
        int     side    = Random.Range(0, 4);
        Vector2 moveDir = side switch
        {
            0 => Vector2.down,
            1 => Vector2.up,
            2 => Vector2.right,
            _ => Vector2.left,
        };

        for (int i = 0; i < splitCount; i++)
        {
            // 선택한 축 위에서 균등 간격으로 배치 (-1 ~ 1)
            float t = splitCount > 1 ? (float)i / (splitCount - 1) * 2f - 1f : 0f;

            Vector2 spawnPos = side switch
            {
                0 => new Vector2(aCenter.x + t * aHalf.x, aCenter.y + aHalf.y + spawnEdge), // 상단 → 아래로
                1 => new Vector2(aCenter.x + t * aHalf.x, aCenter.y - aHalf.y - spawnEdge), // 하단 → 위로
                2 => new Vector2(aCenter.x - aHalf.x - spawnEdge, aCenter.y + t * aHalf.y), // 좌측 → 오른쪽으로
                _ => new Vector2(aCenter.x + aHalf.x + spawnEdge, aCenter.y + t * aHalf.y), // 우측 → 왼쪽으로
            };

            var clone = Instantiate(Owner.gameObject,
                                    new Vector3(spawnPos.x, spawnPos.y, Owner.transform.position.z),
                                    Quaternion.identity);

            clone.transform.localScale = targetScale;

            if (clone.TryGetComponent<EnemyController>(out var ec))
                ec.enabled = true;

            if (clone.TryGetComponent<HordeBehaviour>(out var hb))
                hb.SetAsClone(moveDir);
        }

        Destroy(Owner.gameObject);
    }

    // ── 이동: 클론만 동작 ───────────────────────────────────────
    public override void OnMoveFixedUpdate()
    {
        if (!isClone) return; // 분열 전(Start 이전) 프레임 대기

        Owner.MoveInDirection(moveDirection, Owner.MoveSpeed);

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
