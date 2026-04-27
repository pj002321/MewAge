using System.Collections;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Mewage.DB;

/// <summary>
/// ImprovPanel 안의 증강 카드 하나.
/// Inspector 연결: iconImg, nameText, descText, button
/// </summary>
public class ImprovCard : MonoBehaviour
{
    [SerializeField] private Image           iconImg;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI descText;
    [SerializeField] private Button          button;

    [Header("선택 애니메이션")]
    [SerializeField] private float spinDuration  = 0.35f;  // 회전 한 바퀴 시간
    [SerializeField] private float selectScale   = 1.08f;  // 선택 카드 최종 스케일

    [Header("비선택 애니메이션")]
    [SerializeField] private Transform dimTarget  = null;   // 스케일 줄일 자식 (null이면 자기 자신)
    [SerializeField] private float     dimScale   = 0.65f;
    [SerializeField] private float     dimDuration = 0.2f;

    [Header("딤 설정")]
    [SerializeField] private float dimAlpha = 0.35f;

    public DB_증강 Data { get; private set; }

    private System.Action<ImprovCard, DB_증강> onClick;
    private RectTransform rt;
    private Vector2       originPos;
    private CanvasGroup   canvasGroup;

    private void Awake()
    {
        EnsureRt();
        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();

        button?.onClick.AddListener(() =>
        {
            if (Data != null) onClick?.Invoke(this, Data);
        });
    }

    private void EnsureRt()
    {
        if (rt != null) return;
        rt        = GetComponent<RectTransform>();
        originPos = rt.anchoredPosition;
    }

    public void Setup(DB_증강 data, System.Action<ImprovCard, DB_증강> onClickCallback)
    {
        EnsureRt();
        Data    = data;
        onClick = onClickCallback;

        // 위치/스케일 원래대로 리셋
        rt.anchoredPosition  = originPos;
        transform.localScale = Vector3.one;

        if (iconImg)
        {
            string augIdStr = data.f_증강ID.ToString();
            Sprite sprite = ImprovVisualConfig.Shared?.GetSprite(augIdStr)
                         ?? Resources.Load<Sprite>("AUG/" + augIdStr.ToLower());
            iconImg.sprite  = sprite;
            iconImg.enabled = sprite != null;
        }

        if (nameText) nameText.text = data.f_이름;
        if (descText)  descText.text = BuildDescription(data);

        if (button) button.interactable = true;
        SetDimmed(false);
        gameObject.SetActive(true);
    }

    public void SetEmpty()
    {
        Data = null;
        gameObject.SetActive(false);
    }

    /// <summary>선택되지 않은 카드를 어둡게 합니다.</summary>
    public void SetDimmed(bool dimmed)
    {
        if (canvasGroup == null) return;
        canvasGroup.alpha = dimmed ? dimAlpha : 1f;
    }

    /// <summary>선택된 카드: 360° 스핀 + 스케일 업</summary>
    public void PlaySelectAnimation()
    {
        StopAllCoroutines();
        StartCoroutine(SpinRoutine());
    }

    /// <summary>비선택 카드: 스케일 다운</summary>
    public void PlayDimAnimation()
    {
        StopAllCoroutines();
        var target = dimTarget != null ? dimTarget : transform;
        StartCoroutine(ScaleRoutine(target, dimScale, dimDuration));
    }

    /// <summary>선택 해제 시 즉시 원래대로 복구</summary>
    public void ResetAnimation()
    {
        StopAllCoroutines();
        EnsureRt();
        transform.localScale       = Vector3.one;
        transform.localEulerAngles = Vector3.zero;
        var target = dimTarget != null ? dimTarget : transform;
        target.localScale = Vector3.one;
    }

    private IEnumerator SpinRoutine()
    {
        float elapsed = 0f;
        Vector3 startScale = transform.localScale;
        Vector3 endScale   = Vector3.one * selectScale;

        while (elapsed < spinDuration)
        {
            float t    = elapsed / spinDuration;
            float ease = t < 0.5f ? 2f * t * t : 1f - Mathf.Pow(-2f * t + 2f, 2f) / 2f; // ease in-out

            // 360° 회전 (Y축)
            float angle = Mathf.Lerp(0f, 360f, ease);
            transform.localEulerAngles = new Vector3(0f, angle, 0f);

            // 스케일 업
            transform.localScale = Vector3.Lerp(startScale, endScale, ease);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        transform.localEulerAngles = Vector3.zero;
        transform.localScale       = endScale;
    }

    // ── 설명 텍스트 생성 ──────────────────────────────────────────────

    private static string BuildDescription(DB_증강 data)
    {
        var sb = new System.Text.StringBuilder();

        // f_설명 기본 텍스트 (있을 때만)
        string baseDesc = data.f_설명;
        if (!string.IsNullOrEmpty(baseDesc))
        {
            baseDesc = baseDesc.Replace("{0}", data.f_주수치.ToString());
            baseDesc = baseDesc.Replace("{1}", data.f_보조수치.ToString());
            sb.AppendLine(ColorizeNumbers(baseDesc));
        }


        return sb.ToString().TrimEnd();
    }

    /// <summary>설명 텍스트의 숫자를 맥락에 따라 녹색/빨강으로 채색합니다.</summary>
    private static string ColorizeNumbers(string text)
    {
        return Regex.Replace(text, @"[+\-]?\d+(?:\.\d+)?", match =>
        {
            string num = match.Value;
            bool isNegative = num.StartsWith("-");

            // 숫자가 속한 절(쉼표·마침표·줄바꿈으로 구분)만 검사해 오탐 방지
            string clause = GetClause(text, match.Index);
            bool hasReduction = clause.Contains("감소") || clause.Contains("하락") || clause.Contains("커지");

            string color = (isNegative || hasReduction) ? "#FF5555" : "#55FF88";
            return $"<color={color}>{num}</color>";
        });
    }

    /// <summary>pos가 속한 절을 반환합니다 (쉼표·마침표·줄바꿈을 경계로 분리).</summary>
    private static string GetClause(string text, int pos)
    {
        int start = pos;
        while (start > 0 && text[start - 1] != ',' && text[start - 1] != '.' && text[start - 1] != '\n')
            start--;

        int end = pos;
        while (end < text.Length && text[end] != ',' && text[end] != '.' && text[end] != '\n')
            end++;

        return text.Substring(start, end - start);
    }

    private IEnumerator ScaleRoutine(Transform target, float targetScale, float duration)
    {
        float elapsed     = 0f;
        Vector3 fromScale = target.localScale;
        Vector3 toScale   = Vector3.one * targetScale;

        while (elapsed < duration)
        {
            float t    = elapsed / duration;
            float ease = 1f - (1f - t) * (1f - t);
            target.localScale = Vector3.Lerp(fromScale, toScale, ease);
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        target.localScale = toScale;
    }
}
