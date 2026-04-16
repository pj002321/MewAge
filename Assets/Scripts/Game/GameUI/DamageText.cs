using TMPro;
using UnityEngine;

/// <summary>
/// 적이 데미지를 받을 때 팝인 → 좌우 떨림 → 페이드아웃하는 숫자 텍스트.
/// TMP_Text 컴포넌트가 있는 Prefab에 부착하세요.
/// </summary>
public class DamageText : MonoBehaviour
{
    [SerializeField] private TMP_Text label;

    [Header("타이밍")]
    [SerializeField] private float lifetime       = 0.7f;   // 총 표시 시간
    [SerializeField] private float fadeStartRatio = 0.55f;  // 이 비율 이후부터 페이드

    [Header("떨림")]
    [SerializeField] private float shakeAmplitude = 0.09f;  // 떨림 폭 (월드 단위)
    [SerializeField] private float shakeFrequency = 55f;    // 떨림 빈도 (Hz)

    [Header("팝인 스케일")]
    [SerializeField] private AnimationCurve scaleCurve = DefaultCurve();

    private float   timer;
    private Color   baseColor;
    private Vector3 originPos;

    // ── 초기화 ──────────────────────────────────────────────────
    public void Init(float damage, bool isCritical = false, bool isBleed = false)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        int displayDmg = damage > 0f && damage < 1f ? 1 : Mathf.RoundToInt(damage);
        label.text  = isBleed ? $"-{displayDmg}" : displayDmg.ToString();
        baseColor   = (isCritical || isBleed) ? Color.red : label.color;
        label.color = baseColor;
        timer       = lifetime;
        originPos   = transform.position;

        transform.localScale = Vector3.zero;
    }

    // ── 매 프레임 ────────────────────────────────────────────────
    private void Update()
    {
        timer -= Time.deltaTime;
        float progress = 1f - (timer / lifetime);   // 0 → 1

        // 스케일: 빠르게 팝인
        float s = scaleCurve.Evaluate(progress);
        transform.localScale = Vector3.one * Mathf.Max(s, 0f);

        // 떨림: 시간이 갈수록 줄어드는 수평 진동
        float decay  = 1f - progress;
        float shakeX = Mathf.Sin(Time.time * shakeFrequency) * shakeAmplitude * decay;
        transform.position = originPos + new Vector3(shakeX, 0f, 0f);

        // 페이드아웃
        float fadeLen = 1f - fadeStartRatio;
        float alpha   = progress < fadeStartRatio
            ? 1f
            : Mathf.Lerp(1f, 0f, (progress - fadeStartRatio) / fadeLen);
        label.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        if (timer <= 0f)
        {
            if (FXManager.Shared != null) FXManager.Shared.ReturnDamageText(this);
            else                          Destroy(gameObject);
        }
    }

    // ── 기본 AnimationCurve ─────────────────────────────────────
    private static AnimationCurve DefaultCurve()
    {
        // 0→0.12s 에 1.25까지 팡 튀고 → 1.0으로 안착
        return new AnimationCurve(
            new Keyframe(0f,    0f,   0f,  8f),
            new Keyframe(0.12f, 1.25f, 0f,  0f),
            new Keyframe(0.25f, 1.0f,  0f,  0f),
            new Keyframe(1f,    1.0f,  0f,  0f)
        );
    }
}
