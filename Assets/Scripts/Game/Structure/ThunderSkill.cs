using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Thunder 기물 스킬.
/// Activate() 호출 시 전체 조명을 살짝 어둡게 한 뒤
/// 씬의 모든 적 위치에 번개를 떨어뜨려 피해를 입힙니다.
/// Inspector: lightningPrefab(LightningEffect 프리팹), dimColor, dimDuration, strikesPerEnemy
/// StructureController + ThunderSkill 을 기물 프리팹에 함께 붙이세요.
/// </summary>
public class ThunderSkill : StructureSkillBase
{
    [Header("번개 프리팹")]
    [SerializeField] private GameObject lightningPrefab;
    [Tooltip("적 pivot 기준 번개 스폰 오프셋 (유닛)")]
    [SerializeField] private Vector2 lightningOffset = Vector2.zero;

    [Header("조명 어둡게")]
    [SerializeField] private float dimAmount   = 0.35f;   // 0=완전어둠, 1=그대로
    [SerializeField] private float dimDuration = 1.8f;    // 어두운 시간(초)
    [SerializeField] private float fadeDuration = 0.25f;  // 페이드 in/out 시간

    [Header("사운드")]
    [SerializeField] private AudioClip activateSound;

    [Header("번개 딜레이")]
    [SerializeField] private float strikeDelay = 0.1f;    // 적마다 번개 간격

    private bool _registered = false;

    // ── 진입점 ─────────────────────────────────────────────────────
    public override void Activate()
    {
        StartCoroutine(ThunderRoutine());
    }

    // ── 메인 루틴 ──────────────────────────────────────────────────
    private IEnumerator ThunderRoutine()
    {
        GlobalLightDimmer.Register();
        _registered = true;

        // 1) 페이드 아웃 (어둡게) + 사운드
        SoundManager.Shared?.PlayClip(activateSound);
        yield return StartCoroutine(FadeLight(1f, dimAmount, fadeDuration));

        // 2) 모든 적·보스에게 번개 동시 스폰 + 다단 피해
        int hitCount = Random.Range(3, 6); // 3~5회

        var enemies = EnemyController.All;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            SpawnLightning(enemy.transform.position, enemy.transform);
            float totalDamage = enemy.MaxHP * (mainValue / 100f);
            enemy.Stun(subValue);
            StartCoroutine(MultiStrikeEnemy(enemy, totalDamage, hitCount, subValue));
        }

        var currentBoss = BossController.Current;
        if (currentBoss != null && currentBoss.gameObject.activeInHierarchy)
        {
            SpawnLightning(currentBoss.transform.position, currentBoss.transform);
            float bossTotalDamage = currentBoss.MaxHPValue * (mainValue / 100f);
            StartCoroutine(MultiStrikeBoss(currentBoss, bossTotalDamage, hitCount, subValue));
        }

        // 연타 완료까지 대기 (hitCount × hitInterval)
        yield return new WaitForSeconds(hitCount * 0.05f + 0.1f);

        // 3) 페이드 인 (복구) 후 즉시 파괴
        yield return StartCoroutine(FadeLight(dimAmount, 1f, fadeDuration));

        if (_registered) { GlobalLightDimmer.Release(); _registered = false; }
        Destroy(gameObject);
    }

    // ── 번개 비주얼 스폰 ────────────────────────────────────────────
    private void SpawnLightning(Vector3 worldPos, Transform parent)
    {
        if (lightningPrefab == null) return;
        var go = Instantiate(lightningPrefab, worldPos + (Vector3)lightningOffset, Quaternion.identity);
        go.transform.SetParent(parent, true);
        go.AddComponent<WorldRotationLock>();   // 부모 회전 무시
    }

    // ── 조명 페이드 ─────────────────────────────────────────────────
    private IEnumerator FadeLight(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            GlobalLightDimmer.SetIntensity(Mathf.Lerp(from, to, elapsed / duration));
            yield return null;
        }
        GlobalLightDimmer.SetIntensity(to);
    }

    // ── 다단 히트 연출 ──────────────────────────────────────────────
    private IEnumerator MultiStrikeEnemy(EnemyController enemy, float totalDamage, int hitCount, float stunDur)
    {
        if (enemy == null) yield break;

        // 마비 먼저 적용 (기본 subValue)
        enemy.Stun(stunDur);

        float damagePerHit = totalDamage / hitCount;
        float hitInterval  = 0.05f; // "파파파박" 빠른 연타 (약 20fps)

        for (int i = 0; i < hitCount; i++)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) break;

            // 데미지 적용
            enemy.TakeDamage(damagePerHit);
            FXManager.Shared?.SpawnHitEffect(enemy.transform.position);
            
            // 효과음 (너무 겹치면 시끄러울 수 있으니 필요시 제한 가능)
            if (i % 2 == 0) SoundManager.Shared?.PlayClip(activateSound);

            yield return new WaitForSeconds(hitInterval);
        }
        
        this.DLog($"MultiStrike Completed on {enemy.name}: {totalDamage} damage over {hitCount} hits.");
    }

    private IEnumerator MultiStrikeBoss(BossController boss, float totalDamage, int hitCount, float stunDur)
    {
        if (boss == null) yield break;

        // 보스는 스턴 면역일 수 있으나 일단 시도
        boss.TakeDamage(0, false, Vector2.zero, 0f); // 피격 효과용

        float damagePerHit = totalDamage / hitCount;
        float hitInterval  = 0.05f;

        for (int i = 0; i < hitCount; i++)
        {
            if (boss == null || !boss.gameObject.activeInHierarchy) break;

            boss.TakeDamage(damagePerHit);
            FXManager.Shared?.SpawnHitEffect(boss.transform.position);
            
            if (i % 2 == 0) SoundManager.Shared?.PlayClip(activateSound);

            yield return new WaitForSeconds(hitInterval);
        }
        
        this.DLog($"MultiStrike Completed on Boss: {totalDamage} damage over {hitCount} hits.");
    }

    // ── 파괴 시 조명 복구 보장 ──────────────────────────────────────
    private void OnDestroy()
    {
        if (_registered) { GlobalLightDimmer.Release(); _registered = false; }
    }
}
