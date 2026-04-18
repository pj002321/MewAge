using System.Collections;
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

    private float            _hp;
    private float            _duration;
    private float            _moveSpeed;
    private float            _contactDamage;
    private float            _damageTimer;
    private float            _damageCooldown = 0.5f;
    private bool             _dead;
    private Rigidbody2D      _rb;
    private SpriteRenderer[] _srs;

    public void Init(float duration, float moveSpeed, float contactDamage)
    {
        _hp            = maxHP;
        _duration      = duration;
        _moveSpeed     = moveSpeed;
        _contactDamage = contactDamage;
        _rb            = GetComponent<Rigidbody2D>();
        _srs           = GetComponentsInChildren<SpriteRenderer>(true);

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

        Vector2 dir = ((Vector2)player.transform.position - _rb.position).normalized;
        _rb.MovePosition(_rb.position + dir * _moveSpeed * Time.fixedDeltaTime);

        if (_srs != null)
        {
            bool flipX = dir.x < -0.01f;
            foreach (var sr in _srs) sr.flipX = flipX;
        }
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
