using System.Collections;
using Mewage.DB;
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
    [SerializeField] private GameObject      gameOverPanel;

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
        timerText.text  = secs.ToString();
        timerText.color = waveTimer <= timerWarningThreshold ? timerWarningColor : timerNormalColor;
    }

    private void UpdateWaveUI()
    {
        if (waveText) waveText.text = $"Wave {wave}";
        if (timerText) timerText.color = timerNormalColor;
    }

    public void UpdateHP(float normalized)
    {
        if (hpSlider) hpSlider.value = normalized;
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
        UpdateWaveUI();
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;

    /// <summary>DB_웨이브에서 fromWave보다 큰 가장 가까운 보스 웨이브 번호를 반환합니다. 없으면 -1.</summary>
    public int GetNextBossWave(int fromWave)
    {
        int result = -1;
        DB_웨이브.ForEachEntity(e =>
        {
            if (e.f_보스 && e.f_시작웨이브 > fromWave)
                if (result < 0 || e.f_시작웨이브 < result)
                    result = e.f_시작웨이브;
        });
        return result;
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

        EnemySpawner.Shared?.StopSpawning();
        EnemySpawner.Shared?.ClearAllEnemies();

        waitingForUpgrade = true;
        Debug.Log("[Debug] WaveClearRoutine 시작");
        StartCoroutine(WaveClearRoutine());
    }

    /// <summary>DB_웨이브에서 현재 웨이브보다 큰 가장 가까운 보스 웨이브로 이동합니다.</summary>
    public void DebugJumpToBossWave()
    {
        Debug.Log($"[Debug] JumpToBossWave 호출 — wave={wave}, isGameOver={isGameOver}, timeScale={Time.timeScale}");
        if (isGameOver) { Debug.LogWarning("[Debug] isGameOver=true → 중단"); return; }

        // DB에서 현재 wave 초과하는 가장 가까운 보스 웨이브 탐색
        int targetWave = -1;
        DB_웨이브.ForEachEntity(e =>
        {
            Debug.Log($"  DB_웨이브 항목: f_시작웨이브={e.f_시작웨이브}, f_보스={e.f_보스}");
            if (e.f_보스 && e.f_시작웨이브 > wave)
                if (targetWave < 0 || e.f_시작웨이브 < targetWave)
                    targetWave = e.f_시작웨이브;
        });

        if (targetWave < 0)
        {
            Debug.LogWarning("[Debug] DB_웨이브에 현재 웨이브보다 큰 보스 웨이브 항목이 없습니다.");
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
