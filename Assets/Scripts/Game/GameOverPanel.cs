using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 부활 패널 — 사망 시 표시. 5초 카운트다운 후 게임오버 패널로 전환.
/// 부활 버튼(선택)을 누르면 즉시 부활.
/// GameManager.TriggerGameOver() → gameOverPanel.SetActive(true) → OnEnable 자동 시작.
/// </summary>
public class GameOverPanel : MonoBehaviour
{
    [Header("카운트다운")]
    [SerializeField] private TextMeshProUGUI countdownText;
    [SerializeField] private int             countdownSeconds = 5;

    [Header("부활 버튼 (선택)")]
    [SerializeField] private Button reviveButton;

    [Header("패널 축소 애니메이션")]
    [SerializeField] private float shrinkDuration = 0.4f;

    private bool _revived;

    private void Awake()
    {
        reviveButton?.onClick.AddListener(OnRevive);
    }

    private void OnEnable()
    {
        _revived = false;
        transform.localScale = Vector3.one;
        StartCoroutine(CountdownRoutine());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    private void OnRevive()
    {
        _revived = true;
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
            GameManager.Shared?.ShowGameOverResult();
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
