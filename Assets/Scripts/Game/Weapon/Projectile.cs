using UnityEngine;

/// <summary>
/// 플레이어 또는 적이 발사하는 투사체.
/// 레이어 충돌 매트릭스로 대상을 구분합니다 (PlayerProjectile / EnemyProjectile 레이어).
/// </summary>
public class Projectile : MonoBehaviour
{
    public enum SpriteForward { Up, Right, Down, Left }

    [SerializeField] private LayerMask    targetLayer;  // 인스펙터에서 설정 (Enemy 또는 Player)
    [Tooltip("이 프리팹 에셋의 기본 Forward 방향 (Z=0일 때 스프라이트가 향하는 쪽)")]
    [SerializeField] private SpriteForward spriteForward = SpriteForward.Up;

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
    private float   knockbackPower;


    // 가속 전용
    private float _curSpeed;
    private float _accel;

    // 포물선 전용
    private bool    _isParabolic;
    private Vector3 _paraStart;
    private Vector3 _paraControl;
    private Vector3 _paraEnd;
    private float   _paraDuration;
    private float   _paraElapsed;

    // 에셋 forward 각도 (Up=90, Right=0, Down=-90, Left=180)
    private float ForwardAngle => spriteForward switch
    {
        SpriteForward.Up    =>  90f,
        SpriteForward.Right =>   0f,
        SpriteForward.Down  => -90f,
        SpriteForward.Left  => 180f,
        _                   =>  90f,
    };

    // Physics NonAlloc 버퍼
    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    public static readonly System.Collections.Generic.List<Projectile> All = new();

    private void OnEnable()  => All.Add(this);
    private void OnDisable() => All.Remove(this);

    public void Init(float primaryDmg, Vector2 direction, float speed = 6f, float lifetime = 5f, bool isPrimaryCritical = false, float lifeSteal = 0f, float dotDamage = 0f, float areaHitRadius = 0f, float areaHitDamageRatio = 0f, float criticalRate = 0f, float criticalMultiplier = 2f, float baseDamage = 0f, float knockbackPower = 16f, float acceleration = 0f, float initialSpeedRatio = 0.25f)
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
        this.knockbackPower     = knockbackPower;
        _accel    = acceleration;
        _curSpeed = acceleration > 0f ? speed * initialSpeedRatio : speed;
        Destroy(gameObject, lifetime);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ForwardAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>
    /// 포물선 투사체 초기화.
    /// 2차 베지어(Start → Control → End)로 이동하며, 매 프레임 접선 방향으로 자전.
    /// </summary>
    public void InitParabolic(float primaryDmg, Vector3 targetPos, float speed, float arcHeight,
        bool isPrimaryCritical = false, float lifeSteal = 0f, float dotDamage = 0f,
        float areaHitRadius = 0f, float areaHitDamageRatio = 0f,
        float criticalRate = 0f, float criticalMultiplier = 2f,
        float baseDamage = 0f, float knockbackPower = 16f)
    {
        this.primaryDmg         = primaryDmg;
        this.isPrimaryCritical  = isPrimaryCritical;
        this.lifeSteal          = lifeSteal;
        this.dotDamage          = dotDamage;
        this.areaHitRadius      = areaHitRadius;
        this.areaHitDamageRatio = areaHitDamageRatio;
        this.criticalRate       = criticalRate;
        this.criticalMultiplier = criticalMultiplier;
        this.baseDamage         = baseDamage;
        this.knockbackPower     = knockbackPower;

        _isParabolic  = true;
        _paraStart    = transform.position;
        _paraEnd      = targetPos;

        // 컨트롤 포인트: 시작~끝 중간에서 World Y 방향으로 arcHeight 만큼 올림
        _paraControl  = (_paraStart + _paraEnd) * 0.5f + Vector3.up * arcHeight;

        float dist    = Vector3.Distance(_paraStart, _paraEnd);
        _paraDuration = Mathf.Max(dist / Mathf.Max(speed, 0.1f), 0.05f);
        _paraElapsed  = 0f;

        // 타격 시 넉백 방향 = 발사 방향 (수평)
        direction = ((Vector2)(targetPos - transform.position)).normalized;

        Destroy(gameObject, _paraDuration + 0.2f);
    }

    private (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        bool crit = criticalRate > 0f && Random.value < criticalRate;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
    }

    private void Update()
    {
        if (_isParabolic)
        {
            _paraElapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_paraElapsed / _paraDuration);

            // 2차 베지어 위치
            float u   = 1f - t;
            Vector3 pos = u * u * _paraStart
                        + 2f * u * t * _paraControl
                        + t * t * _paraEnd;
            transform.position = pos;

            // 베지어 접선으로 회전 (해석적 미분: dP/dt = 2(1-t)(P1-P0) + 2t(P2-P1))
            Vector3 tangent = 2f * u * (_paraControl - _paraStart)
                            + 2f * t * (_paraEnd    - _paraControl);
            if (tangent.sqrMagnitude > 0.0001f)
            {
                float angle = Mathf.Atan2(tangent.y, tangent.x) * Mathf.Rad2Deg - ForwardAngle;
                transform.rotation = Quaternion.Euler(0f, 0f, angle);
            }

            if (t >= 1f) Destroy(gameObject);
            return;
        }

        if (_accel > 0f)
            _curSpeed = Mathf.MoveTowards(_curSpeed, speed, _accel * Time.deltaTime);
        transform.Translate(direction * _curSpeed * Time.deltaTime, Space.World);
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
                areaEnemy.TakeDamage(dmg, isCrit, knockDir.normalized, knockbackPower);
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
