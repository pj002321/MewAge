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
    IBeginDragHandler, IDragHandler, IEndDragHandler, IDropHandler
{
    [SerializeField] private Image frameImg;
    [SerializeField] private Image iconImg;

    public int SlotIndex { get; set; }

    // ── 드래그 공유 상태 (정적) ─────────────────────────────────────
    public static EquipSlot Dragging { get; private set; }
    private static GameObject ghost;

    private Canvas       rootCanvas;
    private UpgradePanel panel;
    private bool         dragStarted;

    private void Awake()
    {
        rootCanvas = GetComponentInParent<Canvas>();
    }

    public void Init(UpgradePanel upgradePanel) => panel = upgradePanel;

    // ────────────────────────────────────────────────────────────────
    // 슬롯 갱신
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData data, bool selected = false)
    {
        bool has = data != null;
        if (iconImg)  iconImg.enabled = has;
        if (frameImg) frameImg.color  = has ? WeaponGradeColors.Get(data.grade) : Color.white;
        if (has && iconImg && data.icon) iconImg.sprite = data.icon;
        transform.localScale = Vector3.one * (has && selected ? 1.15f : 1f);
    }

    // ────────────────────────────────────────────────────────────────
    // 클릭 → 패널에 알림
    // ────────────────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData eventData) { } // EventSystem에 pointer down 등록용

    public void OnPointerUp(PointerEventData eventData)
    {
        Debug.Log($"[EquipSlot] OnPointerUp slot={SlotIndex}, dragStarted={dragStarted}, panel={(panel != null ? "OK" : "NULL")}");
        if (dragStarted) return; // 드래그 중이었으면 클릭 무시
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
        Dragging = this;
        CreateGhost(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)    => MoveGhost(eventData.position);
    public void OnEndDrag(PointerEventData eventData) { dragStarted = false; Dragging = null; DestroyGhost(); }

    // ────────────────────────────────────────────────────────────────
    // 드롭 (장착 슬롯 → 장착 슬롯 교체)
    // ────────────────────────────────────────────────────────────────
    public void OnDrop(PointerEventData eventData)
    {
        if (Dragging == null || Dragging == this) return;
        PlayerInventory.Shared?.SwapEquipToEquip(Dragging.SlotIndex, SlotIndex);
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
        if (ghost != null) { Destroy(ghost); ghost = null; }
    }
}
