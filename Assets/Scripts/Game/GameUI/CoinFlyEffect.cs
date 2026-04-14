using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 팔기 시 코인 이미지가 gold_text 위치로 날아가며 잔상을 남기는 이펙트.
/// Canvas 자식으로 생성되어 자동 소멸합니다.
/// </summary>
public class CoinFlyEffect : MonoBehaviour
{
    [SerializeField] private Image  coinImage;
    [SerializeField] private float  flyDuration    = 0.55f;
    [SerializeField] private int    ghostCount     = 4;
    [SerializeField] private float  ghostInterval  = 0.07f;
    [SerializeField] private float  arcHeight      = 60f;   // 포물선 높이

    private RectTransform rt;

    private void Awake() => rt = GetComponent<RectTransform>();

    /// <summary>
    /// startScreenPos: 출발 스크린 좌표 (인벤 슬롯 위치)
    /// targetScreenPos: 목적지 스크린 좌표 (gold_text 위치)
    /// parentCanvas: 이 이펙트를 붙일 Canvas
    /// </summary>
    public void Play(Vector2 startScreenPos, Vector2 targetScreenPos, Canvas parentCanvas)
    {
        transform.SetParent(parentCanvas.transform, false);
        transform.SetAsLastSibling();
        StartCoroutine(FlyRoutine(startScreenPos, targetScreenPos, parentCanvas));
    }

    private IEnumerator FlyRoutine(Vector2 startScreen, Vector2 targetScreen, Canvas canvas)
    {
        Vector2 start  = ToLocal(canvas, startScreen);
        Vector2 target = ToLocal(canvas, targetScreen);
        rt.anchoredPosition = start;

        // 잔상 먼저 스폰
        for (int i = 1; i <= ghostCount; i++)
            StartCoroutine(GhostRoutine(start, target, canvas, i * ghostInterval));

        // 메인 코인 포물선 이동
        float elapsed = 0f;
        while (elapsed < flyDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t   = Mathf.Clamp01(elapsed / flyDuration);
            float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
            rt.anchoredPosition = Vector2.Lerp(start, target, t) + Vector2.up * arc;
            yield return null;
        }

        Destroy(gameObject);
    }

    private IEnumerator GhostRoutine(Vector2 start, Vector2 target, Canvas canvas, float delay)
    {
        yield return new WaitForSecondsRealtime(delay);

        var go  = new GameObject("CoinGhost", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        go.transform.SetParent(canvas.transform, false);
        go.transform.SetAsLastSibling();

        var img = go.GetComponent<Image>();
        if (coinImage) img.sprite = coinImage.sprite;
        img.raycastTarget = false;

        var cg = go.GetComponent<CanvasGroup>();
        var grt = go.GetComponent<RectTransform>();
        grt.sizeDelta = rt.sizeDelta;

        float duration = flyDuration * 0.55f;
        float elapsed  = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t   = Mathf.Clamp01(elapsed / duration);
            float arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
            grt.anchoredPosition = Vector2.Lerp(start, target, t) + Vector2.up * arc;
            cg.alpha = Mathf.Lerp(0.45f, 0f, t);
            yield return null;
        }
        Destroy(go);
    }

    private static Vector2 ToLocal(Canvas canvas, Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform, screenPos,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out Vector2 local);
        return local;
    }
}
