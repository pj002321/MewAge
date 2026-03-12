using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI goldText;      // 골드 표시
    [SerializeField] private TextMeshProUGUI reroll_goldText;      // 골드 표시
    [SerializeField] private TextMeshProUGUI expText;       // 경험치/레벨 표시
    [SerializeField] private Slider          expSlider;     // 경험치 슬라이더 (선택)
    [SerializeField] private Slider          reroll_expSlider;     // 경험치 슬라이더 (선택)
    [SerializeField] private Slider          hpSlider;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Wave 타이머")]
    [SerializeField] private float baseWaveDuration    = 20f;  // Wave 1 지속 시간
    [SerializeField] private float waveDurationIncrease = 2f;  // 웨이브마다 증가량

    private int   wave = 1;
    private float waveTimer;
    private bool  isGameOver;
    private bool  waitingForUpgrade; // 업그레이드 패널 대기 중

    protected override bool DontDestroy => false;

    private void Start()
    {
        waveTimer = WaveDuration(wave);
        UpdateWaveUI();
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    private void Update()
    {
        if (isGameOver) return;

        waveTimer -= Time.deltaTime;
        UpdateTimerUI();

        if (waveTimer <= 0f && !waitingForUpgrade)
        {
            waitingForUpgrade = true;

            if (UpgradeManager.Shared != null)
                UpgradeManager.Shared.ShowPanel(wave);  // 패널이 닫히면 StartNextWave() 호출됨
            else
                StartNextWave(); // UpgradeManager 없으면 즉시 다음 웨이브
        }
    }

    // Wave N의 지속 시간 (20s, 22s, 24s ...)
    private float WaveDuration(int w) => baseWaveDuration + (w - 1) * waveDurationIncrease;

    private void UpdateTimerUI()
    {
        if (timerText == null) return;
        int secs = Mathf.CeilToInt(Mathf.Max(0f, waveTimer));
        timerText.text = secs.ToString();
    }

    private void UpdateWaveUI()
    {
        if (waveText) waveText.text = $"Wave {wave}";
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
        if (expText)   expText.text   = $"{exp} / {expMax}";
        if (expSlider) expSlider.value = (float)exp / expMax;
        if (reroll_expSlider) reroll_expSlider.value = (float)exp / expMax;
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        EnemySpawner.Shared?.StopSpawning();
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    /// <summary>게임오버 패널의 부활 버튼에 연결하세요.</summary>
    public void Revive()
    {
        if (!isGameOver) return;
        isGameOver = false;
        if (gameOverPanel) gameOverPanel.SetActive(false);
        PlayerController.Instance?.Revive();
        EnemySpawner.Shared?.ResumeSpawning(wave);   // spawning 플래그 복구 + 스폰 재개
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    /// <summary>UpgradeManager가 패널을 닫을 때 호출합니다.</summary>
    public void StartNextWave()
    {
        wave++;
        waveTimer         = WaveDuration(wave);
        waitingForUpgrade = false;
        EnemySpawner.Shared?.OnWaveUp(wave);
        UpdateWaveUI();
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;
}
