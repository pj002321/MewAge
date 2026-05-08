using System.Collections;
using UnityEngine;

/// <summary>
/// BOSS_SPIKE 특수효과 — 보스 또는 일반 적 위에서 낙하 후 연타 5회 피해를 입힙니다.
/// WeaponPoint.TrySpecialEffectBoss / TrySpecialEffectEnemy에서 생성합니다.
/// </summary>
public class BossSpike : MonoBehaviour
{
    private SpriteRenderer  _sr;
    private BossController  _boss;
    private EnemyController _enemy;
    private float           _damage;

    private const int   RapidHitCount    = 5;
    private const float RapidHitInterval = 0.06f;

    public static void Spawn(BossController boss, Sprite weaponSprite, float damage, GameObject spikePrefab = null)
    {
        if (boss == null) return;
        var spike = Create(boss.transform.position, weaponSprite, spikePrefab,
                           boss.GetComponentInChildren<SpriteRenderer>());
        if (spike == null) return;
        spike._boss   = boss;
        spike._damage = damage;
        spike.StartCoroutine(spike.SlamRoutine());
    }

    public static void SpawnOnEnemy(EnemyController enemy, Sprite weaponSprite, float damage, GameObject spikePrefab = null)
    {
        if (enemy == null) return;
        var spike = Create(enemy.transform.position, weaponSprite, spikePrefab,
                           enemy.GetComponentInChildren<SpriteRenderer>());
        if (spike == null) return;
        spike._enemy  = enemy;
        spike._damage = damage;
        spike.StartCoroutine(spike.SlamRoutine());
    }

    private static BossSpike Create(Vector3 targetPos, Sprite weaponSprite, GameObject spikePrefab, SpriteRenderer refSr)
    {
        GameObject go;
        SpriteRenderer sr;

        if (spikePrefab != null)
        {
            go = Instantiate(spikePrefab, targetPos + Vector3.up * 6f, Quaternion.identity);
            sr = go.GetComponentInChildren<SpriteRenderer>();
        }
        else
        {
            if (weaponSprite == null) return null;
            go = new GameObject("BossSpike");
            sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = weaponSprite;
            go.transform.position   = targetPos + Vector3.up * 6f;
            go.transform.localScale = Vector3.one * 3f;
        }

        if (sr != null && refSr != null)
        {
            sr.sortingLayerID = refSr.sortingLayerID;
            sr.sortingOrder   = refSr.sortingOrder + 5;
        }

        var spike = go.AddComponent<BossSpike>();
        spike._sr = sr;
        return spike;
    }

    private IEnumerator SlamRoutine()
    {
        Transform target = _boss != null ? _boss.transform : _enemy?.transform;
        if (target == null) { Destroy(gameObject); yield break; }

        Vector3 startPos = transform.position;

        // 예비 동작
        yield return new WaitForSeconds(0.15f);

        // 낙하
        float elapsed = 0f, duration = 0.35f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            if (target == null) { Destroy(gameObject); yield break; }
            float t     = elapsed / duration;
            float eased = t * t;
            transform.position = Vector3.Lerp(startPos, target.position, eased);
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Sin(elapsed * 25f) * 6f);
            yield return null;
        }

        // ── 연타 파바바박 5회 ─────────────────────────────────────
        float singleDmg = _damage / RapidHitCount;
        for (int i = 0; i < RapidHitCount; i++)
        {
            if (_boss != null && _boss.gameObject.activeInHierarchy)
            {
                _boss.TakeDamage(singleDmg, false);
                FXManager.Shared?.SpawnHitEffect(_boss.transform.position);
            }
            else if (_enemy != null && _enemy.gameObject.activeInHierarchy)
            {
                _enemy.TakeDamage(singleDmg, false, Vector2.zero, 0f);
                FXManager.Shared?.SpawnHitEffect(_enemy.transform.position);
            }
            else break;

            if (i < RapidHitCount - 1)
                yield return new WaitForSeconds(RapidHitInterval);
        }

        // 페이드 아웃
        if (_sr != null)
        {
            float fadeElapsed = 0f, fadeDuration = 0.2f;
            Color c = _sr.color;
            while (fadeElapsed < fadeDuration)
            {
                fadeElapsed += Time.deltaTime;
                c.a = 1f - fadeElapsed / fadeDuration;
                _sr.color = c;
                yield return null;
            }
        }

        Destroy(gameObject);
    }
}
