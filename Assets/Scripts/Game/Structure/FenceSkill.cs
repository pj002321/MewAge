using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 설치 시 이동 방향 수직으로 3개 연속 펜스를 배치합니다.
/// 각 펜스 오브젝트에 BoxCollider2D(solid physics)를 자동 추가해
/// 적이 실제로 막히도록 합니다.
/// 펜스에 닿은 적은 이동 불가 상태가 지속됩니다.
///
/// # Unity Setup:
///   1. 기물 프리팹에 이 스크립트를 부착하세요.
///   2. 프리팹 Rigidbody2D → BodyType = Static 이어야 적을 물리적으로 막습니다.
///   3. 기물 프리팹의 레이어를 "Obstacle" (또는 벽 레이어) 로 설정하고,
///      Physics2D Collision Matrix 에서 적 레이어와 충돌하도록 체크하세요.
///   4. colliderWidth / colliderHeight 는 Inspector 에서 스프라이트 크기에 맞게 조절하세요.
/// </summary>
public class FenceSkill : StructureSkillBase
{
    [Header("펜스 배치")]
    [SerializeField] private float fenceSpacing = 0.8f;  // 좌우 간격

    [Header("물리 콜라이더 크기 (스프라이트에 맞게 조정)")]
    [SerializeField] private float colliderWidth  = 0.6f;
    [SerializeField] private float colliderHeight = 1.0f;



    private bool isFenceChild;

    // 현재 이 펜스에 걸려있는 적 목록
    private readonly HashSet<EnemyController> _trapped = new();
    private float _refreshTimer;

    private const float RefreshInterval  = 0.08f;  // Paralyze 재적용 간격
    private const float ParalyzeDuration = 0.12f;  // > RefreshInterval 이어야 끊기지 않음

    public override void Activate()
    {
        EnsureCollider();
        if (!isFenceChild) SpawnFenceLine();
    }

    public void MarkAsFenceChild() => isFenceChild = true;

    private void Update()
    {
        if (_trapped.Count == 0) return;

        _refreshTimer += Time.deltaTime;
        if (_refreshTimer < RefreshInterval) return;
        _refreshTimer = 0f;

        _trapped.RemoveWhere(e => e == null);
        foreach (var e in _trapped)
            e.Paralyze(ParalyzeDuration);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null) return;
        _trapped.Add(enemy);
        enemy.Paralyze(ParalyzeDuration);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null) _trapped.Remove(enemy);
    }

    /// <summary>
    /// Solid BoxCollider2D + 감지용 Trigger BoxCollider2D + Static Rigidbody2D 를 보장합니다.
    /// </summary>
    private void EnsureCollider()
    {
        bool hasSolid   = false;
        bool hasTrigger = false;
        foreach (var c in GetComponents<BoxCollider2D>())
        {
            if (!c.isTrigger) hasSolid   = true;
            else              hasTrigger = true;
        }

        if (!hasSolid)
        {
            var col   = gameObject.AddComponent<BoxCollider2D>();
            col.size  = new Vector2(colliderWidth, colliderHeight);
            col.isTrigger = false;   // 물리 차단용
        }

        if (!hasTrigger)
        {
            var trig   = gameObject.AddComponent<BoxCollider2D>();
            trig.size  = new Vector2(colliderWidth, colliderHeight);
            trig.isTrigger = true;   // 적 감지용
        }

        if (GetComponent<Rigidbody2D>() == null)
        {
            var rb      = gameObject.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Static;
        }
        else
        {
            GetComponent<Rigidbody2D>().bodyType = RigidbodyType2D.Static;
        }

        this.DLog($"[FenceSkill] Collider 보장 완료 — size:({colliderWidth},{colliderHeight})");
    }

    private void SpawnFenceLine()
    {
        var player   = PlayerController.Instance;
        Vector2 move = player != null && player.MoveDirection.sqrMagnitude > 0.01f
            ? player.MoveDirection.normalized : Vector2.up;

        Vector2 perp = new Vector2(-move.y, move.x);

        foreach (int offset in new[] { -1, 1 })
        {
            Vector3 pos   = transform.position + (Vector3)(perp * fenceSpacing * offset);
            var     extra = Instantiate(gameObject, pos, transform.rotation);
            extra.GetComponent<FenceSkill>()?.MarkAsFenceChild();
        }
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, new Vector3(colliderWidth, colliderHeight, 0f));
    }
}
