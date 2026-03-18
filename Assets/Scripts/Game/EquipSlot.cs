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
    [SerializeField] private Image           frameImg;
    [SerializeField] private Image           iconImg;
    [SerializeField] private TextMeshProUGUI starText;

    public int SlotIndex { get; set; }

    private static GameObject ghost;

    private Canvas       rootCanvas;
    private UpgradePanel panel;
    private bool         dragStarted;

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>();
        rootCanvas = c != null ? c.rootCanvas : null;
    }

    public bool IsInitialized { get; private set; }
    public void Init(UpgradePanel upgradePanel) { panel = upgradePanel; IsInitialized = true; }

    // ────────────────────────────────────────────────────────────────
    // 슬롯 갱신
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData data, bool selected = false)
    {
        bool has = data != null;
        if (iconImg)  iconImg.enabled = has;
        if (frameImg) frameImg.color  = has ? WeaponGradeColors.Get(data.grade) : Color.white;
        if (has && iconImg && data.icon) iconImg.sprite = data.icon;
        if (starText) starText.text   = has ? new string('★', data.star) : string.Empty;
        transform.localScale = Vector3.one * (has && selected ? 1.15f : 1f);
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
        CreateGhost(eventData.position);
        Debug.Log($"[EquipSlot] OnBeginDrag slot={SlotIndex}");
    }

    public void OnDrag(PointerEventData eventData) => MoveGhost(eventData.position);

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragStarted)
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
                    break;
                }
            }
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
        if (ghost != null) { Destroy(ghost); ghost = null; }
    }
}
