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

    private int   wave = 1;
    private float waveTimer;
    private bool  isGameOver;
    private bool  waitingForUpgrade;

    protected override bool DontDestroy => false;

    private void Start()
    {
        waveTimer = WaveDuration(wave);
        UpdateWaveUI();
        if (gameOverPanel) gameOverPanel.SetActive(false);
        if (waveClearPanel) waveClearPanel.SetActive(false);
    }

    private void Update()
    {
        if (isGameOver) return;

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
        yield return new WaitForSeconds(waveClearDuration);
        if (waveClearPanel) waveClearPanel.SetActive(false);

        if (UpgradeManager.Shared != null)
            UpgradeManager.Shared.ShowPanel(wave);
        else
            StartNextWave();
    }

    private float WaveDuration(int w) => baseWaveDuration + (w - 1) * waveDurationIncrease;

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
        EnemySpawner.Shared?.OnWaveUp(wave);
        UpdateWaveUI();
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;
}
