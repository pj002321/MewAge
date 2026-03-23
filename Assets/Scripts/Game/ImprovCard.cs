using System.Collections;
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
    [SerializeField] private float selectLiftAmount = 30f;   // 올라갈 픽셀
    [SerializeField] private float selectScale      = 1.12f; // 최종 스케일
    [SerializeField] private float animDuration     = 0.25f;

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
            var sprite = data.f_아이콘;
            iconImg.sprite  = sprite;
            iconImg.enabled = sprite != null;
        }

        if (nameText) nameText.text = data.f_이름;
        if (descText)
        {
            string desc = data.f_설명;
            if (!string.IsNullOrEmpty(desc))
            {
                desc = desc.Replace("{0}", data.f_주수치.ToString());
                desc = desc.Replace("{1}", data.f_보조수치.ToString());
            }
            descText.text = desc ?? string.Empty;
        }

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

    public void PlaySelectAnimation()
    {
        StopAllCoroutines();
        StartCoroutine(SelectRoutine());
    }

    /// <summary>선택 해제 시 위치·스케일을 즉시 원래대로 되돌립니다.</summary>
    public void ResetAnimation()
    {
        StopAllCoroutines();
        EnsureRt();
        rt.anchoredPosition  = originPos;
        transform.localScale = Vector3.one;
    }

    private IEnumerator SelectRoutine()
    {
        Vector2 targetPos   = originPos + Vector2.up * selectLiftAmount;
        Vector3 targetScale = Vector3.one * selectScale;

        float elapsed = 0f;
        while (elapsed < animDuration)
        {
            float t = elapsed / animDuration;
            float ease = 1f - (1f - t) * (1f - t); // ease-out quad

            rt.anchoredPosition  = Vector2.Lerp(originPos, targetPos, ease);
            transform.localScale = Vector3.Lerp(Vector3.one, targetScale, ease);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        rt.anchoredPosition  = targetPos;
        transform.localScale = targetScale;
    }
}
