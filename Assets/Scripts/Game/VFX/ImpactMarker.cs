using System.Collections;
using UnityEngine;

/// <summary>
/// 낙탄 예고 마커.
/// 생성 시 스케일 팝인 → 알파 펄스 루프.
/// BossVolleySkill 이 Dismiss() 를 호출하면 사라집니다.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class ImpactMarker : MonoBehaviour
{
    [SerializeField] private float popInDuration = 0.12f;
    [SerializeField] private float pulseSpeed    = 4f;
    [SerializeField] private float pulseMinAlpha = 0.35f;
    [SerializeField] private float targetScale   = 1f;

    private SpriteRenderer sr;
    private bool dismissed;

    private void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
        transform.localScale = Vector3.zero;
        StartCoroutine(PopIn());
    }

    private IEnumerator PopIn()
    {
        float elapsed = 0f;
        Vector3 target = Vector3.one * targetScale;

        while (elapsed < popInDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / popInDuration);
            transform.localScale = Vector3.LerpUnclamped(Vector3.zero, target, t);
            yield return null;
        }
        transform.localScale = target;

        // 알파 펄스 루프
        float phase = 0f;
        while (!dismissed)
        {
            phase += Time.deltaTime * pulseSpeed;
            float a = Mathf.Lerp(pulseMinAlpha, 1f, (Mathf.Sin(phase) + 1f) * 0.5f);
            SetAlpha(a);
            yield return null;
        }
    }

    /// <summary>충격 발생 시 호출 — 마커를 즉시 파괴합니다.</summary>
    public void Dismiss()
    {
        dismissed = true;
        Destroy(gameObject);
    }

    private void SetAlpha(float a)
    {
        if (sr == null) return;
        Color c = sr.color;
        c.a = a;
        sr.color = c;
    }
}
