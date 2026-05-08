using UnityEngine;

/// <summary>
/// 신화 스킬: 타격 시 일정 확률로 스파이크를 꽂습니다.
///   - 대상 생존 중: 스파이크가 몬스터에 달라붙어 틱 피해.
///   - 대상 사망 시: 스파이크 위치에서 주변 몬스터에게 즉발 폭발 피해.
/// </summary>
public class MythicSpikeSkill : WeaponMythicSkill
{
    [SerializeField] private float      chance       = 0.2f;
    [SerializeField] private GameObject spikePrefab;
    [SerializeField] private float      tickDamage   = 5f;
    [SerializeField] private float      tickInterval = 0.5f;
    [SerializeField] private float      duration     = 5f;
    [SerializeField] private float      deathAoeRadius  = 2.5f;
    [SerializeField] private float      deathAoeDamage  = 20f;

    public override void OnBossHit(BossController boss, Vector2 hitDir)
    {
        if (boss == null || spikePrefab == null) return;
        if (Random.value > chance) return;

        var spike = Instantiate(spikePrefab, boss.transform.position, Quaternion.identity);
        spike.AddComponent<SpikeTicker>().InitBoss(boss, tickDamage, tickInterval, duration);
    }

    public override void OnEnemyHit(EnemyController enemy, Vector2 hitDir)
    {
        if (enemy == null || spikePrefab == null) return;
        if (Random.value > chance) return;

        var spike = Instantiate(spikePrefab, enemy.transform.position, Quaternion.identity);
        spike.AddComponent<SpikeTicker>().InitEnemy(enemy, tickDamage, tickInterval, duration, deathAoeRadius, deathAoeDamage);
    }
}

/// <summary>스파이크가 대상에 달라붙어 틱 피해를 주고, 대상 사망 시 주변 폭발.</summary>
public class SpikeTicker : MonoBehaviour
{
    private BossController  _boss;
    private EnemyController _enemy;
    private float           _dmg;
    private float           _interval;
    private float           _timer;
    private float           _aoeRadius;
    private float           _aoeDmg;
    private bool            _targetDied;

    private static readonly Collider2D[] _aoeBuffer = new Collider2D[24];

    public void InitBoss(BossController boss, float dmg, float interval, float duration)
    {
        _boss     = boss;
        _dmg      = dmg;
        _interval = interval;
        _timer    = interval;
        Destroy(gameObject, duration);
    }

    public void InitEnemy(EnemyController enemy, float dmg, float interval, float duration,
                          float aoeRadius, float aoeDmg)
    {
        _enemy     = enemy;
        _dmg       = dmg;
        _interval  = interval;
        _timer     = interval;
        _aoeRadius = aoeRadius;
        _aoeDmg    = aoeDmg;
        enemy.OnDeath += OnTargetDied;
        Destroy(gameObject, duration);
    }

    private void OnTargetDied()
    {
        _targetDied = true;
        if (_enemy != null) { _enemy.OnDeath -= OnTargetDied; }
        DealAoe(transform.position);
        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (_enemy != null && !_targetDied)
            _enemy.OnDeath -= OnTargetDied;
    }

    private void Update()
    {
        if (_targetDied) return;

        if (_boss != null)
        {
            if (!_boss.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            transform.position = _boss.transform.position;
            Tick(() => _boss.TakeDamage(_dmg, false));
        }
        else if (_enemy != null)
        {
            if (!_enemy.gameObject.activeInHierarchy) { Destroy(gameObject); return; }
            transform.position = _enemy.transform.position;
            Tick(() => _enemy.TakeDamage(_dmg, false));
        }
    }

    private void Tick(System.Action dealDamage)
    {
        _timer -= Time.deltaTime;
        if (_timer <= 0f) { dealDamage(); _timer = _interval; }
    }

    private void DealAoe(Vector3 pos)
    {
        if (_aoeRadius <= 0f || _aoeDmg <= 0f) return;
        int count = Physics2D.OverlapCircleNonAlloc(pos, _aoeRadius, _aoeBuffer);
        for (int i = 0; i < count; i++)
        {
            if (_aoeBuffer[i] == null) continue;
            var b = _aoeBuffer[i].GetComponentInParent<BossController>();
            if (b != null && b.gameObject.activeInHierarchy) { b.TakeDamage(_aoeDmg, false); continue; }
            var e = _aoeBuffer[i].GetComponentInParent<EnemyController>();
            if (e != null && e.gameObject.activeInHierarchy) e.TakeDamage(_aoeDmg, false);
        }
    }
}
