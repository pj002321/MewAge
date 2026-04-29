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
    [SerializeField] private TextMeshProUGUI waveText;

    [Header("아웃게임 보상")]
    [SerializeField] private TextMeshProUGUI rewardGoldText;
    [SerializeField] private TextMeshProUGUI rewardExpText;
    [SerializeField] private TextMeshProUGUI rewardRubyText;

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

        var reward = RunRewardCalculator.Calculate(wave, isClear: false);
        AccountManager.Shared?.GiveReward(reward);

        if (rewardGoldText != null) rewardGoldText.text = $"+{reward.gold}";
        if (rewardExpText  != null) rewardExpText.text  = $"+{reward.exp}";
        if (rewardRubyText != null) rewardRubyText.text = reward.ruby > 0 ? $"+{reward.ruby}" : "-";
    }
}
