using System.Collections;
using UnityEngine;

/// <summary>
/// Lightning Projectile that deals percentage-based damage over multiple hits.
/// Rule #3: Implements State Pattern for the zapping sequence.
/// Rule #7: Uses this.DLog for tracing.
/// </summary>
public class LightningProjectile : MonoBehaviour
{
    private enum LightningState { Setup, Zapping, Finished }
    private LightningState _currentState = LightningState.Setup;

    [Header("Settings")]
    [SerializeField] private float duration = 0.5f;
    [SerializeField] private int minHits = 10;
    [SerializeField] private int maxHits = 20;

    private EnemyController _targetEnemy;
    private BossController _targetBoss;
    private float _totalDamage;
    private bool _isCrit;
    private int _hitCount;
    private float _damagePerHit;

    public void Init(EnemyController target, float totalDamage, bool isCrit = false)
    {
        _targetEnemy = target;
        _totalDamage = totalDamage;
        _isCrit = isCrit;
        ChangeState(LightningState.Setup);
    }

    public void Init(BossController target, float totalDamage, bool isCrit = false)
    {
        _targetBoss = target;
        _totalDamage = totalDamage;
        _isCrit = isCrit;
        ChangeState(LightningState.Setup);
    }

    private void Update()
    {
        // Follow target
        if (_targetEnemy != null) transform.position = _targetEnemy.transform.position;
        else if (_targetBoss != null) transform.position = _targetBoss.transform.position;
        else if (_currentState != LightningState.Finished) ChangeState(LightningState.Finished);
    }

    private void ChangeState(LightningState newState)
    {
        _currentState = newState;
        this.DLog($"State Changed to: {newState}");

        switch (newState)
        {
            case LightningState.Setup:
                HandleSetup();
                break;
            case LightningState.Zapping:
                StartCoroutine(ZapRoutine());
                break;
            case LightningState.Finished:
                Destroy(gameObject);
                break;
        }
    }

    private void HandleSetup()
    {
        _hitCount = Random.Range(minHits, maxHits + 1);
        _damagePerHit = _totalDamage / _hitCount;

        this.DLog($"Setup: Total Damage ({_totalDamage}), Hits ({_hitCount}), DamagePerHit ({_damagePerHit:F2})");
        ChangeState(LightningState.Zapping);
    }

    private IEnumerator ZapRoutine()
    {
        float interval = duration / _hitCount;

        for (int i = 0; i < _hitCount; i++)
        {
            if (_targetEnemy == null && _targetBoss == null) break;

            ApplyDamage();
            yield return new WaitForSeconds(interval);
        }

        ChangeState(LightningState.Finished);
    }

    private void ApplyDamage()
    {
        if (_targetEnemy != null)
        {
            // Simple damage without critical/knockback for the rapid zap
            _targetEnemy.TakeDamage(_damagePerHit, _isCrit);
            FXManager.Shared?.SpawnHitEffect(_targetEnemy.transform.position);
        }
        else if (_targetBoss != null)
        {
            _targetBoss.TakeDamage(_damagePerHit, _isCrit);
            FXManager.Shared?.SpawnHitEffect(_targetBoss.transform.position);
        }
    }
}

/* 
Unity Implementation Guide:
1. Create a Prefab named "LightningProjectile".
2. Attach this script.
3. Add a LightningVisual component (to be created) for FX.
4. Resources/Weapons/ folder should have the weapon prefab that spawns this.
*/
