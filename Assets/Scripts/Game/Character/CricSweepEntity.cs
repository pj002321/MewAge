using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 크릭 스킬 — 맵을 가로지르는 소환체.
/// 닿은 적을 오른쪽(진행 방향)으로 1회 넉백합니다.
/// </summary>
public class CricSweepEntity : MonoBehaviour
{
    private float _targetX;
    private float _speed;
    private float _knockbackForce;
    private bool  _moving;
    private readonly HashSet<EnemyController> _pushed = new();

    private float _damage;

    public void Init(float targetX, float speed, float knockbackForce, float damage,
                     float delayRange = 0.35f, float speedVariance = 0.25f)
    {
        _targetX        = targetX;
        _knockbackForce = knockbackForce;
        _damage         = damage;
        _speed          = speed * Random.Range(1f - speedVariance, 1f + speedVariance);
        StartCoroutine(DelayedStart(Random.Range(0f, delayRange)));
    }

    private IEnumerator DelayedStart(float delay)
    {
        yield return new WaitForSeconds(delay);
        _moving = true;
    }

    private void Update()
    {
        if (!_moving) return;
        transform.Translate(Vector3.right * _speed * Time.deltaTime, Space.World);
        if (transform.position.x >= _targetX)
            Destroy(gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!_moving) return;
        var enemy = other.GetComponent<EnemyController>()
                 ?? other.GetComponentInParent<EnemyController>();
        if (enemy == null || !_pushed.Add(enemy)) return;

        enemy.TakeDamage(_damage);
        enemy.Knockback(Vector2.right, _knockbackForce);
    }
}
