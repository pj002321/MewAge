using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// FireSpreadSkill이 생성한 각 타일에 동적으로 부착되는 컴포넌트.
/// OnTriggerStay2D로 타일 위를 밟는 적을 감지하고 피해 + 화염 출혈을 적용합니다.
/// </summary>
[RequireComponent(typeof(BoxCollider2D))]
public class FireTile : MonoBehaviour
{
    private float _damage;
    private float _bleedDamage;
    private float _bleedDuration;
    private float _bleedInterval;
    private float _hitInterval;   // 같은 적에게 재적용하는 최소 간격

    // instanceID → 마지막 피격 시각
    private readonly Dictionary<int, float> _lastHit = new();

    public void Init(float damage, float bleedDamage, float bleedDuration,
                     float bleedInterval, float hitInterval)
    {
        _damage       = damage;
        _bleedDamage  = bleedDamage;
        _bleedDuration = bleedDuration;
        _bleedInterval = bleedInterval;
        _hitInterval  = hitInterval;
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        float now = Time.time;

        var enemy = other.GetComponentInParent<EnemyController>();
        if (enemy != null)
        {
            int id = enemy.GetInstanceID();
            if (_lastHit.TryGetValue(id, out float last) && now - last < _hitInterval) return;
            _lastHit[id] = now;
            enemy.TakeDamage(_damage);
            enemy.ApplyFireBleed(_bleedDamage, _bleedDuration, _bleedInterval);
            return;
        }

        var boss = other.GetComponentInParent<BossController>();
        if (boss != null)
        {
            int id = boss.GetInstanceID();
            if (_lastHit.TryGetValue(id, out float last) && now - last < _hitInterval) return;
            _lastHit[id] = now;
            boss.TakeDamage(_damage);
        }
    }
}
