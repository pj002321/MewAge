using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

/// <summary>
/// 뽑기 결과 팝업.
/// 흐름: 팝업 열림 → 현재 애니메이션 클립 종료 대기 → bComplete = true → 아이템 팝인
/// </summary>
public class GachaResultPopup : MonoBehaviour
{
    [Header("다중 결과 그리드")]
    public RectTransform     multiResultContent;
    public EquipmentSlotView slotPrefab;

    [Header("단일 결과 (중앙 표시용)")]
    public RectTransform     singleSlotAnchor;

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

    [Header("보물상자 Animator")]
    [SerializeField] Animator chestAnimator;

    [Header("박스 타입별 비주얼 설정")]
    public Image resultBgImage;

    [Header("애니메이션")]
    public float popDuration = 0.28f;
    public float multiDelay  = 0.12f;

    public Action OnDrawRequested;
    public Action OnDrawReset;

    // 뽑기 결과 임시 저장 (애니메이션 후 표시)
    EquipmentData       _pendingResult;
    List<EquipmentData> _pendingMultiResult;

    // 한 번 더 뽑기용 저장
    int          _currentGemCost;
    GachaBoxType _currentBoxType;

    // ── 초기화 ─────────────────────────────────────────────────────────

    void Awake()
    {
        closeHitArea?.onClick.AddListener(Close);
        drawButton?.onClick.AddListener(OnDrawButtonClicked);
        gameObject.SetActive(false);
    }

    void OnDrawButtonClicked()
    {
        OpenForDraw(_currentGemCost, _currentBoxType);
    }

    // ── 공개 API ───────────────────────────────────────────────────────

    /// <summary>팝업을 열고 뽑기를 시작합니다.</summary>
    public void OpenForDraw(int gemCost, GachaBoxType boxType)
    {
        StopAllCoroutines();
        ClearSlots();

        _pendingResult      = null;
        _pendingMultiResult = null;
        _currentGemCost     = gemCost;
        _currentBoxType     = boxType;

        // HomeShopPanel의 중복 방지 플래그 리셋
        OnDrawReset?.Invoke();

        if (drawCostText) drawCostText.text = $"x{gemCost}";

        gameObject.SetActive(true);
        ApplyGachaVisual(boxType);

        // 외부에서 ShowResult / ShowMultiResult 로 결과를 주입받은 뒤
        // 코루틴이 애니메이션 종료 후 표시합니다.
        OnDrawRequested?.Invoke();

        StartCoroutine(WaitAnimThenReveal());
    }

    /// <summary>단일 결과 주입 (데이터만 저장, 표시는 코루틴이 담당)</summary>
    public void ShowResult(EquipmentData equip)
    {
        _pendingResult = equip;
    }

    /// <summary>다중 결과 주입</summary>
    public void ShowMultiResult(List<EquipmentData> results)
    {
        _pendingMultiResult = results;
    }

    public void Close()
    {
        StopAllCoroutines();
        OnDrawRequested = null;
        gameObject.SetActive(false);
    }

    // ── 핵심 코루틴 ───────────────────────────────────────────────────

    /// <summary>
    /// 현재 재생 중인 애니메이션 클립이 끝나면 bComplete = true 후 아이템을 표시합니다.
    /// </summary>
    IEnumerator WaitAnimThenReveal()
    {
        // Animator가 있으면 현재 클립 종료까지 대기
        if (chestAnimator != null)
        {
            // Rebind 직후 한 프레임 대기 (Animator 안정화)
            yield return null;

            var info      = chestAnimator.GetCurrentAnimatorStateInfo(0);
            float clipLen = info.length / Mathf.Max(info.speed, 0.001f);
            float elapsed = info.normalizedTime * clipLen;
            float remaining = Mathf.Max(0f, clipLen - elapsed);

            this.DLog($"[GachaResultPopup] 애니메이션 대기: {remaining:F2}s");

            if (remaining > 0f)
                yield return new WaitForSecondsRealtime(remaining);

            // 현재 클립 종료 → bComplete = true
            chestAnimator.SetBool("bComplete", true);
            SoundManager.Shared?.Play(SoundKey.Gacha_Open);
            this.DLog("[GachaResultPopup] bComplete = true ✅");

            // 전환 대기 (2프레임)
            yield return null;
            yield return null;
        }

        // 아이템 팝인 표시
        if (_pendingResult != null)
            yield return StartCoroutine(RevealSingle(_pendingResult));
        else if (_pendingMultiResult != null)
            yield return StartCoroutine(RevealMulti(_pendingMultiResult));
        else
            this.DLog("[GachaResultPopup] 표시할 결과 없음 (null)");
    }

