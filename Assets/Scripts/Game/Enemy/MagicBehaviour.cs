using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

public enum MagicSkillType { Spike, Summon }

/// <summary>
/// 마법 공격형 AI.
///
/// Magic-Spike  : 플레이어 현재 위치에 마커 고정 → warningDuration 후 이펙트 + 피해 판정.
/// Magic-Summon : 플레이어 주변에 마커 N개 고정 → warningDuration 후 summonPrefab N개 소환.
/// </summary>
public class MagicBehaviour : EnemyBehaviourBase
{
    [Header("스킬 타입")]
    [SerializeField] private MagicSkillType skillType = MagicSkillType.Spike;

    [Header("공통")]
    [SerializeField] private float      castInterval    = 4f;
    [SerializeField] private float      castRange       = 6f;
    [SerializeField] private float      warningDuration = 0.8f;
    [Tooltip("경고 마커 프리팹 (ImpactMarker 컴포넌트 권장, 없으면 단순 Instantiate)")]
    [SerializeField] private GameObject markerPrefab;

    [ShowIf("skillType", MagicSkillType.Spike)]
    [BoxGroup("Magic-Spike")]
    [SerializeField] private float      spikeDamage      = 20f;
    [ShowIf("skillType", MagicSkillType.Spike)]
    [BoxGroup("Magic-Spike")]
    [SerializeField] private float      spikeHitRadius   = 1f;
    [ShowIf("skillType", MagicSkillType.Spike)]
    [BoxGroup("Magic-Spike")]
    [SerializeField] private GameObject spikeEffectPrefab;
    [ShowIf("skillType", MagicSkillType.Spike)]
    [BoxGroup("Magic-Spike")]
    [SerializeField] private float      effectLifetime   = 1f;

    [ShowIf("skillType", MagicSkillType.Summon)]
    [BoxGroup("Magic-Summon")]
    [SerializeField] private GameObject summonPrefab;
    [ShowIf("skillType", MagicSkillType.Summon)]
    [BoxGroup("Magic-Summon")]
    [SerializeField] private int        summonCount  = 3;
    [ShowIf("skillType", MagicSkillType.Summon)]
    [BoxGroup("Magic-Summon")]
    [Tooltip("소환 위치를 플레이어 중심에서 이 반경 원형으로 배치. 1개면 중심에 소환.")]
    [SerializeField] private float      summonSpread = 1.5f;

    [Header("이동")]
    [SerializeField] private float preferredRange = 6f;

    // ── 내부 상태 ─────────────────────────────────────────────────────
    private float        castTimer;
    private bool         isCasting;
    private GameObject[] _activeMarkers;

