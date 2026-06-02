using System.Collections.Generic;
using Coffee.UIExtensions;
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
    [SerializeField] private Image frameImg;
    [SerializeField] private Image iconImg;
    [SerializeField] private Image weaponTypeImg;
    [SerializeField] private List<GameObject> starObjects; // 0=1성, 1=2성, 2=3성, 3=4성
    [SerializeField] private GameObject      starGlowObj;
    [SerializeField] private UIShiny         mergeShiny;
    [SerializeField] private GameObject      grandMergeNotiObj; // 3★+3★ 수동 합성 가능 노티

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
    public void PlayMergeShiny()
    {
        if (mergeShiny == null) return;
        mergeShiny.enabled = true;
        mergeShiny.Play();
    }

    public Image IconImage => iconImg;

    /// <summary>결과 무기가 Destroy된 경우를 대비해 사전 캡처된 값으로 슬롯을 표시합니다.</summary>
    public void RefreshCaptured(Sprite icon, WeaponGrade grade, int star)
    {
        if (iconImg) { iconImg.enabled = icon != null; if (icon != null) iconImg.sprite = icon; }
        if (weaponTypeImg) weaponTypeImg.enabled = false;
        SetStarObject(starObjects, star);
        if (frameImg)
        {
            var gradeSprite = WeaponVisualConfig.Shared?.GetGradeSprite(grade);
            if (gradeSprite != null) { frameImg.sprite = gradeSprite; frameImg.type = Image.Type.Simple; frameImg.color = Color.white; }
        }
        if (starGlowObj) starGlowObj.SetActive(star >= 3);
        transform.localScale = Vector3.one;
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

        starGlowObj?.SetActive(has && data.star >= 3);

        if (grandMergeNotiObj != null)
        {
            bool showNoti = has && data.star >= 3 &&
                (PlayerInventory.Shared?.CountAll(data.weaponName, 3) ?? 0) >= 2;
            grandMergeNotiObj.SetActive(showNoti);
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
                var inv = PlayerInventory.Shared;
                var equipTarget = hit.gameObject.GetComponentInParent<EquipSlot>();
                if (equipTarget != null && equipTarget != this && equipTarget.IsInitialized)
                {
                    var srcW = (inv != null && SlotIndex < inv.Equipped.Count) ? inv.Equipped[SlotIndex] : null;
                    var dstW = (inv != null && equipTarget.SlotIndex < inv.Equipped.Count) ? inv.Equipped[equipTarget.SlotIndex] : null;
                    if (srcW != null && dstW != null && srcW.star >= 3 && dstW.star >= 3 && srcW.weaponName == dstW.weaponName)
                    {
                        Debug.Log($"[EquipSlot] GrandMerge equip{SlotIndex} + equip{equipTarget.SlotIndex}");
                        UpgradeManager.Shared?.TryGrandMerge(srcW.weaponName);
                    }
                    else
                    {
                        Debug.Log($"[EquipSlot] SwapEquip {SlotIndex} → {equipTarget.SlotIndex}");
                        inv?.SwapEquipToEquip(SlotIndex, equipTarget.SlotIndex);
                    }
                    break;
                }
                var invenTarget = hit.gameObject.GetComponentInParent<InvenSlot>();
                if (invenTarget != null && invenTarget.IsInitialized)
                {
                    var srcW = (inv != null && SlotIndex < inv.Equipped.Count) ? inv.Equipped[SlotIndex] : null;
                    var dstW = (inv != null && invenTarget.SlotIndex < inv.Inventory.Count) ? inv.Inventory[invenTarget.SlotIndex] : null;
                    if (srcW != null && dstW != null && srcW.star >= 3 && dstW.star >= 3 && srcW.weaponName == dstW.weaponName)
                    {
                        Debug.Log($"[EquipSlot] GrandMerge equip{SlotIndex} + inven{invenTarget.SlotIndex}");
                        UpgradeManager.Shared?.TryGrandMerge(srcW.weaponName);
                    }
                    else
                    {
                        Debug.Log($"[EquipSlot] MoveEquipToInven {SlotIndex} → inven{invenTarget.SlotIndex}");
                        inv?.MoveEquipToInven(SlotIndex, invenTarget.SlotIndex);
                        UpgradeManager.Shared?.TryAutoMergeAll();
                    }
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