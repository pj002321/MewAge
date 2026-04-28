using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 순간이동.
///
/// 흐름:
///   정지 → PreDig 딜레이 → 숨기 애니(bDigging=true) → FadeOut
///   → 목적지 결정 + 마커 표시 → markerDuration 대기
///   → 순간이동 → 스폰 애니(bDigging=false) + FadeIn → 종료
///
/// 애니메이션 이벤트:
///   숨기 클립 마지막 프레임 : OnTeleportAnimEnd("dig")
///   스폰  클립 마지막 프레임 : OnTeleportAnimEnd("spawn")
///   → TeleportBehaviour 와 동일한 방식으로 Injector 사용 가능.
/// </summary>
public class BossTeleportSkill : BossSkillBase
{
    [Header("딜레이")]
    [Tooltip("숨기 애니 시작 전 정지 시간 (초)")]
    [SerializeField] private float preDigDelay = 0.3f;

    [Header("페이드")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration  = 0.35f;

    [Header("스폰 위치")]
    [Tooltip("플레이어 기준 최소 이동 거리")]
    [SerializeField] private float spawnRadiusMin = 2f;
    [Tooltip("플레이어 기준 최대 이동 거리")]
    [SerializeField] private float spawnRadiusMax = 4f;

    [Header("스폰 마커")]
    [Tooltip("TeleportMarker 컴포넌트가 달린 프리팹. null이면 마커 없이 바로 이동합니다.")]
    [SerializeField] private GameObject spawnMarkerPrefab;
    [Tooltip("마커 표시 후 실제 이동까지 대기 시간 (초)")]
    [SerializeField] private float markerDuration = 1.2f;

    [Header("안전 타임아웃 (애니메이션 이벤트 누락 대비)")]
    [SerializeField] private float animEventTimeout = 3f;

    // ── 상태 ─────────────────────────────────────────────────
    private bool _digAnimEnded;
    private bool _spawnAnimEnded;

    private SpriteRenderer[] _renderers;

    // ── 초기화 ───────────────────────────────────────────────
    public override void Init(BossController boss)
    {
        base.Init(boss);
        _renderers = GetComponentsInChildren<SpriteRenderer>(true);
    }

    // ── 스킬 실행 ─────────────────────────────────────────────
    protected override IEnumerator OnExecute()
    {
        if (Owner.PlayerTransform == null) yield break;

        Boss.IsFiringSkill = true;
        Owner.EnemyAnimator?.SetBool("bMove", false);

        try
        {
            // ① PreDig: 잠깐 정지
            if (preDigDelay > 0f)
                yield return new WaitForSeconds(preDigDelay);

            // ② Digging: 숨기 애니메이션
            Owner.EnemyAnimator?.SetBool("bDigging", true);
            _digAnimEnded = false;
            yield return WaitForAnimEnd(() => _digAnimEnded, animEventTimeout);

            // ③ FadeOut
            yield return StartCoroutine(FadeRoutine(1f, 0f, fadeOutDuration));

            // ④ 목적지 결정 → 마커 표시
            Vector2 spawnPos = PickSpawnPosition();
            ShowMarker(spawnPos);

            yield return new WaitForSeconds(markerDuration);

            // ⑤ 순간이동 후 스폰 애니메이션
            Owner.transform.position = spawnPos;
            if (Owner.Rb != null) Owner.Rb.position = spawnPos;

            Owner.EnemyAnimator?.SetBool("bDigging", false);
            _spawnAnimEnded = false;
            StartCoroutine(FadeRoutine(0f, 1f, fadeInDuration));
            yield return WaitForAnimEnd(() => _spawnAnimEnded, animEventTimeout);

            // ⑥ 완전 불투명 보정
            SetAlpha(1f);
        }
        finally
        {
            Owner.EnemyAnimator?.SetBool("bDigging", false);
            SetAlpha(1f);
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    // ── 애니메이션 이벤트 수신 ────────────────────────────────
    public void OnTeleportAnimEnd(string clipName)
    {
        if (clipName == "dig")   _digAnimEnded   = true;
        if (clipName == "spawn") _spawnAnimEnded = true;
    }

    // ── 마커 ─────────────────────────────────────────────────
    private void ShowMarker(Vector2 pos)
    {
        if (spawnMarkerPrefab == null) return;
        var go     = Instantiate(spawnMarkerPrefab, pos, Quaternion.identity);
        var marker = go.GetComponent<TeleportMarker>();
        marker?.Show(markerDuration);
    }

    // ── 스폰 위치 선택 ────────────────────────────────────────
    private Vector2 PickSpawnPosition()
    {
        if (Owner.PlayerTransform == null)
            return (Vector2)Owner.transform.position;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist  = Random.Range(spawnRadiusMin, spawnRadiusMax);
        return (Vector2)Owner.PlayerTransform.position
             + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
    }

    // ── 페이드 헬퍼 ──────────────────────────────────────────
    private IEnumerator FadeRoutine(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            SetAlpha(Mathf.Lerp(from, to, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }
        SetAlpha(to);
    }

    private void SetAlpha(float alpha)
    {
        if (_renderers == null) return;
        foreach (var sr in _renderers)
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, alpha);
    }

    private static IEnumerator WaitForAnimEnd(System.Func<bool> conditionMet, float timeout)
    {
        float elapsed = 0f;
        while (!conditionMet() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }
}
