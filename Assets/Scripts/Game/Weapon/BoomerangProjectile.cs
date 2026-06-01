using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 부메랑 투사체.
/// 발사 → 첫 타겟 추적 → 타격 시 maxBounces 횟수만큼 다음 가장 가까운 적으로 튕김
/// → 더 이상 튕길 적이 없거나 횟수 소진 시 플레이어에게 귀환 → 도달하면 소멸.
/// </summary>
public class BoomerangProjectile : MonoBehaviour
{
    [Header("기본 설정 (Init()으로 덮어씀)")]
    [SerializeField] private float spinSpeed   = -720f;  // deg/s (시계방향)
    [SerializeField] private float returnRange = 0.35f; // 플레이어와 이 거리 내 도달 시 소멸

    [Header("튕김 타격감")]
    [SerializeField] private float bounceSpeedMult  = 2.2f;   // 튕김 직후 속도 배율
    [SerializeField] private float bounceSpeedDecay = 0.18f;  // 원래 속도로 돌아오는 시간(초)
    [SerializeField] private float bounceSpinBoost  = 1440f;  // 튕김 직후 추가 스핀 (deg/s)

    private float   damage;
    private float   speed;
    private float   _currentSpeed;
    private float   _bounceTimer;
    private float   _currentSpinSpeed;
    private int     bouncesLeft;
    private float   bounceRange;
    private bool    initialized;
    private bool    isCritical;
    private float   lifeSteal;
    private float   dotDamage;
    private float   areaHitRadius;
    private float   areaHitDamageRatio;
    private float   criticalRate;
    private float   criticalMultiplier;
    private float   baseDamage;
    private string  _weaponId;

    public static readonly System.Collections.Generic.List<BoomerangProjectile> All = new();

    private void OnEnable()  => All.Add(this);
    private void OnDisable() => All.Remove(this);

    private enum State { Flying, Returning }
    private State state = State.Flying;

    private object                             currentTarget;
    private Transform                         playerTransform;
    private readonly HashSet<EnemyController> hitEnemies  = new();
    private readonly HashSet<BossController>  hitBosses   = new();

    // 타겟이 없을 때 계속 날아갈 방향
    private Vector2 flyDir;

    // Physics NonAlloc 버퍼
    private static readonly Collider2D[] _hitBuffer = new Collider2D[32];

    public event System.Action OnReturned;

