using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 뽑기 결과 팝업 — 전체 화면 오버레이.
/// 단일: 보물상자에서 슬롯 1개가 중앙으로 스케일업 뿅!
/// 다중: 보물상자에서 슬롯들이 차례로 그리드 위치에 뿅뿅뿅!
/// </summary>
public class GachaResultPopup : MonoBehaviour
{
    [Header("다중 결과 그리드")]
    public RectTransform     multiResultContent;  // GridLayoutGroup Content
    public EquipmentSlotView slotPrefab;

    [Header("단일 결과 (중앙 표시용)")]
    public RectTransform     singleSlotAnchor;    // 중앙 고정 위치 (앵커 0.5, 0.5 짜리 빈 RT)

    [Header("보물상자 (애니메이션 출발점)")]
    public RectTransform     chestRect;

    [Header("결과 텍스트")]
    public TextMeshProUGUI gradeText;
    public TextMeshProUGUI itemNameText;
    public Image           itemIcon;
    public TextMeshProUGUI statsText;

    [Header("뽑기 버튼")]
    public Button          drawButton;
    public TextMeshProUGUI drawCostText;

    [Header("닫기")]
    public Button closeHitArea;

    [Header("애니메이션")]
    public float popDuration  = 0.28f;   // 스케일업 시간
    public float multiDelay   = 0.12f;   // 다중 뽑기 슬롯 간격

    public Action OnDrawRequested;

    Animator _chestAnimator;

    void Awake()
    {
        closeHitArea?.onClick.AddListener(Close);
        drawButton?.onClick.AddListener(OnDrawButtonClicked);
        gameObject.SetActive(false);
    }

    void OnDrawButtonClicked()
    {
        if (_chestAnimator != null && _chestAnimator.runtimeAnimatorController != null)
            _chestAnimator.SetBool("bResult", true);
        OnDrawRequested?.Invoke();
    }

    public void OpenForDraw(int gemCost)
    {
        StopAllCoroutines();
        ClearResults();
        if (drawCostText) drawCostText.text = $"x{gemCost}";
        if (chestRect)
        {
            _chestAnimator = chestRect.GetComponentInChildren<Animator>();
            if (_chestAnimator != null && _chestAnimator.runtimeAnimatorController != null) _chestAnimator.SetBool("bResult", false);
        }
        gameObject.SetActive(true);
    }

    /// <summary>단일 뽑기 — 중앙에 슬롯 1개 스케일업.</summary>
    public void ShowResult(EquipmentData equip)
    {
        if (equip == null) return;

        ClearResults();
        if (multiResultContent) multiResultContent.gameObject.SetActive(false);

        // 단일 슬롯을 singleSlotAnchor 아래(또는 팝업 직속)에 생성
        var parent = singleSlotAnchor != null ? singleSlotAnchor : (RectTransform)transform;
        var slot   = Instantiate(slotPrefab, parent);
        slot.Bind(equip, null);
        slot.StartSpin();

        // 보물상자 위치에서 시작해 중앙으로 이동 + 스케일업
        var slotRT = slot.GetComponent<RectTransform>();
        slotRT.anchorMin = slotRT.anchorMax = new Vector2(0.5f, 0.5f);
        slotRT.pivot     = new Vector2(0.5f, 0.5f);
        slotRT.sizeDelta = new Vector2(240f, 270f);

        Vector2 startPos = chestRect != null
            ? (Vector2)transform.InverseTransformPoint(chestRect.position)
            : Vector2.zero;

        StartCoroutine(PopSingle(slotRT, startPos, Vector2.zero));
    }

    /// <summary>다중 뽑기 — 슬롯들이 차례로 뿅뿅뿅.</summary>
    public void ShowMultiResult(List<EquipmentData> results)
    {
        ClearResults();
        if (multiResultContent) multiResultContent.gameObject.SetActive(true);

        StartCoroutine(PopMulti(results));
    }

    // ── 코루틴 ────────────────────────────────────────────────────────

    IEnumerator PopSingle(RectTransform rt, Vector2 from, Vector2 to)
    {
        rt.anchoredPosition = from;
        rt.localScale       = Vector3.zero;

        float t = 0f;
        while (t < popDuration)
        {
            t += Time.unscaledDeltaTime;
            float p     = Mathf.Clamp01(t / popDuration);
            float scale = BounceOut(p);
            rt.localScale       = Vector3.one * scale;
            rt.anchoredPosition = Vector2.Lerp(from, to, p);
            yield return null;
        }
        rt.localScale       = Vector3.one;
        rt.anchoredPosition = to;
    }

    IEnumerator PopMulti(List<EquipmentData> results)
    {
        if (slotPrefab == null || multiResultContent == null) yield break;

        var grid = multiResultContent.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.cellSize = new Vector2(160f, 180f);

        Vector2 chestPos = chestRect != null
            ? (Vector2)multiResultContent.InverseTransformPoint(chestRect.position)
            : Vector2.zero;

        var slots = new List<RectTransform>();
        foreach (var equip in results)
        {
            if (equip == null) continue;
            var slot   = Instantiate(slotPrefab, multiResultContent);
            slot.Bind(equip, null);
            slot.StartSpin();
            var rt     = slot.GetComponent<RectTransform>();
            rt.localScale = Vector3.zero;
            slots.Add(rt);
        }

        // GridLayoutGroup 레이아웃 강제 적용 후 각 슬롯 위치 얻기
        Canvas.ForceUpdateCanvases();

        foreach (var rt in slots)
        {
            Vector2 targetPos = rt.anchoredPosition;
            rt.anchoredPosition = chestPos;
            StartCoroutine(PopSingle(rt, chestPos, targetPos));
            yield return new WaitForSecondsRealtime(multiDelay);
        }
    }

    /// 바운스 이징 (0→1.4→1)
    static float BounceOut(float t)
    {
        if (t < 0.7f)  return Mathf.Lerp(0f, 1.4f, t / 0.7f);
        return Mathf.Lerp(1.4f, 1f, (t - 0.7f) / 0.3f);
    }

    void ClearResults()
    {
        StopAllCoroutines();
        if (multiResultContent != null)
            foreach (Transform child in multiResultContent)
                Destroy(child.gameObject);

        // singleSlotAnchor 자식 정리
        if (singleSlotAnchor != null)
            foreach (Transform child in singleSlotAnchor)
                Destroy(child.gameObject);

        if (gradeText)    gradeText.gameObject.SetActive(false);
        if (itemNameText) itemNameText.gameObject.SetActive(false);
        if (itemIcon)     itemIcon.gameObject.SetActive(false);
        if (statsText)    statsText.gameObject.SetActive(false);
    }

    public void Close()
    {
        StopAllCoroutines();
        OnDrawRequested = null;
        gameObject.SetActive(false);
    }
}
