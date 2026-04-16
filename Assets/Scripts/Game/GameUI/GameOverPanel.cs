using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 부활 패널 — 사망 시 표시. 5초 카운트다운 후 게임오버 패널로 전환.
/// 2가지 방식(광고/유료)의 부활 버튼 제공.
/// GameManager.TriggerGameOver() → gameOverPanel.SetActive(true) → OnEnable 자동 시작.
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [Header("카운트다운")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private int             countdownSeconds = 5;

    [Header("부활 버튼")]
    [SerializeField] private Button reviveAdButton;
    [SerializeField] private Button revivePaidButton;
    [SerializeField] private Button skipButton;

    [Header("패널 축소 애니메이션")]
    [SerializeField] private float shrinkDuration = 0.4f;

    private bool _revived;

    private void Awake()
    {
        reviveAdButton?.onClick.AddListener(OnReviveAd);
        revivePaidButton?.onClick.AddListener(OnRevivePaid);
        skipButton?.onClick.AddListener(OnSkip);
    }

    private void OnEnable()
    {
        _revived = false;
        transform.localScale = Vector3.one;
        StartCoroutine(CountdownRoutine());

        this.DLog("GameOverPanel Enabled — Starting countdown.");
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    /// <summary>광고 시청 후 부활 버튼 핸들러</summary>
    private void OnReviveAd()
    {
        this.DLog("User requested Ad Revive.");
        // [TODO] 광고 시청 로직 추가 필요. 성공 시에만 ExecuteRevive() 호출.
        ExecuteRevive();
    }

    /// <summary>재화 소모 후 부활 버튼 핸들러</summary>
    private void OnRevivePaid()
    {
        this.DLog("User requested Paid Revive.");
        // [TODO] 재화(젬/다이아 등) 소모 로직 추가 필요. 성공 시에만 ExecuteRevive() 호출.
        ExecuteRevive();
    }

    /// <summary>카운트다운 스킵 버튼 핸들러</summary>
    private void OnSkip()
    {
        this.DLog("User clicked Skip button.");
        _revived = false;
        
        StopAllCoroutines();
        gameObject.SetActive(false);
        GameManager.Shared?.ShowGameOverResult();
    }

    /// <summary>실제 부활 프로세스 실행</summary>
    private void ExecuteRevive()
    {
        _revived = true;
        this.DLog("Revive Executed.");

        StopAllCoroutines();
        gameObject.SetActive(false);
        GameManager.Shared?.Revive();
    }

    private IEnumerator CountdownRoutine()
    {
        var wait = new WaitForSecondsRealtime(1f);

        for (int i = countdownSeconds; i > 0; i--)
        {
            if (countdownText != null) countdownText.text = i.ToString();
            yield return wait;
        }

        yield return StartCoroutine(ShrinkRoutine());

        gameObject.SetActive(false);
        if (!_revived)
        {
            this.DLog("Countdown finished without revive — Showing Result panel.");
            GameManager.Shared?.ShowGameOverResult();
        }
    }

    private IEnumerator ShrinkRoutine()
    {
        float elapsed = 0f;
        Vector3 start = transform.localScale;
        while (elapsed < shrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            transform.localScale = Vector3.Lerp(start, Vector3.zero, t * t);
            yield return null;
        }
        transform.localScale = Vector3.zero;
    }
}

/*
Unity Implementation Guide:
1. GameOverPanel 프리팹을 선택합니다.
2. Inspector에서 각 버튼 슬롯에 맞는 버튼을 할당합니다:
   - Revive Ad Button: 광고 시청 부활 버튼
   - Revive Paid Button: 유료 재화 부활 버튼
   - Skip Button: 5초 카운트다운 스킵 버튼
3. 각 버튼 내부의 텍스트나 아이콘이 용도에 맞게 설정되었는지 확인합니다.
4. 카운트다운 텍스트(TextMeshProUGUI)도 함께 연결되어 있는지 확인하십시오.
*/

