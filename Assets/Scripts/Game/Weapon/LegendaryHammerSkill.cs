using UnityEngine;

/// <summary>
/// 전설 해머 스킬: 착지 충격 시 메인 범위 밖 적에게 충격파(쇼크웨이브) 추가 피해.
/// shockwaveRadius는 메인 충격 반경(Inspector 기본 1.8)보다 크게 설정하세요.
/// </summary>
public class LegendaryHammerSkill : WeaponLegendarySkill
{
    [SerializeField] private float shockwaveRadius = 4.5f; // 쇼크웨이브 총 반경 (메인 범위보다 크게)
    [SerializeField] private float shockwaveDmgRatio = 0.4f;

    private static readonly Collider2D[] _shockBuffer = new Collider2D[32];

    public override void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount)
    {
        if (WP == null) return;

        int shockCount = Physics2D.OverlapCircleNonAlloc(transform.position, shockwaveRadius, _shockBuffer);
        float dmg = WP.AttackDamage * shockwaveDmgRatio;

        for (int i = 0; i < shockCount; i++)
        {
            if (_shockBuffer[i] == null) continue;

            // 메인 버퍼에 이미 포함된 콜라이더는 스킵 (중복 피해 방지)
            bool alreadyHit = false;
            for (int j = 0; j < colCount; j++)
                if (hitBuffer[j] == _shockBuffer[i]) { alreadyHit = true; break; }
            if (alreadyHit) continue;

            var boss = _shockBuffer[i].GetComponentInParent<BossController>();
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                Vector2 dir = ((Vector2)boss.transform.position - (Vector2)transform.position).normalized;
                boss.TakeDamage(dmg, false, dir.sqrMagnitude > 0.001f ? dir : Vector2.up, 0f);
                continue;
            }

            var e = _shockBuffer[i].GetComponentInParent<EnemyController>();
            if (e != null && e.gameObject.activeInHierarchy)
            {
                Vector2 dir = ((Vector2)e.transform.position - (Vector2)transform.position).normalized;
                e.TakeDamage(dmg, false, dir.sqrMagnitude > 0.001f ? dir : Vector2.up, 0f);
            }
        }
    }
}
