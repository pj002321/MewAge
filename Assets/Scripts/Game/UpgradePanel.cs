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
    [SerializeField] private TextMeshProUGUI rerollPriceText;
    [SerializeField] private Button          buyExpButton;
    [SerializeField] private TextMeshProUGUI buyExpPriceText;
    [SerializeField] private Button          nextWaveButton;

    [Header("정보 텍스트")]
    [SerializeField] private TextMeshProUGUI ownedCountText;    // "보유 0 / 10" (선택)
    [SerializeField] private TextMeshProUGUI playerGoldText;    // 패널 내 골드 표시
    [SerializeField] private TextMeshProUGUI waveCompleteText;  // "Wave N 클리어!" 타이틀

    [Header("노티피케이션 (Image 오브젝트)")]
    [SerializeField] private GameObject      notificationRoot;  // Image가 포함된 부모 오브젝트
    [SerializeField] private TextMeshProUGUI notificationText;  // 노티 안의 메시지 텍스트
    [SerializeField] private float           notificationDuration = 2f;

    [Header("자동 진행 타이머")]
    [SerializeField] private TextMeshProUGUI autoStartTimerText;  // 카운트다운 표시 텍스트
    [SerializeField] private float           autoStartDelay = 30f;

    private Coroutine notificationCoroutine;
    private Coroutine autoStartCoroutine;

    public GameObject[] activePanel;
    public GameObject deActivePanel;

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
        UpgradeManager.Shared?.ClosePanel();
    }

    private void OnEnable()
    {
        // timeScale=0이어도 패널 내 모든 Animator가 재생되도록
        foreach (var anim in GetComponentsInChildren<Animator>(true))
            anim.updateMode = AnimatorUpdateMode.UnscaledTime;

        foreach (var ag in activePanel)
        {
            ag.SetActive(true);
        }
        deActivePanel.SetActive(false);

        autoStartCoroutine = StartCoroutine(AutoStartRoutine());
    }

    private void OnDisable()
    {
        if (autoStartCoroutine != null) StopCoroutine(autoStartCoroutine);
    }

    public void Refresh(WeaponData[] drawnCards, int rerollPrice, int buyExpPrice,
                        int ownedCount, int maxOwned, int wave)
    {
        // 카드 세팅
        for (int i = 0; i < cards.Length; i++)
        {
            if (i < drawnCards.Length && drawnCards[i] != null)
                cards[i].Setup(drawnCards[i]);
            else
                cards[i].SetEmpty();
        }

        if (rerollPriceText) rerollPriceText.text = $"{rerollPrice} G";
        if (buyExpPriceText) buyExpPriceText.text  = $"{buyExpPrice} G";
        if (ownedCountText)  ownedCountText.text   = $"보유 {ownedCount} / {maxOwned}";
        if (waveCompleteText) waveCompleteText.text = $"Wave {wave} 클리어!";

        RefreshGold();
    }

    public void RefreshGold()
    {
        if (playerGoldText && PlayerStats.Shared != null)
            playerGoldText.text = $"{PlayerStats.Shared.Gold} G";
    }

    /// <summary>구매 실패 등 안내 메시지를 Image 노티로 표시 후 자동으로 숨깁니다.</summary>
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
            if (autoStartTimerText) autoStartTimerText.text = Mathf.CeilToInt(remaining).ToString();
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

        yield return new WaitForSecondsRealtime(notificationDuration); // timeScale=0에서도 동작

        notificationRoot.SetActive(false);
    }
}
