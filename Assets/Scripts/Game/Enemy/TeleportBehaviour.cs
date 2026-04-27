using System.Collections;
using UnityEngine;

/// <summary>
/// 순간이동형 적 행동.
///
/// 상태 흐름:
///   Chase → PreDig(정지) → Digging(bDigging=true) → [DigAnimEnd]
///   → FadeOut(투명) → 목적지 결정 + 마커 표시(spawnMarkerPrefab)
///   → markerDuration 대기 → Teleport → Spawning(bDigging=false, 페이드인)
///   → [SpawnAnimEnd] → Chase
///
/// 애니메이션 이벤트 설정:
///   숨기 클립 마지막 프레임 : OnAnimationEnd("dig")
///   스폰  클립 마지막 프레임 : OnAnimationEnd("spawn")
///   → Tools/MuAge/Teleport Anim Event Injector 로 자동 주입 가능.
/// </summary>
public class TeleportBehaviour : EnemyBehaviourBase
{
    // ── Inspector ────────────────────────────────────────────
    [Header("순간이동 주기")]
    [SerializeField] private float chaseInterval = 5f;
    [SerializeField] private float preDigDelay   = 0.3f;

    [Header("페이드")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration  = 0.35f;

    [Header("스폰 위치")]
    [SerializeField] private float spawnRadiusMin = 1.5f;
    [SerializeField] private float spawnRadiusMax = 3.5f;

    [Header("스폰 마커")]
    [SerializeField] private GameObject spawnMarkerPrefab;  // TeleportMarker 컴포넌트가 달린 프리팹
    [SerializeField] private float      markerDuration = 1.2f;  // 마커 표시 후 스폰까지 대기 시간

    [Header("안전 타임아웃 (애니메이션 이벤트 누락 대비)")]
    [SerializeField] private float animEventTimeout = 3f;

    // ── 상태 ─────────────────────────────────────────────────
    private enum State { Chase, PreDig, Digging, FadeOut, MarkerWait, Spawning }
    private State _state = State.Chase;

    private float  _chaseTimer;
    private bool   _digAnimEnded;
    private bool   _spawnAnimEnded;
    private bool   _inSequence;

    private SpriteRenderer[] _renderers;

    // ── 초기화 ───────────────────────────────────────────────
    public override void Init(EnemyController controller)
    {
        base.Init(controller);
        _chaseTimer = chaseInterval;
        _renderers  = GetComponentsInChildren<SpriteRenderer>(true);
    }

    // ── 업데이트 루프 ─────────────────────────────────────────
    public override void OnAttackUpdate()
    {
        if (_state != State.Chase || _inSequence) return;

        _chaseTimer -= Time.deltaTime;
        if (_chaseTimer <= 0f)
            StartCoroutine(TeleportSequence());
    }

    public override void OnMoveFixedUpdate()
    {
        if (_state != State.Chase) return;
        Owner.MoveTowardPlayer(Owner.MoveSpeed);
    }

    // ── 애니메이션 이벤트 수신 ────────────────────────────────
    public void OnAnimationEnd(string clipName)
    {
        if (clipName == "dig")   _digAnimEnded   = true;
        if (clipName == "spawn") _spawnAnimEnded = true;
    }

    // ── 순간이동 시퀀스 ───────────────────────────────────────
    private IEnumerator TeleportSequence()
    {
        _inSequence = true;

        // ① PreDig: 이동 정지
        _state = State.PreDig;
        yield return new WaitForSeconds(preDigDelay);

        // ② Digging: 숨기 애니메이션
        _state = State.Digging;
        Owner.EnemyAnimator?.SetBool("bDigging", true);

        _digAnimEnded = false;
        yield return WaitForAnimEnd(() => _digAnimEnded, animEventTimeout);

        // ③ FadeOut: 투명하게 사라짐
        _state = State.FadeOut;
        yield return StartCoroutine(FadeRoutine(1f, 0f, fadeOutDuration));

        // ④ 목적지 결정 → 마커 표시
        _state = State.MarkerWait;
        Vector2 spawnPos = PickSpawnPosition();
        ShowMarker(spawnPos);

        yield return new WaitForSeconds(markerDuration);

        // ⑤ 순간이동 후 스폰 애니메이션
        transform.position = spawnPos;
        _state = State.Spawning;
        Owner.EnemyAnimator?.SetBool("bDigging", false);

        _spawnAnimEnded = false;
        StartCoroutine(FadeRoutine(0f, 1f, fadeInDuration));
        yield return WaitForAnimEnd(() => _spawnAnimEnded, animEventTimeout);

        // ⑥ 추격 재개
        SetAlpha(1f);
        _chaseTimer = chaseInterval;
        _state      = State.Chase;
        _inSequence = false;
    }

    // ── 마커 ─────────────────────────────────────────────────

    private void ShowMarker(Vector2 pos)
    {
        if (spawnMarkerPrefab == null) return;
        var go     = Instantiate(spawnMarkerPrefab, pos, Quaternion.identity);
        var marker = go.GetComponent<TeleportMarker>();
        marker?.Show(markerDuration);
    }

    // ── 공통 헬퍼 ────────────────────────────────────────────

    private static IEnumerator WaitForAnimEnd(System.Func<bool> conditionMet, float timeout)
    {
        float elapsed = 0f;
        while (!conditionMet() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private Vector2 PickSpawnPosition()
    {
        if (Owner.PlayerTransform == null)
            return transform.position;

        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist  = Random.Range(spawnRadiusMin, spawnRadiusMax);
        return (Vector2)Owner.PlayerTransform.position
             + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
    }

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

    public override void ApplyWaveScaling(float multiplier)
    {
        chaseInterval = Mathf.Max(2f, chaseInterval / Mathf.Sqrt(multiplier));
    }
}
