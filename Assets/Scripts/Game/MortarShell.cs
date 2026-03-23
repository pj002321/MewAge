using System.Collections;
using UnityEngine;

/// <summary>
/// 포물선 호를 그리며 목표 지점으로 날아가는 투사체.
/// 비행 중 플레이어 트리거에 닿거나, 착지 시 hitRadius 안에 있으면 데미지를 줍니다.
/// BossVolleySkill 이 Init() 으로 초기화합니다.
/// </summary>
public class MortarShell : MonoBehaviour
{
    [SerializeField] private float arcHeight = 3f;

    private float   damage;
    private float   hitRadius;
    private Vector2 endPos;
    private bool    hasHit;

    /// <summary>스폰 직후 호출.</summary>
    public void Init(Vector2 start, Vector2 end, float flightTime,
                     float overrideArc = -1f, float damage = 0f, float hitRadius = 0.8f)
    {
        this.damage    = damage;
        this.hitRadius = hitRadius;
        this.endPos    = end;
        float arc = overrideArc >= 0f ? overrideArc : arcHeight;
        StartCoroutine(FlyRoutine(start, end, flightTime, arc));
    }

    private IEnumerator FlyRoutine(Vector2 start, Vector2 end, float flightTime, float arc)
    {
        float elapsed = 0f;

        while (elapsed < flightTime)
        {
            float   t    = elapsed / flightTime;
            Vector2 flat = Vector2.Lerp(start, end, t);
            float   yArc = arc * Mathf.Sin(t * Mathf.PI);
            transform.position = new Vector3(flat.x, flat.y + yArc, 0f);

            // 진행 방향으로 회전
            Vector2 vel = (end - start) + Vector2.up * (arc * Mathf.PI * Mathf.Cos(t * Mathf.PI));
            if (vel.sqrMagnitude > 0.001f)
            {
                float angle = Mathf.Atan2(vel.y, vel.x) * Mathf.Rad2Deg - 90f;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 착지 — 범위 내 플레이어 데미지
        transform.position = new Vector3(end.x, end.y, 0f);
        TryHitPlayer(end);
        Destroy(gameObject);
    }

    // 비행 중 충돌 판정 (Trigger Collider 가 붙어있는 경우)
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;
        hasHit = true;
        player.TakeDamage(damage);
        Destroy(gameObject);
    }

    private void TryHitPlayer(Vector2 pos)
    {
        if (hasHit || PlayerController.Instance == null) return;
        float dist = Vector2.Distance((Vector2)PlayerController.Instance.transform.position, pos);
        if (dist <= hitRadius)
        {
            hasHit = true;
            PlayerController.Instance.TakeDamage(damage);
        }
    }
}
