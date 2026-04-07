using UnityEngine;

/// <summary>
/// 챕터별/스테이지별 환경(배경, 이동 범위, 카메라)을 관리합니다.
/// StageEnvironmentSO에서 배경 프리팹을 읽어 normalEnvParent / bossEnvParent 아래 런타임 인스턴스화합니다.
/// 배경 프리팹 내부에 EnvironmentBackground · MovementBounds · SpawnZone 컴포넌트를 배치하세요.
/// </summary>
public class WaveEnvironmentSwitcher : MonoBehaviourSingleton<WaveEnvironmentSwitcher>
{
    [Tooltip("일반 배경 프리팹이 인스턴스화될 부모 (씬의 Root 오브젝트)")]
    [SerializeField] private Transform normalEnvParent;

    [Tooltip("보스 배경 프리팹이 인스턴스화될 부모 (씬의 BossEnvRoot 오브젝트)")]
    [SerializeField] private Transform bossEnvParent;

    private StageEnvironmentSO _currentStage;
    private GameObject         _activeBgInstance;      // 현재 인스턴스화된 일반 배경
    private GameObject         _activeBossInstance;    // 현재 인스턴스화된 보스 배경
    private BossEntryGate      _currentGate;           // 현재 활성 ExitPoint 게이트

    /// <summary>현재 활성 일반 배경의 보스 출구 위치 (null이면 이동 생략).</summary>
    public Transform CurrentBossExitPoint  { get; private set; }
    /// <summary>현재 활성 보스 배경의 플레이어 입장 위치.</summary>
    public Transform CurrentBossEnterPoint { get; private set; }
    /// <summary>현재 활성 보스 배경의 보스 스폰 위치. null이면 EnemySpawner 기본값 사용.</summary>
    public Transform CurrentBossSpawnPoint { get; private set; }

    protected override bool DontDestroy => false;

    /// <summary>스테이지를 설정합니다. 게임 시작 시 또는 스테이지 변경 시 호출하세요.</summary>
    public void SetStage(string stageId)
    {
        var stageSO = Resources.Load<StageEnvironmentSO>($"StageEnvironments/{stageId}");
        if (stageSO == null)
        {
            Debug.LogError($"[WaveEnvironmentSwitcher] SO를 찾을 수 없습니다: Resources/StageEnvironments/{stageId}.asset");
            return;
        }

        _currentStage = stageSO;
        SetWave(1);
    }

    /// <summary>웨이브 시작 시 호출 — 해당 웨이브에 맞는 배경 프리팹으로 교체합니다.</summary>
    public void SetWave(int wave)
    {
        if (_currentStage == null) return;

        DestroyBossInstance();
        SpawnWaveBackground(_currentStage, wave);
        ApplyCameraFromBounds(_currentStage.followPlayer, _currentStage.useBounds);
    }

    /// <summary>보스 웨이브가 끝난 뒤 일반 환경으로 복귀 시 호출합니다.</summary>
    public void SetNormal()
    {
        int wave = GameManager.Shared != null ? GameManager.Shared.CurrentWave : 1;
        SetWave(wave);
    }

    /// <summary>보스 웨이브 시작 시 호출합니다.</summary>
    public void SetBoss()
    {
        if (_currentStage == null) return;

        // 일반 배경 숨기기
        if (_activeBgInstance != null)
            _activeBgInstance.SetActive(false);

        // 보스 배경 스폰
        DestroyBossInstance();
        if (_currentStage.bossBackground != null)
        {
            var parent = bossEnvParent != null ? bossEnvParent : transform;
            _activeBossInstance = Instantiate(_currentStage.bossBackground, parent);

            var envComp = _activeBossInstance.GetComponentInChildren<EnvironmentBackground>(true);
            CurrentBossEnterPoint = envComp?.bossEnterPoint;
            CurrentBossSpawnPoint = envComp?.bossSpawnPoint;

            _activeBossInstance.GetComponentInChildren<MovementBounds>(true)?.Register();
            _activeBossInstance.GetComponentInChildren<SpawnZone>(true)?.Register();
        }

        ApplyCameraFromBounds(_currentStage.bossFollowPlayer, _currentStage.useBounds);
    }

    // ── 게이트 API ────────────────────────────────────────────────

    /// <summary>
    /// 현재 배경의 ExitPoint 게이트를 활성화합니다.
    /// 게이트가 없으면 onEnter를 즉시 호출합니다.
    /// </summary>
    public void ActivateExitGate(System.Action onEnter)
    {
        DeactivateExitGate();

        var envComp = _activeBgInstance != null
            ? _activeBgInstance.GetComponentInChildren<EnvironmentBackground>(true)
            : null;
        _currentGate = envComp?.bossEntryGate;

        if (_currentGate != null)
        {
            _currentGate.Activate(onEnter);
        }
        else
        {
            Debug.LogWarning("[WaveEnvironmentSwitcher] bossEntryGate 없음 — 즉시 진행합니다.");
            onEnter?.Invoke();
        }
    }

    /// <summary>현재 활성 게이트를 비활성화합니다.</summary>
    public void DeactivateExitGate()
    {
        if (_currentGate != null)
        {
            _currentGate.Deactivate();
            _currentGate = null;
        }
    }

    // ── 내부 ──────────────────────────────────────────────────────

    private void SpawnWaveBackground(StageEnvironmentSO stage, int wave)
    {
        // 이전 배경 교체 전 게이트 해제
        DeactivateExitGate();

        // 이전 일반 배경 제거
        if (_activeBgInstance != null)
        {
            Destroy(_activeBgInstance);
            _activeBgInstance = null;
        }
        CurrentBossExitPoint = null;

        if (stage.waveBackgrounds == null || stage.waveBackgrounds.Length == 0) return;

        // fromWave <= wave 중 가장 큰 항목 선택
        StageEnvironmentSO.WaveBackgroundEntry? matched = null;
        foreach (var entry in stage.waveBackgrounds)
        {
            if (entry.fromWave <= wave)
            {
                if (matched == null || entry.fromWave > matched.Value.fromWave)
                    matched = entry;
            }
        }

        if (!matched.HasValue || matched.Value.background == null) return;

        var parent = normalEnvParent != null ? normalEnvParent : transform;
        _activeBgInstance = Instantiate(matched.Value.background, parent);

        var envComp = _activeBgInstance.GetComponentInChildren<EnvironmentBackground>(true);
        CurrentBossExitPoint = envComp?.bossExitPoint;

        _activeBgInstance.GetComponentInChildren<MovementBounds>(true)?.Register();
        _activeBgInstance.GetComponentInChildren<SpawnZone>(true)?.Register();
    }

    private void DestroyBossInstance()
    {
        if (_activeBossInstance != null)
        {
            Destroy(_activeBossInstance);
            _activeBossInstance = null;
        }
        CurrentBossEnterPoint = null;
        CurrentBossSpawnPoint = null;
    }

    private static void ApplyCameraFromBounds(bool soFollowPlayer, bool soUseBounds)
    {
        if (CameraFollow.Shared == null) return;

        var mb = MovementBounds.Shared;
        if (mb == null)
        {
            // Fallback: no MovementBounds — use SO's legacy settings
            CameraFollow.Shared?.ApplyConfig(soFollowPlayer, soUseBounds, Vector2.zero, Vector2.zero);
            return;
        }

        mb.GetCameraBoundsMinMax(out var min, out var max);

        if (mb.ViewMode == MovementBounds.CameraViewMode.FixedView)
            CameraFollow.Shared.ApplyFixedView(min, max);
        else
            CameraFollow.Shared.ApplyDynamicFollow(min, max);
    }
}
