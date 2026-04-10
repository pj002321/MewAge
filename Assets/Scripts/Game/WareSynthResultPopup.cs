using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mewage.DB;

/// <summary>
/// 합성 결과 팝업 — 가챠처럼 슬롯이 뿅뿅 등장하는 연출.
/// 결과가 1종류면 중앙에 크게, 2종류 이상이면 그리드로 순차 등장.
///
/// Inspector 연결:
///   resultContent    — GridLayoutGroup Content (다중 결과)
///   singleSlotAnchor — 단일 결과용 중앙 앵커 (RectTransform)
///   slotPrefab       — WareSlotView 프리팹
///   titleText        — "합성 결과!" 타이틀
///   resultSummaryText — 결과 목록 텍스트
///   closeButton      — 닫기
/// </summary>
public class WareSynthResultPopup : MonoBehaviour
{
    [Header("결과 그리드 (다중)")]
    public RectTransform resultContent;
    public WareSlotView  slotPrefab;

    [Header("단일 결과 앵커")]
    public RectTransform singleSlotAnchor;

    [Header("텍스트")]
    public TMP_Text titleText;
    public TMP_Text resultSummaryText;

    [Header("닫기")]
    public Button closeButton;

    [Header("애니메이션")]
    public float popDuration = 0.28f;
    public float multiDelay  = 0.15f;

    Transform _originalParent;
    Canvas    _rootCanvas;

    void Awake()
    {
        closeButton?.onClick.AddListener(Close);
        gameObject.SetActive(false);
    }

    // ── 공개 진입점 ──────────────────────────────────────────────────

    public void Show(List<(string id, int count)> results)
    {
        if (results == null || results.Count == 0) return;

        // 루트 Canvas로 이동 → 최상단에 표시
        // (Panel_기물이 비활성일 때 GetComponentInParent가 null 반환할 수 있으므로 FindObjectsByType 사용)
        if (_rootCanvas == null)
        {
            foreach (var c in Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.isRootCanvas) { _rootCanvas = c; break; }
        }
        if (_originalParent == null) _originalParent = transform.parent;
        transform.SetParent(_rootCanvas != null ? _rootCanvas.transform : _originalParent, false);

        var rt = GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        gameObject.SetActive(true);
        StopAllCoroutines();
        ClearSlots();

        // 결과 요약 텍스트
        if (resultSummaryText)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var (id, cnt) in results)
            {
                var data = DB_기물.FindEntity(r => r.f_기물ID == id);
                string era = StructureWareManager.EraName(StructureWareManager.EraOf(id));
                sb.AppendLine($"[{era}] {data?.f_이름 ?? id}  ×{cnt}");
            }
            resultSummaryText.text = sb.ToString().TrimEnd();
        }

        if (results.Count == 1)
        {
            ShowSingle(results[0].id, results[0].count);
        }
        else
        {
            ShowMulti(results);
        }
    }

    // ── 단일 연출 ────────────────────────────────────────────────────

    void ShowSingle(string id, int count)
    {
        if (resultContent) resultContent.gameObject.SetActive(false);
        if (singleSlotAnchor) singleSlotAnchor.gameObject.SetActive(true);

        var parent = singleSlotAnchor != null ? singleSlotAnchor : (RectTransform)transform;
        var slot   = Instantiate(slotPrefab, parent);
        slot.Bind(id, null, count > 1 ? count : (int?)null);

        var slotRT       = slot.GetComponent<RectTransform>();
        slotRT.anchorMin = slotRT.anchorMax = new Vector2(0.5f, 0.5f);
        slotRT.pivot     = new Vector2(0.5f, 0.5f);
        slotRT.sizeDelta = new Vector2(220f, 260f);

        StartCoroutine(PopSingle(slotRT, Vector2.zero, Vector2.zero));
    }

    // ── 다중 연출 ────────────────────────────────────────────────────

    void ShowMulti(List<(string id, int count)> results)
    {
        if (singleSlotAnchor) singleSlotAnchor.gameObject.SetActive(false);
        if (resultContent)    resultContent.gameObject.SetActive(true);

        StartCoroutine(PopMulti(results));
    }

    // ── 코루틴 ───────────────────────────────────────────────────────

    IEnumerator PopSingle(RectTransform rt, Vector2 from, Vector2 to)
    {
        rt.anchoredPosition = from;
        rt.localScale       = Vector3.zero;

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float p = Mathf.Clamp01(t / popDuration);
            rt.localScale       = Vector3.one * BounceOut(p);
            rt.anchoredPosition = Vector2.Lerp(from, to, p);
            yield return null;
        }
        rt.localScale       = Vector3.one;
        rt.anchoredPosition = to;
    }

    IEnumerator PopMulti(List<(string id, int count)> results)
    {
        if (slotPrefab == null || resultContent == null) yield break;

        var grid = resultContent.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.cellSize = new Vector2(170f, 200f);

        var slots = new List<RectTransform>();
        foreach (var (id, cnt) in results)
        {
            var slot = Instantiate(slotPrefab, resultContent);
            slot.Bind(id, null, cnt > 1 ? cnt : (int?)null);
            var rt = slot.GetComponent<RectTransform>();
            rt.localScale = Vector3.zero;
            slots.Add(rt);
        }

        Canvas.ForceUpdateCanvases();

        foreach (var rt in slots)
        {
            Vector2 targetPos = rt.anchoredPosition;
            StartCoroutine(PopSingle(rt, Vector2.zero, targetPos));
            yield return new WaitForSecondsRealtime(multiDelay);
        }
    }

    // ── 닫기 ────────────────────────────────────────────────────────

    public void Close()
    {
        StopAllCoroutines();
        ClearSlots();
        if (_originalParent != null)
            transform.SetParent(_originalParent, false);
        gameObject.SetActive(false);
    }

    // ── 내부 유틸 ────────────────────────────────────────────────────

    void ClearSlots()
    {
        if (resultContent != null)
            foreach (Transform child in resultContent)
                Destroy(child.gameObject);
        if (singleSlotAnchor != null)
            foreach (Transform child in singleSlotAnchor)
                Destroy(child.gameObject);
    }

    /// 바운스 이징 (0→1.4→1)
    static float BounceOut(float t)
    {
        if (t < 0.7f) return Mathf.Lerp(0f, 1.4f, t / 0.7f);
        return Mathf.Lerp(1.4f, 1f, (t - 0.7f) / 0.3f);
    }
}
