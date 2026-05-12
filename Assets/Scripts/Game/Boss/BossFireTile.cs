using UnityEngine;

/// <summary>
/// BossFireCrossSkill이 동적으로 부착하는 화염 타일 피해 컴포넌트.
/// OnTriggerStay2D 로 플레이어에게 tickInterval 간격으로 피해를 줍니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class BossFireTile : MonoBehaviour
{
    private float _damage;
    private float _interval;
    private float _timer;

    public void Init(float damage, float interval)
    {
        _damage   = damage;
        _interval = interval;
        _timer    = 0f;
    }

    private void Update()
    {
        if (_timer > 0f) _timer -= Time.deltaTime;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (_timer > 0f) return;
        var p = other.GetComponentInParent<PlayerController>();
        if (p == null) return;
        p.TakeDamage(_damage, sourceName: gameObject.name);
        _timer = _interval;
    }
}
