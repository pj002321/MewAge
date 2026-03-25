using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// GameOver Panel — activated by GameManager.TriggerGameOver().
/// 
/// Behaviour:
///   1. Activates panel and starts a 5-second countdown.
///   2. Updates countdownText each second (5 → 4 → 3 → 2 → 1 → GO!).
///   3. After countdown, plays a scale-down animation (panel shrinks to zero).
///   4. Then deactivates itself and asks GameManager to show the GameClear panel.
/// 
/// # Unity Setup:
///   1. Assign this script to the existing GameOver Panel GameObject.
///   2. Map countdownText to a TextMeshProUGUI under the panel that shows the countdown.
///   3. In GameManager Inspector, change the gameOverPanel reference to this same object.
///   4. GameManager.TriggerGameOver() already calls gameOverPanel.SetActive(true) —
///      this script's OnEnable() picks up from there automatically.
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [Header("카운트다운")]
    [SerializeField] private TextMeshProUGUI countdownText;   // "5", "4", ... "GO!"
    [SerializeField] private int             countdownSeconds = 5;

    [Header("패널 축소 애니메이션")]
    [SerializeField] private float shrinkDuration = 0.4f;    // 줄어드는 시간(초)

    // ── 라이프사이클 ──────────────────────────────────────────────────
    private void OnEnable()
    {
        // Panel이 활성화될 때마다 카운트다운 시작
        transform.localScale = Vector3.one;
        StartCoroutine(CountdownRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    // ── 카운트다운 → 축소 → GameClear ────────────────────────────────
    private IEnumerator CountdownRoutine()
    {
        var wait = new WaitForSecondsRealtime(1f); // timeScale=0 대비 Realtime 사용

        for (int i = countdownSeconds; i > 0; i--)
        {
            SetCountdownText(i.ToString());
            this.DLog($"[GameOverPanel] Countdown: {i}");
            yield return wait;
        }

        yield return new WaitForSecondsRealtime(0.5f);

        // 패널 Scale → 0 애니메이션
        yield return StartCoroutine(ShrinkRoutine());

        // 비활성화 후 GameClear 패널 표시
        gameObject.SetActive(false);
        GameManager.Shared?.ShowGameClearAfterGameOver();
    }

    private IEnumerator ShrinkRoutine()
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;

        while (elapsed < shrinkDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / shrinkDuration);
            // Ease-in: 처음엔 천천히, 마지막엔 빠르게 사라짐
            float eased = t * t;
            transform.localScale = Vector3.Lerp(startScale, Vector3.zero, eased);
            yield return null;
        }

        transform.localScale = Vector3.zero;
    }

    private void SetCountdownText(string msg)
    {
        if (countdownText != null) countdownText.text = msg;
    }
}
