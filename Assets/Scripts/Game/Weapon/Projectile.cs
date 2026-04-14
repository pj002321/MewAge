using UnityEngine;

/// <summary>
/// 플레이어 또는 적이 발사하는 투사체.
/// 레이어 충돌 매트릭스로 대상을 구분합니다 (PlayerProjectile / EnemyProjectile 레이어).
/// </summary>
public class Projectile : MonoBehaviour
{
    [SerializeField] private LayerMask targetLayer; // 인스펙터에서 설정 (Enemy 또는 Player)

    private BossController _ownerBoss;   // 보스가 발사한 투사체면 설정 → 보스 자해 방지
    private float   primaryDmg;
    private Vector2 direction;
    private float   speed;
    private bool    isPrimaryCritical;
    private float   lifeSteal;
    private float   dotDamage;
    private float   areaHitRadius;
    private float   areaHitDamageRatio;
    private float   criticalRate;
    private float   criticalMultiplier;
    private float   baseDamage;

    // Physics NonAlloc 버퍼
    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    public void Init(float primaryDmg, Vector2 direction, float speed = 6f, float lifetime = 5f, bool isPrimaryCritical = false, float lifeSteal = 0f, float dotDamage = 0f, float areaHitRadius = 0f, float areaHitDamageRatio = 0f, float criticalRate = 0f, float criticalMultiplier = 2f, float baseDamage = 0f)
    {
        this.primaryDmg         = primaryDmg;
        this.direction          = direction.normalized;
        this.speed              = speed;
        this.isPrimaryCritical  = isPrimaryCritical;
        this.lifeSteal          = lifeSteal;
        this.dotDamage          = dotDamage;
        this.areaHitRadius      = areaHitRadius;
        this.areaHitDamageRatio = areaHitDamageRatio;
        this.criticalRate       = criticalRate;
        this.criticalMultiplier = criticalMultiplier;
        this.baseDamage         = baseDamage;
        Destroy(gameObject, lifetime);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        bool crit = criticalRate > 0f && Random.value < criticalRate;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
    }

    private void Update()
    {
        transform.Translate(direction * speed * Time.deltaTime, Space.World);
    }

    /// <summary>보스가 발사한 투사체임을 표시합니다. 보스 자해를 방지합니다.</summary>
    public void SetOwnerBoss(BossController boss) => _ownerBoss = boss;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 보스 타격 (레이어 무관) — 보스 자신이 발사한 투사체는 제외
        var boss = _ownerBoss == null ? other.GetComponentInParent<BossController>() : null;
        if (boss != null)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            boss.TakeDamage(primaryDmg, isPrimaryCritical);
            if (lifeSteal > 0f) PlayerController.Instance?.HealHP(primaryDmg * lifeSteal);
            Destroy(gameObject);
            return;
        }

        if ((targetLayer.value & (1 << other.gameObject.layer)) == 0) return;

        var enemy  = other.GetComponentInParent<EnemyController>();
        var player = other.GetComponentInParent<PlayerController>();

        if (enemy != null)
        {
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            // 범위 데미지 적용
            int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, areaHitRadius, _hitBuffer);
            for (int i = 0; i < colCount; i++)
            {
                if (!_hitBuffer[i].TryGetComponent<EnemyController>(out var areaEnemy)) continue;
                bool isPrimary = areaEnemy == enemy;
                float dmg = isPrimary ? primaryDmg : baseDamage * areaHitDamageRatio;
                bool isCrit = isPrimary ? isPrimaryCritical : RollCrit(dmg).isCritical;
                if (!isPrimary)
                {
                    var rolled = RollCrit(baseDamage * areaHitDamageRatio);
                    dmg = rolled.finalDamage;
                    isCrit = rolled.isCritical;
                }
                Vector2 knockDir = ((Vector2)areaEnemy.transform.position - (Vector2)transform.position);
                if (knockDir.sqrMagnitude < 0.001f) knockDir = direction;
                areaEnemy.TakeDamage(dmg, isCrit, knockDir.normalized, 6f);
                if (lifeSteal > 0f) PlayerController.Instance?.HealHP(dmg * lifeSteal);
                if (dotDamage > 0f) areaEnemy.ApplyBleed(dotDamage);
            }
        }
        else if (player != null)
        {
            player.TakeDamage(primaryDmg);
        }
        else
        {
            return;
        }

        Destroy(gameObject);
    }
}
