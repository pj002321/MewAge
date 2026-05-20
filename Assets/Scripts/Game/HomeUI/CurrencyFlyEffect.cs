using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재화 아이콘이 HUD 목표 지점으로 빨려들어가는 연출.
/// Canvas 루트 하위 RectTransform에 부착합니다.
/// </summary>
public class CurrencyFlyEffect : MonoBehaviour
{
    [SerializeField] private Canvas targetCanvas;

    private RectTransform _canvasRT;

    private void Awake()
    {
        if (targetCanvas == null) targetCanvas = GetComponentInParent<Canvas>();
        _canvasRT = targetCanvas != null ? targetCanvas.GetComponent<RectTransform>() : null;
    }

    /// <summary>
    /// 아이콘을 screenFrom에서 targetRT 방향으로 count개 날립니다.
    /// </summary>
    public void Play(Sprite icon, Vector2 screenFrom, RectTransform targetRT,
                     int count = 6, float duration = 0.55f)
    {
        if (_canvasRT == null || targetRT == null || icon == null) return;
        StartCoroutine(SpawnRoutine(icon, screenFrom, targetRT, count, duration));
    }

    private IEnumerator SpawnRoutine(Sprite icon, Vector2 screenFrom, RectTransform targetRT,
                                     int count, float duration)
    {
        Vector2 fromLocal  = ScreenToCanvasLocal(screenFrom);
        Vector2 targetLocal = ScreenToCanvasLocal(
            RectTransformUtility.WorldToScreenPoint(targetCanvas.worldCamera, targetRT.position));

        for (int i = 0; i < count; i++)
        {
            SpawnOne(icon, fromLocal, targetLocal, duration);
            yield return new WaitForSecondsRealtime(0.055f);
        }
    }

    private void SpawnOne(Sprite icon, Vector2 fromLocal, Vector2 targetLocal, float duration)
    {
        var go = new GameObject("FlyIcon");
        go.transform.SetParent(_canvasRT, false);

        var rt         = go.AddComponent<RectTransform>();
        rt.sizeDelta   = new Vector2(36f, 36f);
        rt.anchoredPosition = fromLocal + Random.insideUnitCircle * 35f;

        var img           = go.AddComponent<Image>();
        img.sprite        = icon;
        img.raycastTarget = false;

        // 흩어졌다가 → 수렴
        Vector2 scatter = fromLocal + Random.insideUnitCircle * 60f;
        var seq = DOTween.Sequence().SetUpdate(true);
        seq.Append(rt.DOAnchorPos(scatter, 0.18f).SetEase(Ease.OutQuad));
        seq.Append(rt.DOAnchorPos(targetLocal, duration).SetEase(Ease.InQuart));
        seq.Join(go.transform.DOScale(Vector3.zero, 0.12f)
                    .SetDelay(duration - 0.05f)
                    .SetEase(Ease.InBack)
                    .SetUpdate(true));
        seq.OnComplete(() => Destroy(go));
    }

    private Vector2 ScreenToCanvasLocal(Vector2 screenPos)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _canvasRT, screenPos, targetCanvas.worldCamera, out Vector2 local);
        return local;
    }
}
