using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 수류탄 투사체. 포물선 호를 그리며 날아가 착지 시 범위 피해 + 파편 + 원형 플래시를 발생시킵니다.
/// WeaponRangedBehaviour.useGrenadeMode = true 시 사용됩니다.
/// </summary>
public class GrenadeProjectile : MonoBehaviour
{
    public static readonly List<GrenadeProjectile> All = new();

    private SpriteRenderer _sr;
    private float  _primaryDmg;
    private float  _baseDamage;
    private float  _areaHitRadius;
    private float  _areaHitDamageRatio;
    private float  _criticalRate;
    private float  _criticalMult;
    private bool   _isPrimaryCritical;
    private float  _knockbackPower;
    private float  _dotDamage;
    private float  _lifeSteal;
    private string _weaponId;

    private Vector3 _startPos;
    private Vector3 _ctrlPos;
    private Vector3 _targetPos;
    private float   _duration;
    private float   _elapsed;
    private bool    _hasExploded;
    private float   _spinSpeed;

    private System.Action _onExplode;

    public void Init(SpriteRenderer sourceSr,
                     float primaryDmg, float baseDamage, bool isPrimaryCritical,
                     float areaHitRadius, float areaHitDamageRatio,
                     float criticalRate, float criticalMult,
                     float knockbackPower, float dotDamage, float lifeSteal,
                     string weaponId,
                     Vector3 targetPos, float speed, float arcHeight,
                     System.Action onExplode)
    {
        _sr                = gameObject.AddComponent<SpriteRenderer>();
        _sr.sprite         = sourceSr.sprite;
        _sr.color          = sourceSr.color;
        _sr.flipX          = sourceSr.flipX;
        _sr.flipY          = sourceSr.flipY;
        _sr.sortingLayerID = sourceSr.sortingLayerID;
        _sr.sortingOrder   = sourceSr.sortingOrder;

        var s = sourceSr.transform.lossyScale;
        transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), 1f);

        _primaryDmg         = primaryDmg;
        _baseDamage         = baseDamage;
        _isPrimaryCritical  = isPrimaryCritical;
        _areaHitRadius      = areaHitRadius;
        _areaHitDamageRatio = areaHitDamageRatio;
        _criticalRate       = criticalRate;
        _criticalMult       = criticalMult;
        _knockbackPower     = knockbackPower;
        _dotDamage          = dotDamage;
        _lifeSteal          = lifeSteal;
        _weaponId           = weaponId;
        _onExplode          = onExplode;

        _startPos  = transform.position;
        _targetPos = targetPos;
        float dist = Vector3.Distance(_startPos, _targetPos);
        _duration  = Mathf.Max(0.3f, dist / speed);
        _ctrlPos   = (_startPos + _targetPos) * 0.5f + Vector3.up * arcHeight;
        _spinSpeed  = Random.Range(400f, 600f);
    }

    private void Awake() => All.Add(this);

    private void OnDestroy()
    {
        All.Remove(this);
        // 웨이브 종료 등으로 강제 파괴될 때 무기 스프라이트를 복구합니다.
        if (!_hasExploded) _onExplode?.Invoke();
    }

    private void Update()
    {
        if (_hasExploded) return;

        _elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(_elapsed / _duration);

        float u = 1f - t;
        transform.position = u * u * _startPos + 2f * u * t * _ctrlPos + t * t * _targetPos;
        transform.Rotate(0f, 0f, _spinSpeed * Time.deltaTime);

        if (t >= 1f) Explode();
    }

    private void Explode()
    {
        _hasExploded = true;

        int layerID = _sr != null ? _sr.sortingLayerID : 0;

        if (_sr != null && _sr.sprite != null)
            SpriteSliceFX.PlayExplosion(_sr, 3, 3);
        if (_sr != null) _sr.enabled = false;

        if (_areaHitRadius > 0f)
            CircleFlashFX.Play(transform.position, _areaHitRadius, layerID);

        ApplyAreaDamage();
        SoundManager.Shared?.PlayWeaponHit(_weaponId);

        _onExplode?.Invoke();
        Destroy(gameObject);
    }

    private void ApplyAreaDamage()
    {
        // 보스
        var boss = BossController.Current;
        if (boss != null && boss.gameObject.activeInHierarchy)
        {
            float dist = Vector2.Distance(transform.position, boss.transform.position);
            if (dist <= _areaHitRadius)
            {
                boss.TakeDamage(_primaryDmg, _isPrimaryCritical);
                if (_lifeSteal > 0f) PlayerController.Instance?.HealHP(_primaryDmg * _lifeSteal);
                if (_dotDamage > 0f) boss.ApplyBleed(_dotDamage);
            }
        }

        // 폭발 반경 내 적에게 범위 피해 (가장 가까운 적 = 주 대상)
        int colCount = Physics2D.OverlapCircleNonAlloc(
            transform.position, _areaHitRadius,
            WeaponPoint.SharedHitBuffer,
            1 << LayerMask.NameToLayer("Enemy"));

        EnemyController primaryEnemy = FindPrimaryEnemy(colCount);

        for (int i = 0; i < colCount; i++)
        {
            if (!WeaponPoint.SharedHitBuffer[i].TryGetComponent<EnemyController>(out var enemy)) continue;

            bool  isPrimary = (enemy == primaryEnemy);
            float dmg;
            bool  isCrit;
            if (isPrimary)
            {
                dmg    = _primaryDmg;
                isCrit = _isPrimaryCritical;
            }
            else
            {
                (dmg, isCrit) = RollCrit(_baseDamage * _areaHitDamageRatio);
            }

            Vector2 knockDir = (Vector2)enemy.transform.position - (Vector2)transform.position;
            if (knockDir.sqrMagnitude < 0.001f) knockDir = Vector2.up;

            enemy.TakeDamage(dmg, isCrit, knockDir.normalized, _knockbackPower);
            if (_lifeSteal > 0f) PlayerController.Instance?.HealHP(dmg * _lifeSteal);
            if (_dotDamage > 0f) enemy.ApplyBleed(_dotDamage);
        }
    }

    private EnemyController FindPrimaryEnemy(int colCount)
    {
        EnemyController primary = null;
        float bestSq = float.MaxValue;
        for (int i = 0; i < colCount; i++)
        {
            if (!WeaponPoint.SharedHitBuffer[i].TryGetComponent<EnemyController>(out var e)) continue;
            float sq = ((Vector2)e.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (sq < bestSq) { bestSq = sq; primary = e; }
        }
        return primary;
    }

    private (float dmg, bool crit) RollCrit(float baseDmg)
    {
        bool crit = Random.value < _criticalRate / 100f;
        return (crit ? baseDmg * _criticalMult : baseDmg, crit);
    }
}
