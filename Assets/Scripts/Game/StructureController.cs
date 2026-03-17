using System.Collections;
using UnityEngine;

/// <summary>
/// 설치된 기물의 동작을 담당합니다.
/// 프리팹에 컴포넌트를 붙이고 Inspector에서 타입과 수치를 설정하세요.
/// </summary>
public class StructureController : MonoBehaviour
{
    public enum StructureType
    {
        Paralyze,    // 범위 내 적 주기적으로 마비
        DamageZone,  // 밟으면 즉시 피해
        DoT,         // 범위 내 적에게 도트 피해
        HealZone,    // 범위 내 플레이어 주기적 체력 회복
        Fence,       // 설치 시 3개 연속으로 일렬 배치
    }

    [Header("기물 타입")]
    [SerializeField] private StructureType structureType;

    [Header("수치")]
    [SerializeField] private float mainValue    = 10f;   // 피해량 / 회복량
    [SerializeField] private float subValue     = 1f;    // 마비 지속시간 등 보조값
    [SerializeField] private float duration     = 15f;   // 기물 유지 시간 (0 = 영구)
    [SerializeField] private float radius       = 1.5f;  // 범위
    [SerializeField] private float tickInterval = 1f;    // 틱 간격

    [Header("Fence 설정")]
    [SerializeField] private float fenceSpacing = 0.8f;  // 펜스 간격

    // Fence가 추가 스폰한 자식 여부 (무한 재귀 방지)
    private bool isFenceChild;

    private void Start()
    {
        if (duration > 0f)
            Destroy(gameObject, duration);

        switch (structureType)
        {
            case StructureType.DoT:
            case StructureType.Paralyze:
                StartCoroutine(TickRoutine());
                break;
            case StructureType.HealZone:
                StartCoroutine(HealRoutine());
                break;
            case StructureType.Fence:
                if (!isFenceChild) SpawnFenceLine();
                break;
        }
    }

    // ── DamageZone — 밟으면 피해 ─────────────────────────────────
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (structureType != StructureType.DamageZone) return;
        if (other.TryGetComponent<EnemyController>(out var enemy))
            enemy.TakeDamage(mainValue);
    }

    // ── DoT / Paralyze — 주기적 범위 적용 ───────────────────────
    private IEnumerator TickRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;
            var hits = Physics2D.OverlapCircleAll(transform.position, radius);
            foreach (var col in hits)
            {
                if (!col.TryGetComponent<EnemyController>(out var enemy)) continue;
                if (structureType == StructureType.DoT)
                    enemy.TakeDamage(mainValue);
                else
                    enemy.Paralyze(subValue > 0f ? subValue : tickInterval);
            }
        }
    }

    // ── HealZone — 플레이어가 범위 내에 있으면 주기적 회복 ───────
    private IEnumerator HealRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            yield return wait;
            var player = PlayerController.Instance;
            if (player == null) continue;
            if (Vector2.Distance(transform.position, player.Position) <= radius)
                player.HealHP(mainValue);
        }
    }

    // ── Fence — 플레이어 이동 방향에 수직으로 3개 일렬 배치 ──────
    private void SpawnFenceLine()
    {
        // 플레이어 이동 방향에 수직인 축을 구함
        var player   = PlayerController.Instance;
        Vector2 move = player != null && player.MoveDirection.sqrMagnitude > 0.01f
            ? player.MoveDirection.normalized : Vector2.up;

        // 수직 벡터: (x,y) → (-y,x)
        Vector2 perp = new Vector2(-move.y, move.x);

        // 좌(-1), 중(0), 우(+1) — 중앙(0)은 이미 현재 오브젝트
        int[] offsets = { -1, 1 };
        foreach (int o in offsets)
        {
            Vector3 pos   = transform.position + (Vector3)(perp * fenceSpacing * o);
            var     extra = Instantiate(gameObject, pos, transform.rotation);
            extra.GetComponent<StructureController>().isFenceChild = true;
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = structureType == StructureType.HealZone ? Color.green : Color.cyan;
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
