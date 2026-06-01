using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// WeaponPoint가 Slash VFX 스폰 직후 AddComponent로 부착.
/// 스폰 방향으로 살짝 밀려나가면서 알파 페이드 아웃.
/// VFX 프리팹에 Trigger Collider2D가 달려 있으면 OnTriggerEnter2D로 범위 피해를 처리합니다.
/// </summary>
public class SlashVfxBehaviour : MonoBehaviour
{
    private float    _duration;
    private float    _driftDistance;
    private Vector3  _startPos;
    private Vector3  _driftDir;
    private float    _spawnTime;

    private SpriteRenderer[] _renderers;
    private float[]          _originalAlphas;

    // ── 피해 파라미터 ──────────────────────────────────────────
    private float _attackDamage;
    private float _critRate;
    private float _critMult;
    private float _knockbackPower;
    private float _dotDamage;
    private float _lifeSteal;

    private readonly HashSet<EnemyController> _hitEnemies = new();
    private bool   _hitBoss;
    private string _weaponId;

    private const float FadeStartRatio = 0.4f;

    /// <param name="attackDamage">0이면 피해 처리 없이 시각 효과만.</param>
    public void Init(float duration, float driftDistance, Vector2 driftDir,
        float attackDamage = 0f, float critRate = 0f, float critMult = 2f,
        float knockbackPower = 0f, float dotDamage = 0f, float lifeSteal = 0f,
        string weaponId = "")
    {
        _duration       = Mathf.Max(duration, 0.01f);
        _driftDistance  = driftDistance;
        _startPos       = transform.position;
        _driftDir       = ((Vector3)driftDir).normalized;
        _attackDamage   = attackDamage;
        _critRate       = critRate;
        _critMult       = critMult;
        _knockbackPower = knockbackPower;
        _dotDamage      = dotDamage;
        _lifeSteal      = lifeSteal;
        _weaponId       = weaponId;

        _renderers      = GetComponentsInChildren<SpriteRenderer>(true);
        _originalAlphas = new float[_renderers.Length];
        for (int i = 0; i < _renderers.Length; i++)
            _originalAlphas[i] = _renderers[i].color.a;
    }

    private void Awake()
    {
        _spawnTime = Time.time;
    }

    private void Update()
    {
        float t = Mathf.Clamp01((Time.time - _spawnTime) / _duration);
        transform.position = _startPos + _driftDir * (_driftDistance * t);

        float fadeT = Mathf.Clamp01((t - FadeStartRatio) / (1f - FadeStartRatio));
        for (int i = 0; i < _renderers.Length; i++)
        {
            Color c = _renderers[i].color;
            c.a = _originalAlphas[i] * (1f - fadeT);
            _renderers[i].color = c;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (_attackDamage <= 0f) return;

        if (!_hitBoss)
        {
            var boss = other.GetComponentInParent<BossController>();
            if (boss != null)
            {
                var (dmg, crit) = RollCrit(_attackDamage);
                Vector2 knock = (Vector2)boss.transform.position - (Vector2)transform.position;
                boss.TakeDamage(dmg, crit, knock.sqrMagnitude > 0.001f ? knock.normalized : Vector2.up, _knockbackPower);
                ApplyLifeSteal(dmg);
                SoundManager.Shared?.PlayWeaponHit(_weaponId);
                _hitBoss = true;
                return;
            }
        }

        var enemy = other.GetComponentInParent<EnemyController>();
        if (enemy == null || _hitEnemies.Contains(enemy)) return;
        _hitEnemies.Add(enemy);

        var (eDmg, eCrit) = RollCrit(_attackDamage);
        Vector2 eKnock = (Vector2)enemy.transform.position - (Vector2)transform.position;
        enemy.TakeDamage(eDmg, eCrit, eKnock.sqrMagnitude > 0.001f ? eKnock.normalized : Vector2.up, _knockbackPower);
        ApplyLifeSteal(eDmg);
        if (_dotDamage > 0f) enemy.ApplyBleed(_dotDamage);
        SoundManager.Shared?.PlayWeaponHit(_weaponId);
    }

    private (float, bool) RollCrit(float baseDmg)
    {
        float playerCrit = PlayerController.Instance?.CritRateBonus ?? 0f;
        float total      = _critRate + playerCrit;
        bool  isCrit     = total > 0f && Random.value < total;
        return isCrit ? (baseDmg * _critMult, true) : (baseDmg, false);
    }

    private void ApplyLifeSteal(float dmg)
    {
        if (_lifeSteal <= 0f) return;
        PlayerController.Instance?.HealHP(dmg * _lifeSteal);
    }
}
