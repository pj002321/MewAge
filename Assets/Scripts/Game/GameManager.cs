using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI goldText;
    [SerializeField] private TextMeshProUGUI reroll_goldText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI reroll_levelText;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private Slider          expSlider;
    [SerializeField] private Slider          reroll_expSlider;
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private GameObject         gameOverPanel;       // 부활 패널
    [SerializeField] private GameOverResultPanel gameOverResultPanel; // 게임오버 결과 패널
    [SerializeField] private GameObject      waveInfoProgress;
    [SerializeField] private GameClearPanel  gameClearPanel;

    [Header("Wave 타이머")]
    // Normal wave durations: Wave 1~9=20s, 11~19=30s, 21~29=35s. Boss wave: 90s.
    [SerializeField] private float bossWaveTimeLimit = 90f;  // 보스 웨이브 제한 시간 (초)

    [Header("Wave Clear 노티")]
    [SerializeField] private GameObject waveClearPanel;
    [SerializeField] private float      waveClearDuration = 1f;

    [Header("타이머 색상")]
    [SerializeField] private Color timerNormalColor = Color.white;
    [SerializeField] private Color timerWarningColor = Color.red;
    [SerializeField] private float timerWarningThreshold = 5f;

    private int   wave = 0;
    private float waveTimer;
    private bool  isGameOver;
    private bool  waitingForUpgrade = true; // 트랜지션/초기화 기간 중 0초 달성으로 증강이 뜨는 것 방지
    private bool  bossWaveActive;
    private string currentChapterID = "C1_S1";

    // Session stats (accumulated during a single run for the Clear Panel)
    private int sessionExpGained  = 0;
    private int sessionGoldGained = 0;

    // 앱 전환 / 알림바 등 시스템 인터럽트 대응
    private bool  _systemPaused;
    private float _savedTimeScale;

    protected override bool DontDestroy => false;

    private void Start()
    {
        StartCoroutine(InitAndStartRoutine());
    }

    private IEnumerator InitAndStartRoutine()
    {
        // 1. 유니티 엔진과 모바일 기기(Safe Area, 해상도 등)가 씬 로딩 직후 초기화될 시간을 1프레임 대기합니다.
        yield return null;

        // 2. 가장 민감한 화면 비율(Aspect)과 관련된 크기 계산을 명시적으로 실행합니다.
        CameraFollow.Shared?.Init();

        var fitSprites = FindObjectsOfType<FitSpriteToScreen>();
        foreach (var fs in fitSprites)
        {
            fs.Fit();
        }

        // 3. 게임 시스템 및 UI 초기화
        if (gameOverPanel)               gameOverPanel.SetActive(false);
        if (waveClearPanel)              waveClearPanel.SetActive(false);
        if (gameClearPanel != null)      gameClearPanel.gameObject.SetActive(false);

        // Activate VirtualJoystick for in-game control
        if (VirtualJoystick.Instance != null)
            VirtualJoystick.Instance.gameObject.SetActive(true);

        currentChapterID = PlayerPrefs.GetString("SelectedChapterID", "C1_S1");
        Debug.Log($"[GameManager] 입장 — ChapterID: {currentChapterID}");
        UpgradeManager.Shared?.SetCurrentChapterID(currentChapterID);
        WaveEnvironmentSwitcher.Shared?.SetStage(currentChapterID);
        EnemySpawner.Shared?.SetChapterStage(currentChapterID);

        // 이전 판 증강 수치 초기화 후 캐릭터 기초 스탯 + 기물 시너지 일괄 반영
        // BeginBatch ~ EndBatch 사이 SetBase/AddPctMod는 마지막 EndBatch에서 1회만 적용됨
        var sm = PlayerStatManager.Shared;
        sm?.ResetMods();
        sm?.BeginBatch();
        PlayerCharacterManager.Shared?.ApplyCharacterStats();
        WareSynergySystem.ApplySynergies();
        sm?.EndBatch();

        // 시작 시 Normal 무기 1개 지급 준비
        UpgradeManager.Shared?.GiveStarterWeapon();

        // GameManager.Shared가 준비된 시점에 HP UI 강제 동기화
        if (PlayerController.Instance != null)
            UpdateHP(PlayerController.Instance.CurrentHP, PlayerController.Instance.MaxHPValue);

        // 4. 모든 인게임 초기화가 끝났으므로 로딩 씬에게 내려가도 좋다고 신호를 보냅니다.
        SceneLoadManager.NotifyStageReady();

        // 5. 로딩 씬의 트랜지션 연출이 완전히 끝날 때까지 대기합니다. (게임 일시정지 효과)
        while (SceneLoadManager.IsTransitioning)
            yield return null;

        // 6. 모든 준비 패널이 걷히고 나서 비로소 첫 웨이브를 시작합니다.
        StartNextWave();
    }

    private void Update()
    {
        if (isGameOver) return;

        waveTimer -= Time.deltaTime;
        UpdateTimerUI();

        if (waveTimer <= 0f && !waitingForUpgrade)
        {
            if (bossWaveActive)
            {
                // Boss wave timeout → Game Over
                this.DLog("[GameManager] Boss wave time limit reached! Game Over.");
                TriggerGameOver();
            }
            else
            {
                waitingForUpgrade = true;
                StartCoroutine(WaveClearRoutine());
            }
        }
    }

    private void WaveInfoProgress()
    {
        if (waveInfoProgress == null) return;
        var anim = waveInfoProgress.GetComponent<Animator>();
        if (anim == null) return;

        var   stateInfo  = anim.GetCurrentAnimatorStateInfo(0);
        float clipLength = stateInfo.length;
        if (clipLength <= 0f) return;

        // 10웨이브 주기 내 위치 (0~9)
        int cycleWave = (wave - 1) % 10;
        // 2웨이브마다 1초씩 증가: 0~1→0s, 2~3→1s, 4~5→2s, 6~7→3s, 8~9→4s
        float seconds = Mathf.Floor(cycleWave / 2f);

        // 새 구간 진입 웨이브 (wave 3, 5, 7, 9): 이전 위치에서 1초 재생 후 멈춤
        if (cycleWave > 0 && cycleWave % 2 == 0)
        {
            float startSec = seconds - 1f;
            anim.Play(stateInfo.fullPathHash, 0, startSec / clipLength);
            anim.speed = 1f;
            StartCoroutine(PauseAnimAfterSeconds(anim, 1f));
        }
        else
        {
            // 나머지 웨이브: 해당 위치에 고정
            anim.Play(stateInfo.fullPathHash, 0, seconds / clipLength);
            anim.speed = 0f;
        }
    }

    private IEnumerator PauseAnimAfterSeconds(Animator anim, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (anim != null) anim.speed = 0f;
    }
    private IEnumerator WaveClearRoutine()
    {
        if (waveClearPanel) waveClearPanel.SetActive(true);
        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();
        FXManager.Shared?.ClearAllFX();   // 투사체·파편 즉시 제거

        yield return new WaitForSeconds(waveClearDuration);

        if (waveClearPanel) waveClearPanel.SetActive(false);

        // 최종 웨이브 클리어 → 게임 클리어 패널
        if (wave >= GetMaxWave())
        {
            if (gameClearPanel != null)
                gameClearPanel.Show(wave, sessionExpGained, sessionGoldGained);
            else
                GoHome();
            yield break;
        }

        if (ImprovManager.Shared != null)
            ImprovManager.Shared.ShowPanel(wave);
        else if (UpgradeManager.Shared != null)
            UpgradeManager.Shared.ShowPanel(wave);
        else
            StartNextWave();
    }

    /// <summary>웨이브 번호에 따른 제한 시간 반환. 보스 웨이브는 호출 전에 별도 처리.</summary>
    private float WaveDuration(int w)
    {
        // Boss wave: handled separately in StartNextWave
        if      (w >= 21) return 35f;  // Wave 21~29+
        else if (w >= 11) return 30f;  // Wave 11~19
        else              return 20f;  // Wave 1~9
    }

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        int secs = Mathf.CeilToInt(Mathf.Max(0f, waveTimer));
        timerText.text  = $"{secs / 60:00}:{secs % 60:00}";
        timerText.color = waveTimer <= timerWarningThreshold ? timerWarningColor : timerNormalColor;
    }

    private void UpdateWaveUI()
    {
        if (waveText) waveText.text = $"WAVE {wave}";
        if (timerText) timerText.color = timerNormalColor;
        WaveInfoProgress();
    }

    public void UpdateHP(float current, float max)
    {
        if (hpSlider)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = Mathf.Max(1f, max);
            hpSlider.value    = current;
        }
        if (hpText) hpText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    public void UpdateGoldUI(int gold)
    {
        if (goldText) goldText.text = $"{gold}";
    }

    public void UpdateExpUI(int exp, int expMax, int level)
    {
        if (levelText)        levelText.text         = $"LV.{level}";
        if (reroll_levelText) reroll_levelText.text  = $"LV.{level}";
        if (expText)          expText.text          = $"{exp} / {expMax}";
        if (expSlider)        expSlider.value        = (float)exp / expMax;
        if (reroll_expSlider) reroll_expSlider.value = (float)exp / expMax;
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        EnemySpawner.Shared?.StopSpawning();

        // 최고 웨이브 저장
        string prefKey = $"BestWave_{currentChapterID}";
        int currentBest = PlayerPrefs.GetInt(prefKey, 0);
        if (wave > currentBest)
        {
            PlayerPrefs.SetInt(prefKey, wave);
            PlayerPrefs.Save();
        }

        if (gameOverPanel) gameOverPanel.SetActive(true);
        // GameOverPanel.cs will count down 5 s, shrink itself, then call ShowGameClearAfterGameOver()
    }

    /// <summary>
    /// 부활 패널 카운트다운 종료 후 호출 — 게임오버 결과 패널 표시.
    /// </summary>
    public void ShowGameOverResult()
    {
        if (gameOverResultPanel != null)
            gameOverResultPanel.Show(wave);
        else
            GoHome();
    }

    public void Revive()
    {
        if (!isGameOver) return;
        isGameOver = false;
        if (gameOverPanel)       gameOverPanel.SetActive(false);
        if (gameOverResultPanel) gameOverResultPanel.gameObject.SetActive(false);
        PlayerController.Instance?.Revive();
        EnemySpawner.Shared?.ResumeSpawning(wave);
    }

    public void RestartGame()
    {
        SceneLoadManager.LoadScene("stage@scene");
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        SceneLoadManager.LoadScene("home@scene");
    }

    public void StartNextWave()
    {
        wave++;
        waitingForUpgrade = false;

        // 웨이브 시작 시 MaxHP의 10% 회복 (만땅이어도 텍스트 표시)
        var player = PlayerController.Instance;
        if (player != null)
        {
            float waveHeal = player.MaxHPValue * 0.1f;
            player.HealHP(waveHeal);
            FXManager.Shared?.SpawnPlayerText("+10%", Color.green);
        }

        // WaveHealHP 패시브 — 추가 축적된 회복량
        if (player != null && player.WaveHealAmount > 0f)
            player.HealHP(player.WaveHealAmount);

        bool isBossWave    = EnemySpawner.Shared?.GetNextBossWave(wave - 1) == wave;
        bool needsExitGate = isBossWave;

        // Boss wave gets its own time limit; normal waves use stepped duration
        waveTimer = isBossWave ? bossWaveTimeLimit : WaveDuration(wave);

        this.DLog($"[GameManager] StartNextWave wave={wave} | isBossWave={isBossWave} | needsGate={needsExitGate} | timer={waveTimer}s");

        UpdateWaveUI();

        // 보스 전/후: 업그레이드 패널 닫힘 → 게이트 활성화 → 플레이어 이동 → 실제 웨이브 시작
        if (needsExitGate)
        {
            waitingForUpgrade = true;  // 타이머 카운트다운 억제
            WaveEnvironmentSwitcher.Shared?.ActivateExitGate(() =>
            {
                waitingForUpgrade = false;
                DoStartWave(isBossWave);
            });
            return;
        }

        DoStartWave(isBossWave);
    }

    private void DoStartWave(bool isBossWave)
    {
        if (isBossWave && BossIntroSequence.Shared != null)
        {
            WaveEnvironmentSwitcher.Shared?.SetWave(wave); // 일반 맵 유지 (출구 연출용)
            BossIntroSequence.Shared.Play(() =>
            {
                EnemySpawner.Shared?.ResumeSpawning(wave);
            });
            return;
        }

        // 일반 웨이브 시작 시 플레이어를 맵 중앙으로 이동
        TeleportPlayerToMapCenter();

        EnemySpawner.Shared?.ResumeSpawning(wave);
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);
    }

    private void TeleportPlayerToMapCenter()
    {
        var player = PlayerController.Instance;
        if (player == null) return;

        if (MovementBounds.Shared != null)
        {
            MovementBounds.Shared.GetPlayerBoundsMinMax(out Vector2 min, out Vector2 max);
            player.transform.position = (Vector3)((min + max) * 0.5f);
        }
        else
        {
            player.transform.position = Vector3.zero;
        }
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;

    /// <summary>현재 챕터+스테이지의 DB_웨이브에서 최대 웨이브 번호를 반환합니다.</summary>
    private int GetMaxWave()
    {
        // currentChapterID 형식: "C1_S1"
        string prefix = currentChapterID + "_W";
        int max = 0;
        Mewage.DB.DB_웨이브.ForEachEntity(row =>
        {
            if (row.f_웨이브ID != null && row.f_웨이브ID.StartsWith(prefix))
            {
                string numStr = row.f_웨이브ID.Substring(prefix.Length);
                if (int.TryParse(numStr, out int n) && n > max)
                    max = n;
            }
        });
        return max > 0 ? max : 30; // DB에 없으면 30 fallback
    }

    // ── 모바일 앱 생명주기 ────────────────────────────────────────
    // OnApplicationFocus : 알림바 내리기, 알림바 롱프레스 미리보기 등 포커스 손실 처리
    // OnApplicationPause : 앱 전환(백그라운드) 처리
    // 두 이벤트 모두 _systemPaused 플래그로 중복 호출 방지
    private void OnApplicationFocus(bool hasFocus)  => HandleSystemPause(!hasFocus);
    private void OnApplicationPause(bool pauseStatus) => HandleSystemPause(pauseStatus);

    private void HandleSystemPause(bool pausing)
    {
        if (pausing)
        {
            if (_systemPaused) return;           // 이미 시스템 일시정지 상태
            _systemPaused   = true;
            _savedTimeScale = Time.timeScale;
            Time.timeScale  = 0f;
            AudioListener.pause = true;
        }
        else
        {
            if (!_systemPaused) return;          // 시스템 일시정지가 아니면 무시
            _systemPaused  = false;
            Time.timeScale = _savedTimeScale;    // 패널이 열려있었으면 0 복원, 게임 중이었으면 1 복원
            AudioListener.pause = false;
        }
    }

    /// <summary>현재 챕터의 ChapterRosterSO에서 fromWave보다 큰 가장 가까운 보스 웨이브 번호를 반환합니다. 없으면 -1.</summary>
    public int GetNextBossWave(int fromWave)
    {
        return EnemySpawner.Shared?.GetNextBossWave(fromWave) ?? -1;
    }

    // ── 보스 웨이브 ──────────────────────────────────────────────
    public void StartBossWave()
    {
        bossWaveActive = true;
        WaveEnvironmentSwitcher.Shared?.SetBoss();
    }

    [Header("보스 처치 후 Orb 수집 대기")]
    [SerializeField] private float orbCollectWait = 5f;   // 업그레이드 패널 전 대기 시간(초)

    public void OnBossDefeated()
    {
        bossWaveActive = false;
        WaveEnvironmentSwitcher.Shared?.SetNormal();
        EnemySpawner.Shared?.StopSpawning(); // 보스 죽음 즉시 스폰 중지
        if (waitingForUpgrade) return;
        waitingForUpgrade = true;

        int nextBoss = GetNextBossWave(wave);
        this.DLog($"[GameManager] OnBossDefeated wave={wave} nextBoss={nextBoss}");

        // Orb 수집 대기 카운트다운 후 분기
        StartCoroutine(BossOrbCollectRoutine(nextBoss));
    }

    /// <summary>
    /// 보스 처치 후 orbCollectWait 초 동안 타이머 UI로 카운트다운을 보여주고,
    /// 종료 후 중간 보스면 WaveClearRoutine, 마지막 보스면 ShowGameClear를 호출합니다.
    /// </summary>
    private IEnumerator BossOrbCollectRoutine(int nextBoss)
    {
        // 타이머를 수집 대기 시간으로 덮어써 Update가 카운트다운 UI를 그리게 함
        waveTimer = orbCollectWait;

        this.DLog($"[GameManager] Orb 수집 대기 {orbCollectWait}s 시작");
        yield return new WaitForSeconds(orbCollectWait);

        if (nextBoss > 0)
            StartCoroutine(WaveClearRoutine());   // 중간 보스 → 업그레이드 패널
        else
            ShowGameClear();                      // 마지막 보스 → 게임 클리어
    }

    private void ShowGameClear()
    {
        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        if (gameClearPanel != null)
        {
            gameClearPanel.Show(wave, sessionExpGained, sessionGoldGained);
        }
        else
        {
            this.DLog("[GameManager] GameClearPanel not assigned — showing GameOver as fallback.");
            if (gameOverPanel) gameOverPanel.SetActive(true);
        }
    }

    /// <summary>인게임 획득 골드/경험치를 누적합니다. ItemDropManager, EXP 지급 측에서 호출하세요.</summary>
    public void AddSessionGold(int amount) => sessionGoldGained += amount;
    public void AddSessionExp (int amount) => sessionExpGained  += amount;

    // ── 디버그 전용 ──────────────────────────────────────────────
    /// <summary>현재 웨이브를 즉시 종료합니다. 패널이 열려 있어도 강제 작동합니다.</summary>
    public void DebugSkipWave()
    {
        Debug.Log($"[Debug] SkipWave 호출 — wave={wave}, isGameOver={isGameOver}, waitingForUpgrade={waitingForUpgrade}, timeScale={Time.timeScale}");
        if (isGameOver) { Debug.LogWarning("[Debug] isGameOver=true → 중단"); return; }

        StopAllCoroutines();
        Time.timeScale    = 1f;
        waitingForUpgrade = false;

        // 보스 웨이브 중 스킵 시 보스 상태 초기화
        if (bossWaveActive)
        {
            bossWaveActive = false;
            WaveEnvironmentSwitcher.Shared?.SetNormal();
            BossHUDPanel.Shared?.Hide();
        }
        WaveEnvironmentSwitcher.Shared?.DeactivateExitGate();

        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();
        FXManager.Shared?.ClearAllFX();

        Debug.Log("[Debug] 증강 스킵하고 바로 웨이브 시작");
        StartNextWave();
    }

    /// <summary>지정한 웨이브로 즉시 이동합니다.</summary>
    public void DebugJumpToWave(int targetWave)
    {
        if (isGameOver) return;

        StopAllCoroutines();
        Time.timeScale = 1f;
        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        if (bossWaveActive)
        {
            bossWaveActive = false;
            WaveEnvironmentSwitcher.Shared?.SetNormal();
            BossHUDPanel.Shared?.Hide();
        }

        bossWaveActive    = false;
        wave              = targetWave - 1;
        waitingForUpgrade = false;

        // 보스 입장 게이트(Exit Point) 연출을 띄우려면, 활성화된 배경 맵이 해당 층(예: 9웨이브)의 맵이어야 합니다.
        // 직전 맵으로 강제 세팅하여 보스 문이 정상 활성화되도록 패치
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);

        StartNextWave();
        Debug.Log($"[Debug] → Wave {targetWave}로 점프");
    }

    /// <summary>DB_웨이브에서 현재 웨이브보다 큰 가장 가까운 보스 웨이브로 이동합니다.</summary>
    public void DebugJumpToBossWave()
    {
        Debug.Log($"[Debug] JumpToBossWave 호출 — wave={wave}, isGameOver={isGameOver}, timeScale={Time.timeScale}");
        if (isGameOver) { Debug.LogWarning("[Debug] isGameOver=true → 중단"); return; }

        int targetWave = EnemySpawner.Shared?.GetNextBossWave(wave) ?? -1;

        if (targetWave < 0)
        {
            Debug.LogWarning("[Debug] 현재 웨이브보다 큰 보스 웨이브 항목이 없습니다.");
            return;
        }

        Debug.Log($"[Debug] → Boss Wave {targetWave}로 점프");

        StopAllCoroutines();
        Time.timeScale    = 1f;
        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        bossWaveActive    = false;
        wave              = targetWave - 1;
        waitingForUpgrade = false;

        // 보스 입장 게이트(Exit Point) 연출을 띄우려면, 활성화된 배경 맵이 해당 층(예: 9웨이브)의 맵이어야 합니다.
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);

        StartNextWave();
    }
}
