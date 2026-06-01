using UnityEngine;

/// <summary>
/// Magic+Fire 전용 유도 투사체.
/// Init() 후 매 프레임 타겟을 추적하며 충돌 시 데미지를 줍니다.
/// spriteForward로 스프라이트 기본 전방향을 지정하면 비행 방향으로 자동 회전합니다.
/// </summary>
public class FireHomingProjectile : MonoBehaviour
{
    public enum SpriteForward { Up, Right, Down, Left }

    [SerializeField] private float         turnSpeed     = 300f;
    [SerializeField] private float         hitRadius     = 0.25f;
    [SerializeField] private SpriteForward spriteForward = SpriteForward.Up;

    private float ForwardAngle => spriteForward switch
    {
        SpriteForward.Up    =>  90f,
        SpriteForward.Right =>   0f,
        SpriteForward.Down  => -90f,
        SpriteForward.Left  => 180f,
        _                   =>  90f,
    };

    private float  _damage;
    private float  _speed;
    private float  _lifeSteal;
    private float  _dotDamage;
    private float  _knockbackPower;
    private float  _critRate;
    private float  _critMult;
    private string _weaponId;

    // object 대신 타입 분리 — Unity의 == null 연산자가 올바르게 작동
    private EnemyController _targetEnemy;
    private BossController  _targetBoss;

    private Vector2 _velocity;

    public void Init(float damage, float speed, EnemyController target,
        float lifeSteal = 0f, float dotDamage = 0f,
        float knockbackPower = 0f, float critRate = 0f, float critMult = 2f, string weaponId = "")
    {
        _damage         = damage;
        _speed          = speed;
        _targetEnemy    = target;
        _lifeSteal      = lifeSteal;
        _dotDamage      = dotDamage;
        _knockbackPower = knockbackPower;
        _critRate       = critRate;
        _critMult       = critMult;
        _weaponId       = weaponId;
        _velocity       = target != null
            ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized * speed
            : Vector2.up * speed;
        Destroy(gameObject, 6f);
    }

    public void InitBoss(float damage, float speed, BossController target,
        float lifeSteal = 0f, float dotDamage = 0f,
        float knockbackPower = 0f, float critRate = 0f, float critMult = 2f, string weaponId = "")
    {
        _damage         = damage;
        _speed          = speed;
        _targetBoss     = target;
        _lifeSteal      = lifeSteal;
        _dotDamage      = dotDamage;
        _knockbackPower = knockbackPower;
        _critRate       = critRate;
        _critMult       = critMult;
        _weaponId       = weaponId;
        _velocity       = target != null
            ? ((Vector2)target.transform.position - (Vector2)transform.position).normalized * speed
            : Vector2.up * speed;
        Destroy(gameObject, 6f);
    }

    private void Update()
    {
        // 타겟이 소멸·풀링·비활성화되면 즉시 파괴
        bool alive = (_targetEnemy != null && _targetEnemy.gameObject.activeInHierarchy)
                  || (_targetBoss  != null && _targetBoss.gameObject.activeInHierarchy);
        if (!alive) { Destroy(gameObject); return; }

        Vector2 targetPos = GetTargetPos();

        // 유도: 현재 방향을 타겟 방향으로 서서히 회전
        Vector2 desiredDir = (targetPos - (Vector2)transform.position).normalized;
        float   maxTurn    = turnSpeed * Time.deltaTime;
        Vector2 newDir     = Vector2.MoveTowards(_velocity.normalized, desiredDir, maxTurn * Mathf.Deg2Rad);
        _velocity = newDir.normalized * _speed;

        transform.position += (Vector3)(_velocity * Time.deltaTime);

        // 스프라이트 방향 (spriteForward 기준으로 회전)
        float angle = Mathf.Atan2(_velocity.y, _velocity.x) * Mathf.Rad2Deg - ForwardAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);

        // 타격 판정
        if (Vector2.Distance(transform.position, targetPos) <= hitRadius)
            Hit();
    }

    private void Hit()
    {
        bool  crit     = _critRate > 0f && Random.value < _critRate;
        float finalDmg = crit ? _damage * _critMult : _damage;
        Vector2 knockDir = _velocity.sqrMagnitude > 0.001f ? _velocity.normalized : Vector2.up;

        if (_targetEnemy != null)
        {
            _targetEnemy.TakeDamage(finalDmg, crit, knockDir, _knockbackPower);
            if (_lifeSteal > 0f) PlayerController.Instance?.HealHP(finalDmg * _lifeSteal);
            if (_dotDamage > 0f) _targetEnemy.ApplyBleed(_dotDamage);
        }
        else if (_targetBoss != null)
        {
            _targetBoss.TakeDamage(finalDmg, crit, knockDir, _knockbackPower);
            if (_lifeSteal > 0f) PlayerController.Instance?.HealHP(finalDmg * _lifeSteal);
        }

        SoundManager.Shared?.PlayWeaponHit(_weaponId);
        Destroy(gameObject);
    }

    private Vector2 GetTargetPos()
    {
        if (_targetEnemy != null) return _targetEnemy.transform.position;
        if (_targetBoss  != null) return _targetBoss.transform.position;
        return transform.position;
    }
}
