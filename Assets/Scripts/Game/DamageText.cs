using TMPro;
using UnityEngine;

/// <summary>
/// 적이 데미지를 받을 때 위로 떠오르며 사라지는 숫자 텍스트.
/// TMP_Text 컴포넌트가 있는 Prefab에 부착하세요.
/// </summary>
public class DamageText : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private float moveSpeed  = 2f;
    [SerializeField] private float fadeDuration = 0.65f;
    [SerializeField] private AnimationCurve scaleCurve = AnimationCurve.EaseInOut(0f, 0f, 0.15f, 1.2f);

    private float timer;
    private Color baseColor;

    public void Init(float damage)
    {
        if (label == null) label = GetComponentInChildren<TMP_Text>();
        label.text = Mathf.RoundToInt(damage).ToString();
        baseColor = label.color;
        timer = fadeDuration;

        // 팡! 튀는 시작 스케일
        transform.localScale = Vector3.zero;
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        float progress = 1f - (timer / fadeDuration);   // 0 → 1

        // 위로 이동
        transform.position += Vector3.up * moveSpeed * Time.deltaTime;

        // 스케일: 빠르게 커졌다가 유지
        float s = scaleCurve.Evaluate(progress);
        transform.localScale = Vector3.one * Mathf.Max(s, 0f);

        // 후반부에 페이드아웃
        float alpha = progress < 0.5f ? 1f : Mathf.Lerp(1f, 0f, (progress - 0.5f) / 0.5f);
        label.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

        if (timer <= 0f)
            Destroy(gameObject);
    }
}
