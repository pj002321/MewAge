using System.Collections;
using UnityEngine;

/// <summary>
/// 텔레포트 적이 솟아오를 위치를 미리 알려주는 마커.
/// TeleportBehaviour에서 Instantiate 후 Show()를 호출합니다.
/// duration 동안 펄스 애니메이션을 재생하고 자동으로 파괴됩니다.
/// </summary>
public class TeleportMarker : MonoBehaviour
{
    [Header("펄스 설정")]
    [SerializeField] private float minScale    = 0.5f;
    [SerializeField] private float maxScale    = 1.2f;
    [SerializeField] private float pulseSpeed  = 3f;   // 초당 사이클

    [Header("색상")]
    [SerializeField] private Color startColor = new Color(1f, 0.3f, 0.3f, 0f);  // 투명 시작
    [SerializeField] private Color peakColor  = new Color(1f, 0.3f, 0.3f, 0.8f);
    [SerializeField] private Color endColor   = new Color(1f, 0.8f, 0.3f, 1f);  // 스폰 직전 밝게

    private SpriteRenderer _sr;

    private void Awake()
    {
        _sr = GetComponentInChildren<SpriteRenderer>(true);
    }

    /// <summary>duration 동안 마커를 표시한 뒤 자동 파괴됩니다.</summary>
    public void Show(float duration)
    {
        StartCoroutine(MarkerRoutine(duration));
    }

    private IEnumerator MarkerRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            float t = elapsed / duration;  // 0 → 1

            // 스케일: sin 기반 펄스
            float pulse = (Mathf.Sin(elapsed * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f; // 0~1
            float scale = Mathf.Lerp(minScale, maxScale, pulse);
            transform.localScale = Vector3.one * scale;

            // 색상: 등장(페이드인) → 유지 → 스폰 직전(밝아짐)
            Color color;
            if (t < 0.2f)
                color = Color.Lerp(startColor, peakColor, t / 0.2f);
            else if (t < 0.8f)
                color = peakColor;
            else
                color = Color.Lerp(peakColor, endColor, (t - 0.8f) / 0.2f);

            if (_sr) _sr.color = color;

            elapsed += Time.deltaTime;
            yield return null;
        }

        Destroy(gameObject);
    }
}
