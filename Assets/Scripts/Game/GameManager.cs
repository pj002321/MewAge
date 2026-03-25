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
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private Slider          expSlider;
    [SerializeField] private Slider          reroll_expSlider;
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private GameObject      gameOverPanel;
    [SerializeField] private GameObject      waveInfoProgress;

    [Header("Wave 타이머")]
    [SerializeField] private float baseWaveDuration    = 20f;
    [SerializeField] private float waveDurationIncrease = 2f;

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

    protected override bool DontDestroy => false;

    private void Start()
    {
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (waveClearPanel) waveClearPanel.SetActive(false);

        // 시작 시 Normal 무기 1개 지급 후 바로 Wave 1 시작
        UpgradeManager.Shared?.GiveStarterWeapon();
        StartNextWave();
    }

    private void Update()
    {
        if (isGameOver) return;
        if (bossWaveActive) return;   // 보스 생존 중 타이머 동결

        waveTimer -= Time.deltaTime;
        UpdateTimerUI();

        if (waveTimer <= 0f && !waitingForUpgrade)
        {
            waitingForUpgrade = true;
            StartCoroutine(WaveClearRoutine());
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
        EnemySpawner.Shared?.ClearAllEnemies(); // 이전 웨이브 잔여 적 제거
        yield return new WaitForSeconds(waveClearDuration);
        if (waveClearPanel) waveClearPanel.SetActive(false);

        if (ImprovManager.Shared != null)
            ImprovManager.Shared.ShowPanel(wave);
        else if (UpgradeManager.Shared != null)
            UpgradeManager.Shared.ShowPanel(wave);
        else
            StartNextWave();
    }

    private float WaveDuration(int w) => baseWaveDuration + Mathf.Max(0, w - 1) * waveDurationIncrease;

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
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public void GoHome()
    {
        Time.timeScale = 1f;
        UnityEngine.SceneManagement.SceneManager.LoadScene("home@scene");
    }

    public void StartNextWave()
    {
        wave++;
        waveTimer         = WaveDuration(wave);
        waitingForUpgrade = false;

        // WaveHealHP 패시브 — 웨이브 시작 시 축적된 회복량만큼 체력 회복
        var player = PlayerController.Instance;
        if (player != null && player.WaveHealAmount > 0f)
            player.HealHP(player.WaveHealAmount);

        EnemySpawner.Shared?.ResumeSpawning(wave);
        WaveEnvironmentSwitcher.Shared?.SetWave(wave);
        UpdateWaveUI();
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;

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

    public void OnBossDefeated()
    {
        bossWaveActive = false;
        WaveEnvironmentSwitcher.Shared?.SetNormal();
        if (!waitingForUpgrade)
        {
            waitingForUpgrade = true;
            StartCoroutine(WaveClearRoutine());
        }
    }

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