    public void Init(float damage, float speed, int maxBounces, float bounceRange,
                      object firstTarget, Transform playerTf, bool isCritical = false, float lifeSteal = 0f,
                      float dotDamage = 0f, float areaHitRadius = 0f, float areaHitDamageRatio = 0f,
                      float criticalRate = 0f, float criticalMultiplier = 2f, float baseDamage = 0f,
                      string weaponId = "")
    {
        this.damage             = damage;
        this.isCritical         = isCritical;
        this.lifeSteal          = lifeSteal;
        this.dotDamage          = dotDamage;
        this.areaHitRadius      = areaHitRadius;
        this.areaHitDamageRatio = areaHitDamageRatio;
        this.criticalRate       = criticalRate;
        this.criticalMultiplier = criticalMultiplier;
        this.baseDamage         = baseDamage;
        this.speed              = speed;
        _weaponId               = weaponId;
        
        // DB 등에서 maxBounces가 명시적으로 지정되지 않았다면(-1 혹은 0)
        // 맵에 존재하는 몬스터 수를 튕김 횟수로 할당하되 최소치(3)를 보장.
        if (maxBounces <= 0)
        {
            int totalEnemies = EnemyController.ActiveCount + (BossController.Current != null ? 1 : 0);
            this.bouncesLeft = Mathf.Max(3, totalEnemies);
        }
        else
        {
            this.bouncesLeft = maxBounces;
        }

        this.bounceRange        = bounceRange;
        this.playerTransform    = playerTf;
        this.initialized        = true;
        _currentSpeed           = speed;
        _currentSpinSpeed       = spinSpeed;
        _bounceTimer            = 0f;

        // Rigidbody2D가 있으면 물리 간섭 차단
        if (TryGetComponent<Rigidbody2D>(out var rb))
        {
            rb.bodyType        = RigidbodyType2D.Kinematic;
            rb.gravityScale    = 0f;
            rb.linearVelocity  = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        currentTarget = firstTarget;

        flyDir = firstTarget != null
            ? ((Vector2)((firstTarget as Component)?.transform.position ?? Vector2.zero) - (Vector2)transform.position).normalized
            : Vector2.up;
    }

    private (float finalDamage, bool isCritical) RollCrit(float baseDamage)
    {
        bool crit = criticalRate > 0f && Random.value < criticalRate;
        return crit ? (baseDamage * criticalMultiplier, true) : (baseDamage, false);
    }

    private void Update()
    {
        if (!initialized) return;

        // 튕김 속도 감쇠 (bounceSpeedMult → 1.0 으로 부드럽게 복귀)
        if (_bounceTimer > 0f)
        {
            _bounceTimer -= Time.deltaTime;
            float t = Mathf.Clamp01(1f - _bounceTimer / bounceSpeedDecay);
            _currentSpeed     = Mathf.Lerp(speed * bounceSpeedMult, speed, t);
            _currentSpinSpeed = Mathf.Lerp(spinSpeed - bounceSpinBoost, spinSpeed, t);
        }
        else
        {
            _currentSpeed     = speed;
            _currentSpinSpeed = spinSpeed;
        }

        // 시각적 스핀
        transform.Rotate(0f, 0f, _currentSpinSpeed * Time.deltaTime);

        switch (state)
        {
            case State.Flying:    UpdateFlying();    break;
            case State.Returning: UpdateReturning(); break;
        }
    }

    private void UpdateFlying()
    {
        bool targetActive = false;
        Vector3 targetPos = Vector3.zero;
        if (currentTarget is EnemyController ec && ec.gameObject.activeInHierarchy)
        {
            targetActive = true;
            targetPos = ec.transform.position;
        }
        else if (currentTarget is BossController bc && bc.gameObject.activeInHierarchy)
        {
            targetActive = true;
            targetPos = bc.transform.position;
        }

        if (targetActive)
        {
            MoveToward(targetPos);
        }
        else
        {
            // 타겟이 소멸(또는 처음부터 없음) → 가장 가까운 새 타겟으로 즉시 전환, 없으면 귀환
            currentTarget = null;
            object next = FindNextEnemy(transform.position, float.MaxValue);
            if (next != null)
                currentTarget = next;
            else
                BeginReturn();
        }
    }

    private void UpdateReturning()
    {
        if (playerTransform == null) { OnReturned?.Invoke(); return; }

        MoveToward(playerTransform.position);

        if (Vector2.Distance(transform.position, playerTransform.position) <= returnRange)
            OnReturned?.Invoke();
    }

    private void MoveToward(Vector3 target)
    {
        Vector2 dir = ((Vector2)target - (Vector2)transform.position).normalized;
        flyDir = dir;
        transform.Translate(dir * _currentSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (state == State.Returning) return; // 귀환 중엔 적에 반응하지 않음

        var enemy = other.GetComponentInParent<EnemyController>();
        var boss = other.GetComponentInParent<BossController>();
        if ((enemy == null || hitEnemies.Contains(enemy)) && (boss == null || hitBosses.Contains(boss))) return;

        if (enemy != null) hitEnemies.Add(enemy);
        if (boss != null) hitBosses.Add(boss);

        // 범위 데미지 적용
        int colCount = Physics2D.OverlapCircleNonAlloc(transform.position, areaHitRadius, _hitBuffer);
        bool anyHit = false;
        for (int i = 0; i < colCount; i++)
        {
            EnemyController areaEnemy = null;
            BossController areaBoss = null;
            if (_hitBuffer[i].TryGetComponent<EnemyController>(out areaEnemy))
            {
                bool isPrimary = areaEnemy == enemy;
                float dmg = isPrimary ? damage : baseDamage * areaHitDamageRatio;
                bool isCrit = isPrimary ? isCritical : RollCrit(dmg).isCritical;
                if (!isPrimary)
                {
                    var rolled = RollCrit(baseDamage * areaHitDamageRatio);
                    dmg = rolled.finalDamage;
                    isCrit = rolled.isCritical;
                }
                Vector2 knockDir = ((Vector2)areaEnemy.transform.position - (Vector2)transform.position);
                if (knockDir.sqrMagnitude < 0.001f) knockDir = (areaEnemy.transform.position - transform.position).normalized;
                areaEnemy.TakeDamage(dmg, isCrit, knockDir.normalized, 6f);
                if (lifeSteal > 0f) PlayerController.Instance?.HealHP(dmg * lifeSteal);
                if (dotDamage > 0f) areaEnemy.ApplyBleed(dotDamage);
                anyHit = true;
            }
            else if (_hitBuffer[i].TryGetComponent<BossController>(out areaBoss))
            {
                bool isPrimary = areaBoss == boss;
                float dmg = isPrimary ? damage : baseDamage * areaHitDamageRatio;
                bool isCrit = isPrimary ? isCritical : RollCrit(dmg).isCritical;
                if (!isPrimary)
                {
                    var rolled = RollCrit(baseDamage * areaHitDamageRatio);
                    dmg = rolled.finalDamage;
                    isCrit = rolled.isCritical;
                }
                Vector2 knockDir = ((Vector2)areaBoss.transform.position - (Vector2)transform.position);
                if (knockDir.sqrMagnitude < 0.001f) knockDir = (areaBoss.transform.position - transform.position).normalized;
                areaBoss.TakeDamage(dmg, isCrit, knockDir.normalized, 6f);
                if (lifeSteal > 0f) PlayerController.Instance?.HealHP(dmg * lifeSteal);
                if (dotDamage > 0f) areaBoss.ApplyBleed(dotDamage);
                anyHit = true;
            }
        }

        if (anyHit) SoundManager.Shared?.PlayWeaponHit(_weaponId);

        bouncesLeft--;

        if (bouncesLeft <= 0)
        {
            BeginReturn();
            return;
        }

        // 다음 튕길 적 탐색
        Vector3 primaryPos = enemy != null ? enemy.transform.position : (boss != null ? boss.transform.position : transform.position);
        object next = FindNextEnemy(primaryPos);
        if (next != null)
        {
            currentTarget = next;
            _bounceTimer  = bounceSpeedDecay; // 튕김 속도 폭발
        }
        else
            BeginReturn();
    }

    private void BeginReturn()
    {
        state         = State.Returning;
        currentTarget = null;
    }

    private void OnDestroy()
    {
        OnReturned?.Invoke();
    }

    /// <param name="range">탐색 반경. float.MaxValue 이면 전체 맵 탐색.</param>
    private object FindNextEnemy(Vector2 fromPos, float range = -1f)
    {
        EnemyController nearestEnemy = null;
        BossController nearestBoss = null;
        float minDist = range > 0f ? range : bounceRange;

        foreach (var e in EnemyController.All)
        {
            if (hitEnemies.Contains(e)) continue;
            float d = Vector2.Distance(fromPos, e.transform.position);
            if (d < minDist) { minDist = d; nearestEnemy = e; }
        }

        if (BossController.Current != null && !hitBosses.Contains(BossController.Current))
        {
            float d = Vector2.Distance(fromPos, BossController.Current.transform.position);
            if (d < minDist) { minDist = d; nearestBoss = BossController.Current; }
        }

        if (nearestEnemy != null && nearestBoss != null)
        {
            float dE = Vector2.Distance(fromPos, nearestEnemy.transform.position);
            float dB = Vector2.Distance(fromPos, nearestBoss.transform.position);
            return dE < dB ? nearestEnemy : nearestBoss;
        }
        else if (nearestEnemy != null) return nearestEnemy;
        else if (nearestBoss != null) return nearestBoss;
        else return null;
    }
}
