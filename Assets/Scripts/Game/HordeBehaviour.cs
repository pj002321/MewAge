using UnityEngine;

/// <summary>
/// 떼거지형 — 스폰 시 자신을 N개로 복제해 원형으로 사방에 날립니다.
/// 복제본은 각자 지정된 방향으로 직진하다 맵 밖을 벗어나면 사라집니다.
/// 접촉 피해는 EnemyController 의 OnTrigger 에서 처리됩니다.
/// </summary>
public class HordeBehaviour : EnemyBehaviourBase
{
    [Header("떼거지 설정")]
    [Tooltip("분열할 총 개수 (원본 포함)")]
    [SerializeField] private int   splitCount    = 6;
    [Tooltip("맵 경계 바깥으로 이 거리 이상 벗어나면 제거됩니다.")]
    [SerializeField] private float destroyMargin = 2f;

    private Vector2 moveDirection;
    private bool    isClone; // 클론은 Start에서 재분열하지 않음

    /// <summary>클론 생성 직후 방향을 지정합니다. Awake/Init 이후 첫 프레임 Start 전에 호출됩니다.</summary>
    public void SetAsClone(Vector2 dir)
    {
        isClone       = true;
        moveDirection = dir.normalized;
    }

    // ── Start: 원본만 분열 ──────────────────────────────────────
    private void Start()
    {
        if (isClone) return; // 클론은 분열 없이 직진만

        Burst();
    }

    private void Burst()
    {
        Vector3 targetScale = Owner.SpawnScale; // Destroy 전에 미리 캡처

        for (int i = 0; i < splitCount; i++)
        {
            float   angle = 360f / splitCount * i;
            Vector2 dir   = new Vector2(
                Mathf.Cos(angle * Mathf.Deg2Rad),
                Mathf.Sin(angle * Mathf.Deg2Rad));

            var clone = Instantiate(Owner.gameObject,
                                    Owner.Rb.position,       // rb.position 기준으로 통일
                                    Quaternion.identity);

            clone.transform.localScale = targetScale;

            // Owner가 BounceIn 중이면 EnemyController.enabled=false가 복사됨 → 강제 활성화
            if (clone.TryGetComponent<EnemyController>(out var ec))
                ec.enabled = true;

            if (clone.TryGetComponent<HordeBehaviour>(out var hb))
                hb.SetAsClone(dir);
        }

        // 원본은 역할 끝 — 조용히 제거 (사망 연출 없이)
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
