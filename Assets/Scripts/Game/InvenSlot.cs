using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 인벤토리 슬롯 하나. 클릭 시 확대 하이라이트 + 옆에 장착/팔기 패널 표시.
/// 드래그로 EquipSlot과 교체 가능.
/// </summary>
public class InvenSlot : MonoBehaviour,
    IPointerDownHandler, IPointerClickHandler,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] private Image           frameImg;
    [SerializeField] private Image           iconImg;
    [SerializeField] private TextMeshProUGUI starText;

    [Header("선택 확대")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float normalScale   = 1.0f;

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
    public void Refresh(WeaponData data, bool selected)
    {
        bool has = data != null;
        if (iconImg)  iconImg.enabled = has;
        if (frameImg)
        {
            if (has)
            {
                var path = "Weapons/grade/" + data.grade.ToString().ToLower();
                var sprite = Resources.Load<Sprite>(path);
                Debug.Log($"[InvenSlot] Loading grade sprite: {path}, result: {sprite != null}");
                if (sprite != null)
                {
                    frameImg.sprite = sprite;
                    frameImg.color  = Color.white;
                }
                else
                {
                    Debug.LogWarning($"[InvenSlot] Grade sprite not found: {path}");
                }
            }
            else
            {
                var path = "Weapons/grade/normal";
                var sprite = Resources.Load<Sprite>(path);
                Debug.Log($"[InvenSlot] Loading normal sprite: {path}, result: {sprite != null}");
                if (sprite != null)
                {
                    frameImg.sprite = sprite;
                    frameImg.color  = Color.white;
                }
                else
                {
                    Debug.LogWarning($"[InvenSlot] Normal sprite not found: {path}");
                }
            }
        }
        if (has && iconImg && data.icon) iconImg.sprite = data.icon;
        if (starText)
        {
            starText.text = has ? new string('★', data.star) : string.Empty;
            starText.gameObject.SetActive(has);
        }

        float s = (has && selected) ? selectedScale : normalScale;
        transform.localScale = Vector3.one * s;
    }

    // ────────────────────────────────────────────────────────────────
    // 클릭 (드래그가 아닌 경우에만)
    // ────────────────────────────────────────────────────────────────
    public void OnPointerDown(PointerEventData eventData)
    {
        Debug.Log($"[InvenSlot] OnPointerDown slot={SlotIndex}");
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (dragStarted) return;
        panel?.OnInvenSlotClicked(SlotIndex, transform);
    }

    // ────────────────────────────────────────────────────────────────
    // 드래그 (인벤 → 장착/인벤 슬롯)
    // ────────────────────────────────────────────────────────────────
    public void OnBeginDrag(PointerEventData eventData)
    {
        Debug.Log($"[InvenSlot] OnBeginDrag called slot={SlotIndex}");
        var inv = PlayerInventory.Shared;
        if (inv == null || SlotIndex >= inv.Inventory.Count || inv.Inventory[SlotIndex] == null)
        {
            Debug.Log($"[InvenSlot] OnBeginDrag CANCELLED slot={SlotIndex}, inv={inv != null}, count={inv?.Inventory.Count}");
            return;
        }
        dragStarted = true;
        panel?.NotifyDragBegin();
        CreateGhost(eventData.position);
        Debug.Log($"[InvenSlot] OnBeginDrag OK slot={SlotIndex}");
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
                panel?.SellDraggedInven(SlotIndex);
            }
            else
            {
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);
                Debug.Log($"[InvenSlot] OnEndDrag slot={SlotIndex}, hits={results.Count}");

                foreach (var hit in results)
                {
                    var equipTarget = hit.gameObject.GetComponentInParent<EquipSlot>();
                    if (equipTarget != null && equipTarget.IsInitialized)
                    {
                        Debug.Log($"[InvenSlot] MoveInvenToEquip inven{SlotIndex} → equip{equipTarget.SlotIndex}");
                        PlayerInventory.Shared?.MoveInvenToEquip(SlotIndex, equipTarget.SlotIndex);
                        break;
                    }
                    var invenTarget = hit.gameObject.GetComponentInParent<InvenSlot>();
                    if (invenTarget != null && invenTarget != this && invenTarget.IsInitialized)
                    {
                        Debug.Log($"[InvenSlot] SwapInven {SlotIndex} ↔ {invenTarget.SlotIndex}");
                        PlayerInventory.Shared?.SwapInvenToInven(SlotIndex, invenTarget.SlotIndex);
                        break;
                    }
                }
            }
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
        if (iconImg == null)    { Debug.LogWarning("[InvenSlot] CreateGhost FAIL: iconImg null");    return; }
        if (!iconImg.enabled)   { Debug.LogWarning("[InvenSlot] CreateGhost FAIL: iconImg disabled"); return; }
        if (rootCanvas == null) { Debug.LogWarning("[InvenSlot] CreateGhost FAIL: rootCanvas null");  return; }

        ghost = new GameObject("DragGhost",
            typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
        ghost.transform.SetParent(rootCanvas.transform, false);
        ghost.transform.SetAsLastSibling();

        var img = ghost.GetComponent<Image>();
        img.sprite        = iconImg.sprite;
        img.raycastTarget = false;

        var cg = ghost.GetComponent<CanvasGroup>();
        cg.alpha          = 0.75f;
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
