using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviourSingleton<GameManager>
{
    [Header("UI")]
    [SerializeField] private TextMeshProUGUI scoreText;
    [SerializeField] private TextMeshProUGUI waveText;
    [SerializeField] private Slider hpSlider;
    [SerializeField] private GameObject gameOverPanel;

    [Header("Wave")]
    [SerializeField] private float waveDuration = 30f;

    private int score;
    private int wave = 1;
    private float waveTimer;
    private bool isGameOver;

    protected override bool DontDestroy => false;

    private void Start()
    {
        waveTimer = waveDuration;
        UpdateUI();
        if (gameOverPanel) gameOverPanel.SetActive(false);
    }

    private void Update()
    {
        if (isGameOver) return;

        waveTimer -= Time.deltaTime;
        if (waveTimer <= 0f)
        {
            wave++;
            waveTimer = waveDuration;
            EnemySpawner.Shared?.OnWaveUp(wave);
            UpdateUI();
        }
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
        Debug.Log("[GameManager] Game Over");
    }

    public void RestartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
    }

    private void UpdateUI()
    {
        if (waveText) waveText.text = $"Wave {wave}";
    }

    public bool IsGameOver => isGameOver;
    public int CurrentWave => wave;
}
