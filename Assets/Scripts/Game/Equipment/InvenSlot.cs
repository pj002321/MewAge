using System.Collections.Generic;
using Coffee.UIExtensions;
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
    [SerializeField] private Image            frameImg;
    [SerializeField] private Image            iconImg;
    [SerializeField] private Image            weaponTypeImg;
    [SerializeField] private List<GameObject> starObjects; // 0=1성, 1=2성, 2=3성, 3=4성
    [SerializeField] private UIShiny          mergeShiny;
    [SerializeField] private GameObject       grandMergeNotiObj; // 3★+3★ 수동 합성 가능 노티

    [Header("선택 확대")]
    [SerializeField] private float selectedScale = 1.2f;
    [SerializeField] private float normalScale   = 1.0f;

    public int SlotIndex { get; set; }

    private static GameObject ghost;

    private Canvas       rootCanvas;
    private UpgradePanel panel;
    private bool         dragStarted;
    private EquipSlot    _hoveredEquipSlot;

    private bool       _mergeHighlight;
    private WeaponData _currentData;
    private static readonly Color MergeHighlightColor = new Color(1f, 0.85f, 0.1f);

    public void SetMergeHighlight(bool active)
    {
        _mergeHighlight = active;
        if (frameImg != null)
            frameImg.color = active && _currentData != null ? MergeHighlightColor : Color.white;
    }

    private void Awake()
    {
        var c = GetComponentInParent<Canvas>();
        rootCanvas = c != null ? c.rootCanvas : null;
    }

    public bool IsInitialized { get; private set; }
    public void Init(UpgradePanel upgradePanel) { panel = upgradePanel; IsInitialized = true; }

    public void PlayMergeShiny()
    {
        if (mergeShiny == null) return;
        mergeShiny.enabled = true;
        mergeShiny.Play();
    }

    public Image IconImage => iconImg;

    /// <summary>결과 무기가 Destroy된 경우를 대비해 사전 캡처된 값으로 슬롯을 표시합니다.</summary>
    public void RefreshCaptured(Sprite icon, WeaponGrade grade, int star, bool selected = false)
    {
        if (frameImg)
        {
            var gradeSprite = WeaponVisualConfig.Shared?.GetGradeSprite(grade);
            if (gradeSprite != null) { frameImg.sprite = gradeSprite; frameImg.type = Image.Type.Simple; frameImg.color = Color.white; }
        }
        if (iconImg) { iconImg.enabled = icon != null; if (icon != null) { iconImg.sprite = icon; iconImg.SetNativeSize(); } }
        if (weaponTypeImg) weaponTypeImg.enabled = false;
        SetStarObject(starObjects, star);
        transform.localScale = Vector3.one * (selected ? selectedScale : normalScale);
    }

    /// <summary>합성 슬램 연출 중 슬롯을 빈 상태로 표시합니다.</summary>
    public void HideContentImages()
    {
        if (frameImg)
        {
            var emptySprite = WeaponVisualConfig.Shared?.GetGradeSprite(WeaponGrade.Common);
            if (emptySprite != null) frameImg.sprite = emptySprite;
        }
        if (iconImg)           iconImg.enabled        = false;
        if (weaponTypeImg)     weaponTypeImg.enabled  = false;
        if (grandMergeNotiObj) grandMergeNotiObj.SetActive(false);
        if (starObjects != null)
            foreach (var s in starObjects) if (s != null) s.SetActive(false);
    }

    // ────────────────────────────────────────────────────────────────
    // 슬롯 갱신
    // ────────────────────────────────────────────────────────────────
    public void Refresh(WeaponData data, bool selected)
    {
        _currentData = data;
        bool has = data != null;
        if (iconImg)  iconImg.enabled = has;
        if (frameImg)
        {
            var gradeSprite = WeaponVisualConfig.Shared?.GetGradeSprite(
                has ? data.grade : WeaponGrade.Common);
            if (gradeSprite != null)
            {
                frameImg.sprite = gradeSprite;
                frameImg.type   = UnityEngine.UI.Image.Type.Simple;
                frameImg.color  = _mergeHighlight && has ? MergeHighlightColor : Color.white;
            }
        }
        if (has && iconImg && data.icon) { iconImg.sprite = data.icon; iconImg.SetNativeSize(); }
        ApplyWeaponOverlays(has ? data : null);
        SetStarObject(starObjects, has ? data.star : 0);

        float s = (has && selected) ? selectedScale : normalScale;
        transform.localScale = Vector3.one * s;

        if (grandMergeNotiObj != null)
        {
            bool showNoti = has && data.star >= 3 &&
                (PlayerInventory.Shared?.CountAll(data.weaponName, 3) ?? 0) >= 2;
            grandMergeNotiObj.SetActive(showNoti);
        }
    }

    private static void SetStarObject(List<GameObject> objs, int star)
    {
        if (objs == null) return;
        for (int i = 0; i < objs.Count; i++)
            if (objs[i] != null) objs[i].SetActive(i == star - 1 && star > 0);
    }

    private void ApplyWeaponOverlays(WeaponData data)
    {
        var cfg = WeaponVisualConfig.Shared;

        if (weaponTypeImg)
        {
            var sprite = data != null ? cfg?.GetTypeSprite(data.weaponType) : null;
            weaponTypeImg.sprite  = sprite;
            weaponTypeImg.enabled = sprite != null;
            if (sprite != null) { weaponTypeImg.color = WeaponGradeColors.Get(data.grade); weaponTypeImg.SetNativeSize(); }
        }
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
            return;
        }
        dragStarted = true;
        panel?.NotifyDragBegin(inv.Inventory[SlotIndex]);
        CreateGhost(eventData.position);
    }

    public void OnDrag(PointerEventData eventData)
    {
        MoveGhost(eventData.position);
        if (!dragStarted) return;

        panel?.NotifyDragHover(eventData.position);

        // EquipSlot 호버 하이라이트 (RectTransform bounds 기준, raycastTarget 무관)
        EquipSlot hovered = panel?.GetEquipSlotAtScreenPos(eventData.position);
        if (hovered != _hoveredEquipSlot)
        {
            _hoveredEquipSlot = hovered;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (dragStarted)
        {
            bool sold = panel?.NotifyDragEnd(eventData.position) ?? false;
            _hoveredEquipSlot = null;
            if (sold)
            {
                panel?.SellDraggedInven(SlotIndex);
            }
            else
            {
                var results = new List<RaycastResult>();
                EventSystem.current.RaycastAll(eventData, results);

                foreach (var hit in results)
                {
                    var inv = PlayerInventory.Shared;
                    var equipTarget = hit.gameObject.GetComponentInParent<EquipSlot>();
                    if (equipTarget != null && equipTarget.IsInitialized)
                    {
                        var srcW = (inv != null && SlotIndex < inv.Inventory.Count) ? inv.Inventory[SlotIndex] : null;
                        var dstW = (inv != null && equipTarget.SlotIndex < inv.Equipped.Count) ? inv.Equipped[equipTarget.SlotIndex] : null;
                        if (srcW != null && dstW != null && srcW.star >= 3 && dstW.star >= 3 && srcW.weaponName == dstW.weaponName)
                        {
                            UpgradeManager.Shared?.TryGrandMerge(srcW.weaponName);
                        }
                        else
                        {
                            SoundManager.Shared?.Play(SoundKey.UI_Equip);
                            inv?.MoveInvenToEquip(SlotIndex, equipTarget.SlotIndex);
                        }
                        break;
                    }
                    var invenTarget = hit.gameObject.GetComponentInParent<InvenSlot>();
                    if (invenTarget != null && invenTarget != this && invenTarget.IsInitialized)
                    {
                        var srcW = (inv != null && SlotIndex < inv.Inventory.Count) ? inv.Inventory[SlotIndex] : null;
                        var dstW = (inv != null && invenTarget.SlotIndex < inv.Inventory.Count) ? inv.Inventory[invenTarget.SlotIndex] : null;
                        if (srcW != null && dstW != null && srcW.star >= 3 && dstW.star >= 3 && srcW.weaponName == dstW.weaponName)
                        {
                            Debug.Log($"[InvenSlot] GrandMerge inven{SlotIndex} + inven{invenTarget.SlotIndex}");
                            UpgradeManager.Shared?.TryGrandMerge(srcW.weaponName);
                        }
                        else
                        {
                            Debug.Log($"[InvenSlot] SwapInven {SlotIndex} ↔ {invenTarget.SlotIndex}");
                            inv?.SwapInvenToInven(SlotIndex, invenTarget.SlotIndex);
                        }
                        break;
                    }
                }
                _hoveredEquipSlot = null;
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
        if (ghost != null) { ghost.SetActive(false); Destroy(ghost); ghost = null; }
    }
}
