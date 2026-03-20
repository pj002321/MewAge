using UnityEngine;

/// <summary>
/// 일반 웨이브 / 보스 웨이브에 따라 환경(배경, 이동 범위, 카메라)을 교체합니다.
/// NormalEnvironment / BossEnvironment 하위에 각각
/// Background(FitSpriteToScreen) + MovementBounds 를 배치하세요.
/// </summary>
public class WaveEnvironmentSwitcher : MonoBehaviourSingleton<WaveEnvironmentSwitcher>
{
    [System.Serializable]
    public struct CameraConfig
    {
        [Tooltip("플레이어를 따라갈지 여부. false면 카메라 고정.")]
        public bool followPlayer;
        [Tooltip("카메라 이동 경계 제한 사용 여부.")]
        public bool useBounds;
        [Tooltip("경계 최솟값 (useBounds = true 일 때만 사용)")]
        public Vector2 boundsMin;
        [Tooltip("경계 최댓값 (useBounds = true 일 때만 사용)")]
        public Vector2 boundsMax;
    }

    [Header("일반 웨이브")]
    [SerializeField] private GameObject   normalEnvironment;
    [SerializeField] private CameraConfig normalCamera = new()
    {
        followPlayer = true,
        useBounds    = true,
        boundsMin    = new Vector2(-10f, -18f),
        boundsMax    = new Vector2( 10f,  18f),
    };

    [Header("보스 웨이브")]
    [SerializeField] private GameObject   bossEnvironment;
    [SerializeField] private CameraConfig bossCamera = new()
    {
        followPlayer = false,
        useBounds    = false,
    };

    protected override bool DontDestroy => false;

    private void Start() => SetNormal();

    public void SetNormal()
    {
        if (bossEnvironment)   bossEnvironment.SetActive(false);
        if (normalEnvironment) normalEnvironment.SetActive(true);
        normalEnvironment?.GetComponentInChildren<MovementBounds>(true)?.Register();
        normalEnvironment?.GetComponentInChildren<SpawnZone>(true)?.Register();
        ApplyCamera(normalCamera);
    }

    public void SetBoss()
    {
        if (normalEnvironment) normalEnvironment.SetActive(false);
        if (bossEnvironment)   bossEnvironment.SetActive(true);
        bossEnvironment?.GetComponentInChildren<MovementBounds>(true)?.Register();
        bossEnvironment?.GetComponentInChildren<SpawnZone>(true)?.Register();
        ApplyCamera(bossCamera);
    }

    private static void ApplyCamera(CameraConfig cfg)
    {
        CameraFollow.Shared?.ApplyConfig(cfg.followPlayer, cfg.useBounds, cfg.boundsMin, cfg.boundsMax);
    }
}
