using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 게임오버 결과 패널 — 부활 패널 카운트다운 종료 후 표시.
/// 도달한 웨이브를 보여주고 로비/재시작 버튼을 제공합니다.
/// </summary>
public class GameOverResultPanel : MonoBehaviour
{
    [Header("웨이브 표시")]
    [SerializeField] private TextMeshProUGUI waveText;   // "WAVE 15"

    [Header("버튼")]
    [SerializeField] private Button homeButton;
    [SerializeField] private Button restartButton;

    private void Awake()
    {
        gameObject.SetActive(false);

        homeButton?.onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            GameManager.Shared?.GoHome();
        });
        restartButton?.onClick.AddListener(() =>
        {
            Time.timeScale = 1f;
            GameManager.Shared?.RestartGame();
        });
    }

    public void Show(int wave)
    {
        gameObject.SetActive(true);
        if (waveText != null) waveText.text = $"WAVE {wave}";
    }
}
