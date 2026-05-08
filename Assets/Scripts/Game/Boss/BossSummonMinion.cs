using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 소환 스킬로 생성된 소환체.
/// Init() 호출 후 duration초 동안 플레이어를 추격하다 스스로 소멸합니다.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
public class BossSummonMinion : MonoBehaviour
{
    [Header("HP")]
    [SerializeField] private float maxHP = 30f;

    [Header("분리 (간격 유지)")]
    [SerializeField] private float separationRadius   = 1.0f;
    [SerializeField] private float separationStrength = 6f;

    private float       _hp;
    private float       _duration;
    private float       _moveSpeed;
    private float       _contactDamage;
    private float       _damageTimer;
    private float       _damageCooldown = 0.5f;
    private bool        _dead;
    private Rigidbody2D _rb;

    // 분리 스로틀 (EnemyController와 동일 방식)
    private float       _sepTimer;
    private const float SepInterval = 0.05f;

    public static readonly List<BossSummonMinion> All = new();

    public void Init(float duration, float moveSpeed, float contactDamage)
    {
        _hp            = maxHP;
        _duration      = duration;
        _moveSpeed     = moveSpeed;
        _contactDamage = contactDamage;
        _rb            = GetComponent<Rigidbody2D>();
        All.Add(this);

        if (_rb != null)
        {
            _rb.bodyType    = RigidbodyType2D.Kinematic;
            _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        StartCoroutine(LifeRoutine());
    }

    public void TakeDamage(float damage)
    {
        if (_dead) return;
        _hp -= damage;
        if (_hp <= 0f) StartCoroutine(DieRoutine());
    }

    private void Update()
    {
        if (_damageTimer > 0f) _damageTimer -= Time.deltaTime;
    }

    private void FixedUpdate()
    {
        if (_dead) return;
        var player = PlayerController.Instance;
        if (player == null || _rb == null) return;

        Vector2 dir  = ((Vector2)player.transform.position - _rb.position).normalized;
        Vector2 move = dir * _moveSpeed * Time.fixedDeltaTime;
        move += CalcSeparation();
        _rb.MovePosition(_rb.position + move);

        if (dir.x > 0.01f || dir.x < -0.01f)
        {
            var s = transform.localScale;
            s.x = dir.x < 0f ? -Mathf.Abs(s.x) : Mathf.Abs(s.x);
            transform.localScale = s;
        }
    }

    private Vector2 CalcSeparation()
    {
        if (separationRadius <= 0f || _rb == null) return Vector2.zero;

        _sepTimer -= Time.fixedDeltaTime;
        if (_sepTimer > 0f) return Vector2.zero;
        _sepTimer = SepInterval;

        float   r2   = separationRadius * separationRadius;
        Vector2 push = Vector2.zero;

        // 다른 소환체와의 분리
        for (int i = 0; i < All.Count; i++)
        {
            var other = All[i];
            if (other == this || other._rb == null) continue;
            Vector2 diff   = _rb.position - other._rb.position;
            float   sqDist = diff.sqrMagnitude;
            if (sqDist > 0.000001f && sqDist < r2)
            {
                float dist = Mathf.Sqrt(sqDist);
                push += (diff / dist) * (1f - dist / separationRadius);
            }
        }

        // 일반 적과의 분리
        for (int i = 0; i < EnemyController.All.Count; i++)
        {
            var other = EnemyController.All[i];
            if (other == null || other.Rb == null) continue;
            Vector2 diff   = _rb.position - other.Rb.position;
            float   sqDist = diff.sqrMagnitude;
            if (sqDist > 0.000001f && sqDist < r2)
            {
                float dist = Mathf.Sqrt(sqDist);
                push += (diff / dist) * (1f - dist / separationRadius);
            }
        }

        return push * separationStrength * SepInterval;
    }

    private void OnDestroy()
    {
        All.Remove(this);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (_dead || _damageTimer > 0f) return;
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        if (other.gameObject != p.gameObject) return;
        _damageTimer = _damageCooldown;
        p.TakeDamage(_contactDamage);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        if (_dead || _damageTimer > 0f) return;
        var p = collision.collider.GetComponentInParent<PlayerController>();
        if (p == null) return;
        _damageTimer = _damageCooldown;
        p.TakeDamage(_contactDamage);
    }

    private IEnumerator LifeRoutine()
    {
        yield return new WaitForSeconds(_duration);
        if (!_dead) StartCoroutine(DieRoutine());
    }

    private IEnumerator DieRoutine()
    {
        _dead = true;
        if (_rb != null) _rb.linearVelocity = Vector2.zero;

        float   elapsed   = 0f;
        Vector3 initScale = transform.localScale;
        while (elapsed < 0.3f)
        {
            float t = elapsed / 0.3f;
            transform.localScale = Vector3.Lerp(initScale, Vector3.zero, t * t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
