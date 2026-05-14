using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// 플레이어 머리 위에서 위로 상승하며 페이드아웃하는 텍스트.
/// 코인/경험치 획득, 회복, 회피 피드백에 사용합니다.
/// </summary>
public class FloatingText : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Header("타이밍")]
    [SerializeField] private float lifetime       = 0.9f;
    [SerializeField] private float fadeStartRatio = 0.5f;

    [Header("이동")]
    [SerializeField] private float riseSpeed = 1.4f;

    [Header("크기")]
    [SerializeField] private float displayScale = 0.3f;

    private float timer;
    private Color baseColor;
    private bool  initialized;

    public void Init(string text, Color color)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null) { Debug.LogWarning("[FloatingText] TMP_Text 컴포넌트를 찾을 수 없습니다."); Destroy(gameObject); return; }

        label.text  = text;
        baseColor   = color;
        label.color = color;
        timer       = lifetime;
        initialized = true;

        transform.localScale = Vector3.zero;
    }

    /// <summary>숫자가 빠르게 바뀌다가 finalValue로 안착하는 룰렛 연출 후 일반 상승 애니메이션을 시작합니다.</summary>
    public void InitRoulette(int finalValue, Color color)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        if (label == null) { Destroy(gameObject); return; }

        baseColor   = color;
        label.color = color;
        initialized = false;
        transform.localScale = Vector3.one * displayScale;
        label.text = $"+{finalValue}";

        StopAllCoroutines();
        StartCoroutine(RouletteRoutine(finalValue));
    }

    private IEnumerator RouletteRoutine(int finalValue)
    {
        float duration = 0.5f;
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            float t        = elapsed / duration;
            float interval = Mathf.Lerp(0.04f, 0.15f, t * t);
            int   spread   = Mathf.RoundToInt(finalValue * (1f - t));
            int   display  = Mathf.Max(1, finalValue + Random.Range(-spread, spread + 1));
            label.text = $"+{display}";
            yield return new WaitForSecondsRealtime(interval);
            elapsed += interval;
        }

        label.text  = $"+{finalValue}";
        timer       = lifetime;
        initialized = true;
    }

    private void Update()
    {
        if (!initialized) return;

        // unscaledDeltaTime 사용 → timeScale=0 상황에서도 타이머 작동
        timer -= Time.unscaledDeltaTime;
        float progress = Mathf.Clamp01(1f - (timer / lifetime));

        // 위로 상승 (게임 시간 기준)
        transform.position += Vector3.up * riseSpeed * Time.unscaledDeltaTime;

        // 팝인: 0→1.25 빠르게, 이후 1.0으로 안착
        float s;
        if      (progress < 0.12f) s = Mathf.Lerp(0f,    1.25f, progress / 0.12f);
        else if (progress < 0.25f) s = Mathf.Lerp(1.25f, 1f,    (progress - 0.12f) / 0.13f);
        else                       s = 1f;
        transform.localScale = Vector3.one * (s * displayScale);

        // 페이드아웃
        float fadeLen = 1f - fadeStartRatio;
        float alpha = progress < fadeStartRatio
            ? 1f
            : Mathf.Lerp(1f, 0f, (progress - fadeStartRatio) / fadeLen);
        label.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        if (timer <= 0f) FXManager.Shared?.ReturnFloatingText(this);
    }
}
