using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private TextMeshProUGUI timerText;   // 웨이브 남은 시간
    [SerializeField] private Slider hpSlider;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Wave 타이머")]
    [SerializeField] private float baseWaveDuration    = 20f;  // Wave 1 지속 시간
    [SerializeField] private float waveDurationIncrease = 2f;  // 웨이브마다 증가량

    private int   score;
    private int   wave = 1;
    private float waveTimer;
    private bool  isGameOver;

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

        if (waveTimer <= 0f)
        {
            wave++;
            waveTimer = WaveDuration(wave);
            EnemySpawner.Shared?.OnWaveUp(wave);
            UpdateWaveUI();
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

    public void AddScore(int amount)
    {
        score += amount;
        if (scoreText) scoreText.text = $"Score: {score}";
    }

    public void UpdateHP(float normalized)
    {
        if (hpSlider) hpSlider.value = normalized;
    }

    public void TriggerGameOver()
    {
        if (isGameOver) return;
        isGameOver = true;
        EnemySpawner.Shared?.StopSpawning();
        if (gameOverPanel) gameOverPanel.SetActive(true);
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    public bool IsGameOver  => isGameOver;
    public int  CurrentWave => wave;
}
