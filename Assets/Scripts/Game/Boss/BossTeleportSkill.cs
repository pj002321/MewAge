using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 스킬: 순간이동 (EnemyCombatRoutines.TeleportSequence 공유).
///
/// 흐름:
///   정지 → PreDig 딜레이 → 숨기 애니 → FadeOut
///   → 목적지 결정 + 마커 → 순간이동 → 스폰 애니 + FadeIn → 종료
///
/// 애니메이션 이벤트:
///   숨기 클립 마지막 프레임 : OnTeleportAnimEnd("dig")
///   스폰  클립 마지막 프레임 : OnTeleportAnimEnd("spawn")
/// </summary>
public class BossTeleportSkill : BossSkillBase
{
    [Header("딜레이")]
    [SerializeField] private float preDigDelay = 0.3f;

    [Header("페이드")]
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float fadeInDuration  = 0.35f;

    [Header("스폰 위치")]
    [SerializeField] private float spawnRadiusMin = 2f;
    [SerializeField] private float spawnRadiusMax = 4f;

    [Header("스폰 마커")]
    [SerializeField] private GameObject spawnMarkerPrefab;
    [SerializeField] private float      markerDuration = 1.2f;

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
        Owner.EnemyAnimator?.SetBool(EnemyAnim.BMove, false);

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

        try
        {
            yield return StartCoroutine(EnemyCombatRoutines.TeleportSequence(
                Owner, _renderers, cfg,
                () => _digAnimEnded, () => _spawnAnimEnded));
        }
        finally
        {
            Owner.EnemyAnimator?.SetBool(EnemyAnim.BDigging, false);
            EnemyFX.SetAlpha(_renderers, 1f);
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    // ── 애니메이션 이벤트 수신 ────────────────────────────────
    public void OnTeleportAnimEnd(string clipName)
    {
        if (clipName == "dig")   _digAnimEnded   = true;
        if (clipName == "spawn") _spawnAnimEnded = true;
    }
}
