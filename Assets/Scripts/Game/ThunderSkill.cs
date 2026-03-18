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

    // ── 진입점 ─────────────────────────────────────────────────────
    public override void Activate()
    {
        StartCoroutine(ThunderRoutine());
    }

    // ── 메인 루틴 ──────────────────────────────────────────────────
    private IEnumerator ThunderRoutine()
    {
        // 씬의 글로벌 Light2D 찾기
        Light2D globalLight = FindGlobalLight();

        // 1) 페이드 아웃 (어둡게) + 사운드
        SoundManager.Shared?.Play(SoundKey.Structure_Thunder);
        yield return StartCoroutine(FadeLight(globalLight, 1f, dimAmount, fadeDuration));

        // 2) 모든 적에게 번개
        var enemies = Object.FindObjectsByType<EnemyController>(FindObjectsSortMode.None);
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.gameObject.activeInHierarchy) continue;

            SpawnLightning(enemy.transform.position);
            enemy.TakeDamage(mainValue);
            enemy.Stun(subValue);   // subValue = 마비 지속시간(초)

            yield return new WaitForSeconds(strikeDelay);
        }

        // 조명이 어두운 상태 잠깐 유지
        float remaining = dimDuration - fadeDuration - (enemies.Length * strikeDelay);
        if (remaining > 0f)
            yield return new WaitForSeconds(remaining);

        // 3) 페이드 인 (복구)
        yield return StartCoroutine(FadeLight(globalLight, dimAmount, 1f, fadeDuration));
    }

    // ── 번개 비주얼 스폰 ────────────────────────────────────────────
    private void SpawnLightning(Vector3 worldPos)
    {
        if (lightningPrefab == null) return;
        Instantiate(lightningPrefab, worldPos, Quaternion.identity);
    }

    // ── 조명 페이드 ─────────────────────────────────────────────────
    private IEnumerator FadeLight(Light2D light, float from, float to, float duration)
    {
        if (light == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            light.intensity = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        light.intensity = to;
    }

    // ── 글로벌 Light2D 탐색 ─────────────────────────────────────────
    private static Light2D FindGlobalLight()
    {
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
        {
            if (l.lightType == Light2D.LightType.Global)
                return l;
        }
        return null;
    }
}
