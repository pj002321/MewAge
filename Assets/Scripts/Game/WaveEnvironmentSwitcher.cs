using UnityEngine;

/// <summary>
/// 일반 웨이브 / 보스 웨이브에 따라 환경(배경, 이동 범위, 카메라)을 교체합니다.
/// NormalEnvironment / BossEnvironment 하위에 각각
/// Background(FitSpriteToScreen) + MovementBounds 를 배치하세요.
///
/// 웨이브별 배경 교체:
///   waveBackgrounds 배열에 fromWave + backgroundObject 를 등록하세요.
///   배경 오브젝트들은 NormalEnvironment 하위에 넣어두면 됩니다.
///   SetWave(wave) 호출 시 fromWave가 현재 웨이브 이하인 것 중 가장 큰 항목이 활성화됩니다.
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

    [System.Serializable]
    public struct WaveBackgroundEntry
    {
        [Tooltip("이 웨이브 이상부터 적용 (1 = 처음부터)")]
        public int        fromWave;
        [Tooltip("활성화할 배경 오브젝트 (NormalEnvironment 하위에 배치)")]
        public GameObject background;
        [Tooltip("보스 인트로 시 플레이어가 걸어갈 출구 위치 (없으면 이동 생략)")]
        public Transform  bossExitPoint;
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

    [Header("웨이브별 배경 (fromWave 오름차순 권장)")]
    [SerializeField] private WaveBackgroundEntry[] waveBackgrounds = new WaveBackgroundEntry[0];

    [Header("보스 웨이브")]
    [SerializeField] private GameObject   bossEnvironment;
    [SerializeField] private Transform    bossEnterPoint;   // 보스 맵에서 플레이어 입장 위치
    [SerializeField] private CameraConfig bossCamera = new()
    {
        followPlayer = false,
        useBounds    = false,
    };

    /// <summary>현재 활성 배경의 보스 출구 위치 (null이면 이동 생략).</summary>
    public Transform CurrentBossExitPoint  { get; private set; }
    /// <summary>보스 맵 플레이어 입장 위치.</summary>
    public Transform CurrentBossEnterPoint => bossEnterPoint;

    protected override bool DontDestroy => false;

    private void Start() => SetWave(1);

    /// <summary>웨이브 시작 시 호출 — 해당 웨이브에 맞는 배경으로 교체합니다.</summary>
    public void SetWave(int wave)
    {
        if (bossEnvironment)   bossEnvironment.SetActive(false);
        if (normalEnvironment) normalEnvironment.SetActive(true);
        ApplyCamera(normalCamera);
        ApplyWaveBackground(wave); // MovementBounds/SpawnZone 등록은 내부에서 처리
    }

    /// <summary>보스 웨이브가 끝난 뒤 일반 환경으로 복귀 시 호출합니다.</summary>
    public void SetNormal()
    {
        int wave = GameManager.Shared != null ? GameManager.Shared.CurrentWave : 1;
        SetWave(wave);
    }

    public void SetBoss()
    {
        if (normalEnvironment) normalEnvironment.SetActive(false);
        if (bossEnvironment)   bossEnvironment.SetActive(true);
        bossEnvironment?.GetComponentInChildren<MovementBounds>(true)?.Register();
        bossEnvironment?.GetComponentInChildren<SpawnZone>(true)?.Register();
        ApplyCamera(bossCamera);
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void ApplyWaveBackground(int wave)
    {
        if (waveBackgrounds == null || waveBackgrounds.Length == 0) return;

        // fromWave <= wave 중 가장 큰 항목 선택
        WaveBackgroundEntry? matched = null;
        foreach (var entry in waveBackgrounds)
        {
            if (entry.fromWave <= wave)
            {
                if (matched == null || entry.fromWave > matched.Value.fromWave)
                    matched = entry;
            }
        }

        // 모든 배경 비활성화 → 선택된 것만 활성화
        foreach (var entry in waveBackgrounds)
            entry.background?.SetActive(false);

        if (!matched.HasValue) { CurrentBossExitPoint = null; return; }

        CurrentBossExitPoint = matched.Value.bossExitPoint;

        var bg = matched.Value.background;
        bg?.SetActive(true);

        // 활성화된 배경 하위의 MovementBounds / SpawnZone 등록
        // (배경마다 이동범위가 다를 경우 각 배경 하위에 컴포넌트를 배치하세요)
        if (bg != null)
        {
            bg.GetComponentInChildren<MovementBounds>(true)?.Register();
            bg.GetComponentInChildren<SpawnZone>(true)?.Register();
        }
        else
        {
            // 배경에 없으면 normalEnvironment 직속 자식에서 fallback
            normalEnvironment?.GetComponentInChildren<MovementBounds>(false)?.Register();
            normalEnvironment?.GetComponentInChildren<SpawnZone>(false)?.Register();
        }
    }

    private static void ApplyCamera(CameraConfig cfg)
    {
        CameraFollow.Shared?.ApplyConfig(cfg.followPlayer, cfg.useBounds, cfg.boundsMin, cfg.boundsMax);
    }
}
