using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 스킬: 순간이동.
///
/// 흐름: 정지 → PreDig 딜레이 → 숨기 애니 + FadeOut
///        → 목적지 결정 + 마커 → 순간이동 → 스폰 애니 + FadeIn → 종료
///
/// 애니메이션 이벤트는 BossController.OnTeleportAnimEnd() 로 수신됩니다.
/// </summary>
[System.Serializable]
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

    private bool             _digAnimEnded;
    private bool             _spawnAnimEnded;
    private SpriteRenderer[] _renderers;

    public override void Init(BossController boss)
    {
        base.Init(boss);
        _renderers = boss.GetComponentsInChildren<SpriteRenderer>(true);
    }

    protected override async UniTask OnExecuteAsync(CancellationToken ct)
    {
        if (PlayerT == null) return;

        _digAnimEnded   = false;
        _spawnAnimEnded = false;
        Boss.EnemyAnimator?.SetBool(EnemyAnim.BMove, false);

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
            await EnemyCombatRoutines.TeleportSequence(
                Boss, _renderers, cfg,
                () => _digAnimEnded, () => _spawnAnimEnded)
                .ToUniTask(cancellationToken: ct);
        }
        finally
        {
            Boss?.EnemyAnimator?.SetBool(EnemyAnim.BDigging, false);
            if (_renderers != null) EnemyFX.SetAlpha(_renderers, 1f);
        }
    }

    // BossController.OnTeleportAnimEnd() 에서 전달받습니다.
    internal void NotifyTeleportAnimEnd(string clipName)
    {
        if (clipName == "dig")   _digAnimEnded   = true;
        if (clipName == "spawn") _spawnAnimEnded = true;
    }

    public override void OnInterrupted()
    {
        // 애니메이션 이벤트 대기를 즉시 해제해 코루틴이 완료될 수 있게 합니다.
        _digAnimEnded   = true;
        _spawnAnimEnded = true;
        if (_renderers != null) EnemyFX.SetAlpha(_renderers, 1f);
        Boss?.EnemyAnimator?.SetBool(EnemyAnim.BDigging, false);
    }
}
