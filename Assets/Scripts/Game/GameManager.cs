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
    [SerializeField] private GameObject      gameOverPanel;
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
    private bool  waitingForUpgrade;
    private bool  bossWaveActive;

    // Session stats (accumulated during a single run for the Clear Panel)
    private int sessionExpGained  = 0;
    private int sessionGoldGained = 0;

    // 앱 전환 / 알림바 등 시스템 인터럽트 대응
    private bool  _systemPaused;
    private float _savedTimeScale;

    protected override bool DontDestroy => false;

    private void Start()
    {
        if (gameOverPanel)               gameOverPanel.SetActive(false);
        if (waveClearPanel)              waveClearPanel.SetActive(false);
        if (gameClearPanel != null)      gameClearPanel.gameObject.SetActive(false);

        // 이전 판 증강 수치 초기화 후 캐릭터 기초 스탯 재적용
        PlayerStatManager.Shared?.ResetMods();
        PlayerCharacterManager.Shared?.ApplyCharacterStats();

        // 시작 시 Normal 무기 1개 지급 후 바로 Wave 1 시작
        UpgradeManager.Shared?.GiveStarterWeapon();
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

        const float sliceTime = 1f;
        int milestone = wave / 10; // 지나간 10웨이브 마일스톤 수

        if (wave % 10 != 0)
        {
            // 10의 배수가 아닌 웨이브: 마지막 마일스톤 위치에 멈춤
            float pauseSec = milestone * sliceTime;
            anim.Play(stateInfo.fullPathHash, 0, pauseSec / clipLength);
            anim.speed = 0f;
            return;
        }

        // 10의 배수 웨이브: 이전 마일스톤 위치에서 1초 재생 후 멈춤
        float startSec = (milestone - 1) * sliceTime;
        anim.Play(stateInfo.fullPathHash, 0, startSec / clipLength);
        anim.speed = 1f;
        StartCoroutine(PauseAnimAfterSlice(anim, sliceTime));
    }

    private IEnumerator PauseAnimAfterSlice(Animator anim, float delay)
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

        // 필드에 남은 드롭 아이템 → 플레이어 방향으로 유도
        foreach (var drop in Object.FindObjectsByType<DropItem>(FindObjectsSortMode.None))
            drop.ForceAttract();

        yield return new WaitForSeconds(waveClearDuration);

        // 유도 후 미수집 아이템 강제 수집
        foreach (var drop in Object.FindObjectsByType<DropItem>(FindObjectsSortMode.None))
            if (drop != null) drop.Collect();

        if (waveClearPanel) waveClearPanel.SetActive(false);

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
        if (waveText) waveText.text = $"Wave {wave}";
        if (timerText) timerText.color = timerNormalColor;
        WaveInfoProgress();
    }

    public void UpdateHP(float current, float max)
    {
        float normalized = max > 0f ? current / max : 0f;
        if (hpSlider) hpSlider.value = normalized;
        if (hpText)   hpText.text    = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
    }

    public void UpdateGoldUI(int gold)
    {
        if (goldText) goldText.text = $"{gold}";
    }

    public void UpdateExpUI(int exp, int expMax, int level)
    {
        if (levelText)        levelText.text         = $"lv {level}";
        if (reroll_levelText) reroll_levelText.text  = $"lv {level}";
        if (expText)          expText.text          = $"{exp} / {expMax}";
        if (expSlider)        expSlider.value        = (float)exp / expMax;
        if (reroll_expSlider) reroll_expSlider.value = (float)exp / expMax;
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        EnemySpawner.Shared?.StopSpawning();
        if (gameOverPanel) gameOverPanel.SetActive(true);
        // GameOverPanel.cs will count down 5 s, shrink itself, then call ShowGameClearAfterGameOver()
    }

    /// <summary>
    /// GameOverPanel 카운트다운 + 축소 연출이 끝난 뒤 호출됩니다.
    /// GameClear 패널을 표시합니다.
    /// </summary>
    public void ShowGameClearAfterGameOver()
    {
        if (gameClearPanel != null)
        {
            gameClearPanel.Show(wave, sessionExpGained, sessionGoldGained);
        }
        else
        {
            this.DLog("[GameManager] ShowGameClearAfterGameOver: gameClearPanel not assigned.");
        }
    }

    public void Revive()
    {
        if (!isGameOver) return;
        isGameOver = false;
        if (gameOverPanel) gameOverPanel.SetActive(false);
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

        // 보스 웨이브이고 인트로 시퀀스가 있으면 컷씬 먼저 재생
        bool isBossWave = EnemySpawner.Shared?.GetNextBossWave(wave - 1) == wave;
        // Boss wave gets its own time limit; normal waves use stepped duration
        waveTimer = isBossWave ? bossWaveTimeLimit : WaveDuration(wave);

        this.DLog($"[GameManager] StartNextWave wave={wave} | isBossWave={isBossWave} | timer={waveTimer}s | BossIntro={(BossIntroSequence.Shared != null ? "있음" : "없음")}");

        UpdateWaveUI();

        if (isBossWave && BossIntroSequence.Shared != null)
        {
            WaveEnvironmentSwitcher.Shared?.SetWave(wave); // 일반 맵 유지 (출구 연출용)
            BossIntroSequence.Shared.Play(() =>
            {
                EnemySpawner.Shared?.ResumeSpawning(wave);
            });
            return;
        }

        EnemySpawner.Shared?.ResumeSpawning(wave);
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;

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

        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        waitingForUpgrade = true;
        Debug.Log("[Debug] WaveClearRoutine 시작");
        StartCoroutine(WaveClearRoutine());
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
        StartNextWave();
    }
}