    // ── 초기화 ───────────────────────────────────────────────────────
    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        castTimer = castInterval;
    }

    // ── AI 루프 ──────────────────────────────────────────────────────
    public override void OnAttackUpdate()
    {
        if (isCasting) return;
        if (Owner.PlayerTransform == null) return;

        castTimer -= Time.deltaTime;
        if (castTimer > 0f) return;
        castTimer = castInterval;

        float dist = Vector2.Distance(transform.position, Owner.PlayerTransform.position);
        if (dist > castRange) return;

        Owner.EnemyAnimator?.SetTrigger(EnemyAnim.TAttack);
        StartCoroutine(skillType == MagicSkillType.Spike ? SpikeRoutine() : SummonRoutine());
    }

    public override void OnMoveFixedUpdate()
    {
        if (Owner.PlayerTransform == null) return;
        if (isCasting) return;

        float   dist     = Vector2.Distance(Owner.Rb.position, Owner.PlayerTransform.position);
        Vector2 toPlayer = ((Vector2)Owner.PlayerTransform.position - Owner.Rb.position).normalized;

        Vector2 moveDir = dist < preferredRange * 0.8f ? -toPlayer :
                          dist > preferredRange * 1.2f ?  toPlayer : Vector2.zero;

        if (moveDir != Vector2.zero)
            Owner.MoveInDirection(moveDir, Owner.MoveSpeed);

        Owner.FlipToward(toPlayer);
    }

    // ── Magic-Spike ───────────────────────────────────────────────────
    private IEnumerator SpikeRoutine()
    {
        isCasting = true;

        // 플레이어 현재 위치 고정 (마커는 따라가지 않음)
        Vector2 targetPos = Owner.PlayerTransform != null
            ? (Vector2)Owner.PlayerTransform.position
            : (Vector2)transform.position;

        _activeMarkers = new[] { SpawnMarker(targetPos) };

        yield return new WaitForSeconds(warningDuration);

        DismissMarkers();

        if (Owner == null) { isCasting = false; yield break; }

        // 이펙트
        if (spikeEffectPrefab != null)
            Destroy(Instantiate(spikeEffectPrefab, targetPos, Quaternion.identity), effectLifetime);

        // 피해 판정
        var player = PlayerController.Instance;
        if (player != null &&
            Vector2.Distance(player.transform.position, targetPos) <= spikeHitRadius)
        {
            float dmg = spikeDamage > 0f ? spikeDamage : Owner.ContactDamage;
            player.TakeDamage(dmg);
        }

        isCasting = false;
    }

    // ── Magic-Summon ──────────────────────────────────────────────────
    private IEnumerator SummonRoutine()
    {
        isCasting = true;

        Vector2 center = Owner.PlayerTransform != null
            ? (Vector2)Owner.PlayerTransform.position
            : (Vector2)transform.position;

        // 소환 위치: 원형 배치 (1개면 중심)
        int count = Mathf.Max(1, summonCount);
        var positions = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float rad = 2f * Mathf.PI / count * i;
            positions[i] = center + (count > 1
                ? new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * summonSpread
                : Vector2.zero);
        }

        // 마커 일괄 생성
        _activeMarkers = new GameObject[count];
        for (int i = 0; i < count; i++)
            _activeMarkers[i] = SpawnMarker(positions[i]);

        yield return new WaitForSeconds(warningDuration);

        DismissMarkers();

        if (Owner == null) { isCasting = false; yield break; }

        // 소환
        if (summonPrefab != null)
            for (int i = 0; i < count; i++)
                Instantiate(summonPrefab, positions[i], Quaternion.identity);

        isCasting = false;
    }

    // ── 마커 헬퍼 ────────────────────────────────────────────────────
    private GameObject SpawnMarker(Vector2 pos)
    {
        if (markerPrefab == null) return null;
        return Instantiate(markerPrefab, pos, Quaternion.identity);
    }

    private void DismissMarkers()
    {
        if (_activeMarkers == null) return;
        foreach (var m in _activeMarkers)
        {
            if (m == null) continue;
            if (m.TryGetComponent<ImpactMarker>(out var im)) im.Dismiss();
            else Destroy(m);
        }
        _activeMarkers = null;
    }

    // ── 정리 ─────────────────────────────────────────────────────────
    private void OnDisable()
    {
        isCasting = false;
        DismissMarkers();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        var col = GetComponentInParent<Collider2D>();
        Vector3 pos = col != null ? col.bounds.center : transform.position;

        // 시전 사거리 (보라)
        UnityEditor.Handles.color = new Color(0.5f, 0f, 1f, 0.12f);
        UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, castRange);
        UnityEditor.Handles.color = new Color(0.5f, 0f, 1f, 0.8f);
        UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, castRange);

        if (skillType == MagicSkillType.Spike)
        {
            // 피해 반경 (빨강)
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.15f);
            UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, spikeHitRadius);
            UnityEditor.Handles.color = new Color(1f, 0f, 0f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, spikeHitRadius);
        }
        else if (skillType == MagicSkillType.Summon && summonCount > 1)
        {
            // 소환 배치 반경 (주황)
            UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.12f);
            UnityEditor.Handles.DrawSolidDisc(pos, Vector3.forward, summonSpread);
            UnityEditor.Handles.color = new Color(1f, 0.6f, 0f, 0.8f);
            UnityEditor.Handles.DrawWireDisc(pos, Vector3.forward, summonSpread);
        }
    }
#endif
}
