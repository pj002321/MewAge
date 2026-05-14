using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 장착 슬롯 하나.
/// - 드래그 앤 드롭으로 다른 장착/인벤 슬롯과 교체
/// - 클릭 시 UpgradePanel에 알려 해제 패널 표시
/// </summary>
public class EquipSlot : MonoBehaviour,
    IPointerDownHandler, IPointerUpHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image synergyhilightImg;
    [SerializeField] private Image frameImg;
    [SerializeField] private Image iconImg;
    [SerializeField] private Image weaponTypeImg;
    [SerializeField] private List<GameObject> starObjects; // 0=1성, 1=2성, 2=3성, 3=4성

    public int SlotIndex { get; set; }

    private static GameObject ghost;

    private Canvas       rootCanvas;
    private UpgradePanel panel;
    private bool         dragStarted;

    // 반짝임 효과 (3성=골드, 4성=레인보우)
    private Coroutine _shimmerCoroutine;
    private int       _shimmerStar;

    // 하이라이트 효과
    private Coroutine _highlightCoroutine;
    private Vector3   _highlightOriginalScale;

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>();
        rootCanvas = c != null ? c.rootCanvas : null;
        if (synergyhilightImg)
        {
            _highlightOriginalScale = synergyhilightImg.transform.localScale;
            synergyhilightImg.enabled = false;
        }
    }

    public bool IsInitialized { get; private set; }
    public void Init(UpgradePanel upgradePanel) { panel = upgradePanel; IsInitialized = true; }

    private void OnDisable()
    {
        StopShimmer();
        StopHighlight();
    }

    // ────────────────────────────────────────────────────────────────
    // 하이라이트
    // ────────────────────────────────────────────────────────────────
    public void ShowHighlight()
    {
        if (synergyhilightImg == null) return;
        StopHighlight();
        synergyhilightImg.enabled              = true;
        synergyhilightImg.color                = new Color(1f, 1f, 1f, 1f);
        synergyhilightImg.transform.localScale = _highlightOriginalScale;
    }

    public void HideHighlight()
    {
        StopHighlight();
        if (synergyhilightImg) synergyhilightImg.enabled = false;
    }

    /// <summary>장착 확정 시 스케일업 + 페이드아웃 후 비활성화</summary>
    public void PlayEquipEffect()
    {
        if (synergyhilightImg == null) return;
        StopHighlight();
        _highlightCoroutine = StartCoroutine(EquipEffectRoutine());
    }

    private void StopHighlight()
    {
        if (_highlightCoroutine != null) { StopCoroutine(_highlightCoroutine); _highlightCoroutine = null; }
    }

    private System.Collections.IEnumerator EquipEffectRoutine()
    {
        synergyhilightImg.enabled = true;
        synergyhilightImg.color   = new Color(1f, 1f, 1f, 1f);
        var t = synergyhilightImg.transform;
        t.localScale = _highlightOriginalScale;

        const float duration  = 0.45f;
        const float scaleMult = 1.6f;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float f    = elapsed / duration;
            float ease = 1f - (1f - f) * (1f - f); // ease-out quad
            t.localScale            = _highlightOriginalScale * Mathf.LerpUnclamped(1f, scaleMult, ease);
            synergyhilightImg.color = new Color(1f, 1f, 1f, Mathf.Lerp(1f, 0f, ease));
            yield return null;
        }

        synergyhilightImg.enabled = false;
        t.localScale              = _highlightOriginalScale;
        _highlightCoroutine       = null;
    }

    // ────────────────────────────────────────────────────────────────
    // 슬롯 갱신
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData data, bool selected = false)
    {
        bool has = data != null;
        if (iconImg)  iconImg.enabled = has;
        if (has && iconImg && data.icon) iconImg.sprite = data.icon;

        SetStarObject(starObjects, has ? data.star : 0);
        if (has)
            WeaponInfoPanel.SetTypeIcon(weaponTypeImg, data.weaponType, data.grade);
        else if (weaponTypeImg)
            weaponTypeImg.enabled = false;
        transform.localScale = Vector3.one * (has && selected ? 1.15f : 1f);

        if (frameImg)
        {
            var gradeSprite = WeaponVisualConfig.Shared?.GetGradeSprite(has ? data.grade : WeaponGrade.Common);
            if (gradeSprite != null)
            {
                frameImg.sprite = gradeSprite;
                frameImg.type   = UnityEngine.UI.Image.Type.Simple;
                frameImg.color  = Color.white;
            }
        }

        // 3성 이상이면 shimmer 시작
        bool useShimmer = has && data.star >= 3 && frameImg != null;
        int  targetStar = useShimmer ? data.star : 0;

        // 성 수가 바뀌었거나, 반짝임이 켜져야 하는데 코루틴이 없는 경우 재시작
        bool needsUpdate = (targetStar != _shimmerStar) || (useShimmer && _shimmerCoroutine == null);
        if (needsUpdate)
        {
            StopShimmer();
            _shimmerStar = targetStar;
            if (useShimmer)
                _shimmerCoroutine = StartCoroutine(ShimmerRoutine(targetStar));
        }
    }

    private void StopShimmer()
    {
        if (_shimmerCoroutine != null) { StopCoroutine(_shimmerCoroutine); _shimmerCoroutine = null; }
        _shimmerStar = 0;
    }

    /// <summary>3성: 황금 펄스 / 4성 이상: 레인보우 사이클</summary>
    private System.Collections.IEnumerator ShimmerRoutine(int star)
    {
        while (true)
        {
            if (frameImg == null) yield break;
            if (star >= 4)
            {
                // 레인보우: hue를 시간에 따라 순환
                float hue = Mathf.Repeat(Time.unscaledTime * 0.4f, 1f);
                frameImg.color = Color.HSVToRGB(hue, 0.85f, 1f);
            }
            else
            {
                // 3성 황금 펄스: 진한 금 ↔ 밝은 금
                float t = (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.5f;
                frameImg.color = Color.Lerp(
                    new Color(1f, 0.55f, 0f),    // 진한 금
                    new Color(1f, 0.92f, 0.3f),  // 밝은 금
                    t);
            }
            yield return null;
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 클릭 → 패널에 알림
    // ────────────────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData eventData) { }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (dragStarted) return;
        panel?.OnEquipSlotClicked(SlotIndex);
    }

    // ────────────────────────────────────────────────────────────────
    // 드래그
    // ────────────────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null || SlotIndex >= inv.Equipped.Count || inv.Equipped[SlotIndex] == null)
            return;

        dragStarted = true;
        panel?.NotifyDragBegin(inv.Equipped[SlotIndex]);
        CreateGhost(eventData.position);
        Debug.Log($"[EquipSlot] OnBeginDrag slot={SlotIndex}");
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveGhost(eventData.position);
        if (dragStarted) panel?.NotifyDragHover(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragStarted)
        {
            bool sold = panel?.NotifyDragEnd(eventData.position) ?? false;
            if (sold)
            {
                panel?.SellDraggedEquip(SlotIndex);
            }
            else
            {
            // RaycastAll로 포인터 아래 타겟을 직접 탐색
            var results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            Debug.Log($"[EquipSlot] OnEndDrag slot={SlotIndex}, hits={results.Count}");

            foreach (var hit in results)
            {
                var equipTarget = hit.gameObject.GetComponentInParent<EquipSlot>();
                if (equipTarget != null && equipTarget != this && equipTarget.IsInitialized)
                {
                    Debug.Log($"[EquipSlot] SwapEquip {SlotIndex} → {equipTarget.SlotIndex}");
                    PlayerInventory.Shared?.SwapEquipToEquip(SlotIndex, equipTarget.SlotIndex);
                    break;
                }
                var invenTarget = hit.gameObject.GetComponentInParent<InvenSlot>();
                if (invenTarget != null && invenTarget.IsInitialized)
                {
                    Debug.Log($"[EquipSlot] MoveEquipToInven {SlotIndex} → inven{invenTarget.SlotIndex}");
                    PlayerInventory.Shared?.MoveEquipToInven(SlotIndex, invenTarget.SlotIndex);
                    UpgradeManager.Shared?.TryAutoMergeAll();
                    break;
                }
            }
            } // end else (not sold)
        }
        else
        {
            panel?.NotifyDragEnd(eventData.position);
        }

        dragStarted = false;
        DestroyGhost();
    }

    // ────────────────────────────────────────────────────────────────
    // Ghost 유틸
    // ────────────────────────────────────────────────────────────────
    private void CreateGhost(Vector2 screenPos)
    {
        if (iconImg == null || !iconImg.enabled || rootCanvas == null) return;

        ghost = new GameObject("DragGhost",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        ghost.transform.SetParent(rootCanvas.transform, false);
        ghost.transform.SetAsLastSibling();

        var img = ghost.GetComponent<Image>();
        img.sprite = iconImg.sprite;
        img.raycastTarget = false;

        var cg = ghost.GetComponent<CanvasGroup>();
        cg.alpha = 0.75f;
        cg.blocksRaycasts = false;

        ghost.GetComponent<RectTransform>().sizeDelta = ((RectTransform)transform).sizeDelta;
        MoveGhost(screenPos);
    }

    private void MoveGhost(Vector2 screenPos)
    {
        if (ghost == null || rootCanvas == null) return;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)rootCanvas.transform, screenPos,
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : rootCanvas.worldCamera,
            out Vector2 local);
        ghost.GetComponent<RectTransform>().anchoredPosition = local;
    }

    private static void DestroyGhost()
    {
        if (ghost != null) { ghost.SetActive(false); Destroy(ghost); ghost = null; }
    }

    private static void SetStarObject(List<GameObject> objs, int star)
    {
        if (objs == null) return;
        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null) objs[i].SetActive(i == star - 1 && star > 0);
    }
}