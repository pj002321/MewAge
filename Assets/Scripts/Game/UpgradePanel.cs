using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 웨이브 종료 후 표시되는 업그레이드 패널 UI.
/// UpgradeManager가 Show/Hide 및 Refresh를 제어합니다.
/// </summary>
public class UpgradePanel : MonoBehaviour
{
    [Header("카드 슬롯 (4개)")]
    [SerializeField] private UpgradeCard[] cards;

    [Header("버튼")]
    [SerializeField] private Button          rerollButton;
    [SerializeField] private Button          buyExpButton;
    [SerializeField] private Button          nextWaveButton;

    [Header("정보 텍스트")]
    [SerializeField] private TextMeshProUGUI ownedCountText;
    [SerializeField] private TextMeshProUGUI playerGoldText;

    [Header("노티피케이션")]
    [SerializeField] private GameObject      notificationRoot;
    [SerializeField] private TextMeshProUGUI notificationText;
    [SerializeField] private float           notificationDuration = 2f;

    [Header("자동 진행 타이머")]
    [SerializeField] private TextMeshProUGUI autoStartTimerText;
    [SerializeField] private float           autoStartDelay = 30f;

    [Header("타이머 색상")]
    [SerializeField] private Color timerNormalColor  = Color.white;
    [SerializeField] private Color timerWarningColor = Color.red;
    [SerializeField] private float timerWarningThreshold = 5f;

    [Header("다음 웨이브 시작 딜레이")]
    [SerializeField] private float nextWaveDelay = 1f;

    private Coroutine notificationCoroutine;
    private Coroutine autoStartCoroutine;

    private void Awake()
    {
        rerollButton? .onClick.AddListener(() => UpgradeManager.Shared?.TryReroll());
        buyExpButton? .onClick.AddListener(() => UpgradeManager.Shared?.TryBuyExp());
        nextWaveButton?.onClick.AddListener(OnNextWaveClicked);
        gameObject.SetActive(false);
    }

    private void OnNextWaveClicked()
    {
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
        nextWaveButton.interactable = false;
        autoStartCoroutine = StartCoroutine(NextWaveDelayRoutine());
    }

    private IEnumerator NextWaveDelayRoutine()
    {
        float remaining = nextWaveDelay;
        while (remaining > 0f)
        {
            if (autoStartTimerText)
            {
                autoStartTimerText.text  = Mathf.CeilToInt(remaining).ToString();
                autoStartTimerText.color = timerWarningColor;
            }
            yield return new WaitForSecondsRealtime(0.1f);
            remaining -= 0.1f;
        }
        UpgradeManager.Shared?.ClosePanel();
    }

    private void OnEnable()
    {
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;
        if (nextWaveButton) nextWaveButton.interactable = true;
        autoStartCoroutine = StartCoroutine(AutoStartRoutine());
    }

    private void OnDisable()
    {
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
    }

    public void Refresh(WeaponData[] drawnCards, int rerollPrice, int buyExpPrice,
                        int ownedCount, int maxOwned, int wave)
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (i < drawnCards.Length && drawnCards[i] != null)
                cards[i].Setup(drawnCards[i]);
            else
                cards[i].SetEmpty();
        }
        RefreshGold();
    }

    public void RefreshGold()
    {
        if (playerGoldText && PlayerStats.Shared != null)
            playerGoldText.text = $"{PlayerStats.Shared.Gold} G";
    }

    public void ShowNotification(string message)
    {
        if (notificationRoot == null) return;
        if (notificationCoroutine != null) StopCoroutine(notificationCoroutine);
        notificationCoroutine = StartCoroutine(NotificationRoutine(message));
    }

    private IEnumerator AutoStartRoutine()
    {
        float remaining = autoStartDelay;
        while (remaining > 0f)
        {
            if (autoStartTimerText)
            {
                autoStartTimerText.text  = Mathf.CeilToInt(remaining).ToString();
                autoStartTimerText.color = remaining <= timerWarningThreshold
                    ? timerWarningColor : timerNormalColor;
            }
            yield return new WaitForSecondsRealtime(0.1f);
            remaining -= 0.1f;
        }
        if (autoStartTimerText) autoStartTimerText.text = "0";
        UpgradeManager.Shared?.ClosePanel();
    }

    private IEnumerator NotificationRoutine(string message)
    {
        if (notificationText) notificationText.text = message;
        notificationRoot.SetActive(true);
        yield return new WaitForSecondsRealtime(notificationDuration);
        notificationRoot.SetActive(false);
    }
}
