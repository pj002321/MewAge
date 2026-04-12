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

    [Header("조명 어둡게")]
    [SerializeField] private float dimAmount   = 0.35f;   // 0=완전어둠, 1=그대로
    [SerializeField] private float dimDuration = 1.8f;    // 어두운 시간(초)
    [SerializeField] private float fadeDuration = 0.25f;  // 페이드 in/out 시간

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
        SoundManager.Shared?.Play(SoundKey.Structure_Thunder);
        yield return StartCoroutine(FadeLight(1f, dimAmount, fadeDuration));

        // 2) 모든 적에게 번개
        var enemies = EnemyController.All;
        for (int i = enemies.Count - 1; i >= 0; i--)
        {
            var enemy = enemies[i];
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            SpawnLightning(enemy.transform.position, enemy.transform);
            enemy.TakeDamage(mainValue);
            enemy.Stun(subValue);   // subValue = 마비 지속시간(초)

            yield return new WaitForSeconds(strikeDelay);
        }

        // 조명이 어두운 상태 잠깐 유지
        float remaining = dimDuration - fadeDuration - (enemies.Count * strikeDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 3) 페이드 인 (복구)
        yield return StartCoroutine(FadeLight(dimAmount, 1f, fadeDuration));

        if (_registered) { GlobalLightDimmer.Release(); _registered = false; }
    }

    // ── 번개 비주얼 스폰 ────────────────────────────────────────────
    private void SpawnLightning(Vector3 worldPos, Transform parent)
    {
        if (lightningPrefab == null) return;
        var go = Instantiate(lightningPrefab, worldPos, Quaternion.identity);
        go.transform.SetParent(parent, true);
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

    // ── 파괴 시 조명 복구 보장 ──────────────────────────────────────
    private void OnDestroy()
    {
        if (_registered) { GlobalLightDimmer.Release(); _registered = false; }
    }
}
