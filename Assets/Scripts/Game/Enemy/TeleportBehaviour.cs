using System.Collections;
using UnityEngine;

/// <summary>
/// 순간이동형 적 행동.
///
/// 상태 흐름:
///   Chase → TeleportSequence (EnemyCombatRoutines) → Chase
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
    [SerializeField] private GameObject spawnMarkerPrefab;
    [SerializeField] private float      markerDuration = 1.2f;

    [Header("안전 타임아웃 (애니메이션 이벤트 누락 대비)")]
    [SerializeField] private float animEventTimeout = 3f;

    // ── 상태 ─────────────────────────────────────────────────
    private float  _chaseTimer;
    private bool   _inSequence;
    private bool   _digAnimEnded;
    private bool   _spawnAnimEnded;

    private SpriteRenderer[] _renderers;

    // ── 초기화 ───────────────────────────────────────────────
    public override void Init(IEnemyHost host)
    {
        base.Init(host);
        _chaseTimer = chaseInterval;
        _renderers  = GetComponentsInChildren<SpriteRenderer>(true);
    }

    // ── 업데이트 루프 ─────────────────────────────────────────
    public override void OnAttackUpdate()
    {
        if (_inSequence) return;

        _chaseTimer -= Time.deltaTime;
        if (_chaseTimer <= 0f)
            StartCoroutine(TeleportSequence());
    }

    public override void OnMoveFixedUpdate()
    {
        if (_inSequence) return;
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
        _inSequence     = true;
        _digAnimEnded   = false;
        _spawnAnimEnded = false;

        var cfg = new EnemyCombatRoutines.TeleportConfig
        {
            PreDigDelay       = preDigDelay,
            FadeOutDuration   = fadeOutDuration,
            FadeInDuration    = fadeInDuration,
            SpawnRadiusMin    = spawnRadiusMin,
            SpawnRadiusMax    = spawnRadiusMax,
            SpawnMarkerPrefab = spawnMarkerPrefab,
            MarkerDuration    = markerDuration,
            AnimEventTimeout  = animEventTimeout,
        };

        yield return StartCoroutine(EnemyCombatRoutines.TeleportSequence(
            Owner, _renderers, cfg,
            () => _digAnimEnded, () => _spawnAnimEnded));

        _chaseTimer = chaseInterval;
        _inSequence = false;
    }

    public override void ApplyWaveScaling(float multiplier)
    {
        chaseInterval = Mathf.Max(2f, chaseInterval / Mathf.Sqrt(multiplier));
    }
}