    // ── 아이템 표시 ───────────────────────────────────────────────────

    IEnumerator RevealSingle(EquipmentData equip)
    {
        if (multiResultContent) multiResultContent.gameObject.SetActive(false);

        var parent = singleSlotAnchor != null ? singleSlotAnchor : (RectTransform)transform;
        var slot   = Instantiate(slotPrefab, parent);
        slot.Bind(equip, null);
        slot.StartSpin();
        SoundManager.Shared?.Play(SoundKey.Gacha_ItemReveal);

        var rt = slot.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot     = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(240f, 270f);

        Vector2 from = chestRect != null
            ? (Vector2)transform.InverseTransformPoint(chestRect.position)
            : Vector2.zero;

        yield return StartCoroutine(PopAnim(rt, from, Vector2.zero));
    }

    IEnumerator RevealMulti(List<EquipmentData> results)
    {
        if (multiResultContent) multiResultContent.gameObject.SetActive(true);

        var grid = multiResultContent?.GetComponent<GridLayoutGroup>();
        if (grid != null) grid.cellSize = new Vector2(160f, 180f);

        Vector2 chestPos = chestRect != null
            ? (Vector2)multiResultContent.InverseTransformPoint(chestRect.position)
            : Vector2.zero;

        var slots = new List<RectTransform>();
        foreach (var equip in results)
        {
            if (equip == null) continue;
            var slot = Instantiate(slotPrefab, multiResultContent);
            slot.Bind(equip, null);
            slot.StartSpin();
            SoundManager.Shared?.Play(SoundKey.Gacha_ItemReveal);
            var rt = slot.GetComponent<RectTransform>();
            rt.localScale = Vector3.zero;
            slots.Add(rt);
        }

        Canvas.ForceUpdateCanvases();

        foreach (var rt in slots)
        {
            Vector2 targetPos   = rt.anchoredPosition;
            rt.anchoredPosition = chestPos;
            StartCoroutine(PopAnim(rt, chestPos, targetPos));
            yield return new WaitForSecondsRealtime(multiDelay);
        }
    }

    IEnumerator PopAnim(RectTransform rt, Vector2 from, Vector2 to)
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

    // ── 유틸 ──────────────────────────────────────────────────────────

    void ApplyGachaVisual(GachaBoxType boxType)
    {
        var cfg = HomeShopVisualConfig.Shared;
        if (cfg == null || !cfg.TryGetEntry(boxType, out var entry)) return;

        if (chestAnimator != null && entry.animatorController != null)
        {
            chestAnimator.runtimeAnimatorController = entry.animatorController;
            chestAnimator.Rebind();
            chestAnimator.Update(0f);
            // Rebind 후 bComplete 초기화
            chestAnimator.SetBool("bComplete", false);
        }

        if (resultBgImage != null)
        {
            resultBgImage.sprite  = entry.backgroundImage;
            resultBgImage.enabled = entry.backgroundImage != null;
        }
    }

    void ClearSlots()
    {
        if (multiResultContent != null)
            foreach (Transform child in multiResultContent)
                Destroy(child.gameObject);

        if (singleSlotAnchor != null)
            foreach (Transform child in singleSlotAnchor)
                Destroy(child.gameObject);

        if (gradeText)    gradeText.gameObject.SetActive(false);
        if (itemNameText) itemNameText.gameObject.SetActive(false);
        if (itemIcon)     itemIcon.gameObject.SetActive(false);
        if (statsText)    statsText.gameObject.SetActive(false);
    }

    static float BounceOut(float t)
    {
        if (t < 0.7f) return Mathf.Lerp(0f, 1.4f, t / 0.7f);
        return Mathf.Lerp(1.4f, 1f, (t - 0.7f) / 0.3f);
    }
}
