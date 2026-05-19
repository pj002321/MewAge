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

    [Header("고스트 트레일")]
    [SerializeField] private bool  ghostTrail         = false;
    [SerializeField] private Color ghostColor         = new Color(1f, 0.15f, 0.15f, 0.45f);
    [SerializeField] private float ghostInterval      = 0.04f;
    [SerializeField] private float ghostLifetime      = 0.12f;

    private BossController _ownerBoss;   // 보스가 발사한 투사체면 설정 → 보스 자해 방지
    private bool    _pierce;             // 관통: 적을 파괴 없이 통과
    private bool    _reflect;            // 반사: 첫 타격 후 방향 반전
    private bool    _reflected;
    private readonly System.Collections.Generic.HashSet<EnemyController> _pierced = new();
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


    // 주 타겟 적 타격 시 WeaponPoint로 콜백 (HIT_* 특수효과용)
    private System.Action<EnemyController, float, Vector2> _onHitEnemy;
    // 보스 타격 시 WeaponPoint로 콜백 (특수효과용)
    private System.Action<BossController, float> _onHitBoss;

    // 보스 중복 타격 방지 플래그 (physics layer 우회 수동 체크와 OnTriggerEnter2D 중복 방지)
    private bool          _bossHitDone;
    private Collider2D    _cachedBossCol;

    // 착탄 VFX
    private GameObject _impactVfxPrefab;
    private float      _impactVfxLifetime;

    // 고스트 트레일
    private SpriteRenderer _sr;
    private float          _ghostTimer;

    // 가속 전용
    private float _curSpeed;
    private float _accel;
    private float _accelStartSpeed;
    private float _accelDuration;
    private float _accelTimer;

    // 벽 반사 전용 (보스 바위 굴리기 등)
    private bool  _bounce;
    private int   _bounceRemaining;
    private float _bounceRotateSpeed;

    // 유도 전용
    private bool  _isHoming;
    private float _homingTurnSpeed;   // deg/s
    private bool  _homingTargetPlayer;
    private float _homingTimeLeft = float.MaxValue;

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

    public float ForwardAngleDeg => ForwardAngle;

    // Physics NonAlloc 버퍼
    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    public static readonly System.Collections.Generic.List<Projectile> All = new();

    private void Awake() => _sr = GetComponentInChildren<SpriteRenderer>();
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
        _accelDuration   = acceleration; // 파라미터 의미: 최고속도 도달까지 걸리는 시간(초), 0=즉시
        _accelStartSpeed = speed * Mathf.Clamp01(initialSpeedRatio);
        _curSpeed        = _accelDuration > 0f ? _accelStartSpeed : speed;
        _accel           = 0f;
        _accelTimer      = 0f;
        Destroy(gameObject, lifetime);

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ForwardAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    /// <summary>유도탄 활성화 (적 추적). Init() 호출 직후 사용하세요.</summary>
    public void SetHoming(float turnSpeed = 200f)
    {
        _isHoming        = true;
        _homingTurnSpeed = turnSpeed;
    }

    /// <summary>플레이어 추적 유도탄 활성화. 보스 스킬용. Init() 호출 직후 사용하세요.</summary>
    public void SetHomingPlayer(float turnSpeed = 200f, float homingDuration = 2f)
    {
        _isHoming           = true;
        _homingTurnSpeed    = turnSpeed;
        _homingTargetPlayer = true;
        _homingTimeLeft     = homingDuration;
    }

    /// <summary>벽 반사(바운스) 모드를 활성화합니다. Init() 호출 직후 사용하세요.</summary>
    public void SetBounce(int maxBounces, float rotateSpeed = 270f)
    {
        _bounce            = true;
        _bounceRemaining   = maxBounces;
        _bounceRotateSpeed = rotateSpeed;
    }

    /// <summary>관통/반사 플래그를 설정합니다. Init() 호출 직후 사용하세요.</summary>
    public void SetSpecialFlags(bool pierce, bool reflect)
    {
        _pierce   = pierce;
        _reflect  = reflect;
        _reflected = false;
        _pierced.Clear();
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

    private void SpawnGhost()
    {
        if (_sr == null || _sr.sprite == null) return;
        var go    = new GameObject("ProjGhost", typeof(SpriteRenderer));
        go.transform.SetPositionAndRotation(transform.position, transform.rotation);
        go.transform.localScale = transform.localScale;
        var ghost              = go.GetComponent<SpriteRenderer>();
        ghost.sprite           = _sr.sprite;
        ghost.color            = ghostColor;
        ghost.sortingLayerID   = _sr.sortingLayerID;
        ghost.sortingOrder     = _sr.sortingOrder - 1;
        Destroy(go, ghostLifetime);
    }

    private void Update()
    {
        if (ghostTrail)
        {
            _ghostTimer -= Time.deltaTime;
            if (_ghostTimer <= 0f)
            {
                SpawnGhost();
                _ghostTimer = ghostInterval;
            }
        }

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

            if (t >= 1f) { SpawnImpactVfx(); Destroy(gameObject); }
            return;
        }

        if (_isHoming)
        {
            _homingTimeLeft -= Time.deltaTime;
            if (_homingTimeLeft <= 0f) _isHoming = false;
            else UpdateHoming();
        }

        if (_accelDuration > 0f)
        {
            _accelTimer += Time.deltaTime;
            float t = Mathf.Clamp01(_accelTimer / _accelDuration);
            // ease-in cubic (t³): 초반엔 거의 안 움직이다가 끝에 팍 꽂히는 느낌
            _curSpeed = Mathf.Lerp(_accelStartSpeed, speed, t * t * t);
        }
        transform.Translate(direction * _curSpeed * Time.deltaTime, Space.World);

        if (_bounce) UpdateBounce();

        CheckBossProximity();
    }

    private void UpdateBounce()
    {
        // Z축 회전
        transform.Rotate(0f, 0f, _bounceRotateSpeed * Time.deltaTime, Space.Self);

        var mb = MovementBounds.Shared;
        if (mb == null || mb.IsInside(transform.position)) return;

        if (_bounceRemaining <= 0) { Destroy(gameObject); return; }

        // clamp 전 위치로 법선 계산 → transform.position 변경 전에 diff 구해야 정확
        Vector2 curPos  = transform.position;
        Vector2 clamped = mb.ClosestPointOnBoundary(curPos);
        transform.position = clamped;

        Vector2 diff   = curPos - clamped;
        Vector2 normal = diff.sqrMagnitude > 0.0001f ? diff.normalized : -direction;

        direction = Vector2.Reflect(direction, normal).normalized;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ForwardAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        _bounceRemaining--;
    }

    private void UpdateHoming()
    {
        Transform target = null;

        if (_homingTargetPlayer)
        {
            var player = PlayerController.Instance;
            if (player != null) target = player.transform;
        }
        else
        {
            // 보스 우선, 없으면 가장 가까운 일반 적
            float best = float.MaxValue;
            var boss = BossController.Current;
            if (boss != null && boss.gameObject.activeInHierarchy)
            {
                target = boss.transform;
            }
            else
            {
                foreach (var e in EnemyController.All)
                {
                    if (e == null || !e.gameObject.activeInHierarchy) continue;
                    float d = ((Vector2)e.transform.position - (Vector2)transform.position).sqrMagnitude;
                    if (d < best) { best = d; target = e.transform; }
                }
            }
        }

        if (target == null) return;

        Vector2 toTarget = ((Vector2)target.position - (Vector2)transform.position).normalized;
        float   maxDeg   = _homingTurnSpeed * Time.deltaTime;
        direction = Vector2.MoveTowards(direction, toTarget, maxDeg * Mathf.Deg2Rad).normalized;

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ForwardAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // physics layer matrix와 무관하게 보스 collider 직접 체크
    private void CheckBossProximity()
    {
        if (_bossHitDone || _ownerBoss != null) return;
        var boss = BossController.Current;
        if (boss == null || !boss.gameObject.activeInHierarchy) return;

        if (_cachedBossCol == null || !_cachedBossCol)
            _cachedBossCol = boss.GetComponentInChildren<Collider2D>();
        if (_cachedBossCol == null) return;

        if (!_cachedBossCol.OverlapPoint(transform.position)) return;

        _bossHitDone = true;
        SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
        boss.TakeDamage(primaryDmg, isPrimaryCritical);
        if (lifeSteal > 0f) PlayerController.Instance?.HealHP(primaryDmg * lifeSteal);
        _onHitBoss?.Invoke(boss, primaryDmg);
        SpawnImpactVfx();
        if (_reflect && !_reflected)
        {
            _reflected   = true;
            _bossHitDone = false;
            direction    = -direction;
            float ra = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ForwardAngle;
            transform.rotation = Quaternion.Euler(0f, 0f, ra);
            return;
        }
        if (_pierce) return;
        Destroy(gameObject);
    }

    /// <summary>보스가 발사한 투사체임을 표시합니다. 보스 자해를 방지합니다.</summary>
    public void SetOwnerBoss(BossController boss) => _ownerBoss = boss;

    /// <summary>주 타겟 적 타격 시 호출할 콜백을 설정합니다 (HIT_* 특수효과용).</summary>
    public void SetOnHitEnemy(System.Action<EnemyController, float, Vector2> callback) => _onHitEnemy = callback;

    /// <summary>보스 타격 시 호출할 콜백을 설정합니다 (특수효과용).</summary>
    public void SetOnHitBoss(System.Action<BossController, float> callback) => _onHitBoss = callback;

    /// <summary>충돌/착탄 시 스폰할 VFX를 설정합니다.</summary>
    public void SetImpactVfx(GameObject prefab, float lifetime = 1f)
    {
        _impactVfxPrefab   = prefab;
        _impactVfxLifetime = lifetime;
    }

    private void SpawnImpactVfx()
    {
        if (_impactVfxPrefab == null) return;
        var vfx = Instantiate(_impactVfxPrefab, transform.position, Quaternion.identity);
        if (_impactVfxLifetime > 0f) Destroy(vfx, _impactVfxLifetime);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 보스 타격 (레이어 무관) — 보스 자신이 발사한 투사체는 제외
        var boss = _ownerBoss == null ? other.GetComponentInParent<BossController>() : null;
        if (boss != null)
        {
            if (_bossHitDone) return;
            _bossHitDone = true;
            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            boss.TakeDamage(primaryDmg, isPrimaryCritical);
            if (lifeSteal > 0f) PlayerController.Instance?.HealHP(primaryDmg * lifeSteal);
            _onHitBoss?.Invoke(boss, primaryDmg);
            SpawnImpactVfx();
            if (_reflect && !_reflected)
            {
                _reflected   = true;
                _bossHitDone = false;
                direction    = -direction;
                float ra = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ForwardAngle;
                transform.rotation = Quaternion.Euler(0f, 0f, ra);
                return;
            }
            if (_pierce) return;
            Destroy(gameObject);
            return;
        }

        if ((targetLayer.value & (1 << other.gameObject.layer)) == 0) return;

        var enemy  = other.GetComponentInParent<EnemyController>();
        var player = other.GetComponentInParent<PlayerController>();

        if (enemy != null)
        {
            // 관통: 이미 타격한 적은 스킵
            if (_pierce && _pierced.Contains(enemy)) return;

            SoundManager.Shared?.Play(SoundKey.Weapon_Attack);
            // 범위 데미지 적용
            int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, areaHitRadius, _hitBuffer);
            for (int i = 0; i < colCount; i++)
            {
                if (!_hitBuffer[i].TryGetComponent<EnemyController>(out var areaEnemy)) continue;
                if (_pierce && _pierced.Contains(areaEnemy)) continue;
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
                if (_pierce) _pierced.Add(areaEnemy);
            }

            // 주 타겟 콜백 (areaHitRadius=0이어도 반드시 발동)
            Vector2 primaryKnockDir = ((Vector2)enemy.transform.position - (Vector2)transform.position);
            if (primaryKnockDir.sqrMagnitude < 0.001f) primaryKnockDir = direction;
            _onHitEnemy?.Invoke(enemy, primaryDmg, primaryKnockDir.normalized);

            SpawnImpactVfx();

            if (_reflect && !_reflected)
            {
                _reflected = true;
                direction  = -direction;
                float ra = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - ForwardAngle;
                transform.rotation = Quaternion.Euler(0f, 0f, ra);
                return;
            }
            if (_pierce) return;
            Destroy(gameObject);
            return;
        }
        else if (player != null)
        {
            player.TakeDamage(primaryDmg, sourceName: gameObject.name);
        }
        else
        {
            return;
        }

        SpawnImpactVfx();
        Destroy(gameObject);
    }
}
