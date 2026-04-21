using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 상점 패널.
/// 단일 세로 스크롤: 패키지 → 일일상점 → 장비뽑기 → 골드 → 보석
/// </summary>
public class HomeShopPanel : MonoBehaviour
{
    [Header("가챠 결과 팝업")]
    public GachaResultPopup gachaPopup;

    [Header("섹션 스크롤")]
    [SerializeField] private ScrollRect   scrollRect;
    [SerializeField] private RectTransform gemSection;
    [SerializeField] private RectTransform goldSection;

    bool         _resultShown;
    EquipBoxType _pendingBox;
    int          _pendingCount = 1;

    // ── 뽑기 흐름 ───────────────────────────────────────────────────

    /// <summary>단일 뽑기 버튼 클릭 시 호출.</summary>
    public void TryDraw(int gemCost, EquipBoxType boxType = EquipBoxType.Normal)
        => TryDrawMulti(gemCost, 1, boxType);

    /// <summary>다중 뽑기 버튼 클릭 시 호출. count=10 등.</summary>
    public void TryDrawMulti(int gemCost, int count, EquipBoxType boxType = EquipBoxType.Normal)
    {
        if (gachaPopup == null) { Debug.LogWarning("[Shop] gachaPopup 참조 없음"); return; }
        _resultShown  = false;
        _pendingBox   = boxType;
        _pendingCount = count;

        MovePopupToRootCanvas();

        gachaPopup.OnDrawRequested  = null;
        gachaPopup.OnDrawRequested += OnGachaDrawPressed;
        gachaPopup.OpenForDraw(gemCost);
    }

    void MovePopupToRootCanvas()
    {
        Canvas root = null;
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.transform.parent == null || c.transform.parent.GetComponent<Canvas>() == null)
                if (root == null || c.sortingOrder >= root.sortingOrder)
                    root = c;

        if (root == null) return;

        gachaPopup.transform.SetParent(root.transform, false);
        var rt       = gachaPopup.GetComponent<UnityEngine.RectTransform>();
        rt.anchorMin = UnityEngine.Vector2.zero;
        rt.anchorMax = UnityEngine.Vector2.one;
        rt.offsetMin = rt.offsetMax = UnityEngine.Vector2.zero;
    }

    void OnGachaDrawPressed()
    {
        if (_resultShown) return;
        _resultShown = true;

        if (EquipmentInventory.Shared == null)
            MonoBehaviourSingleton<EquipmentInventory>.Create("EquipmentInventory");

        if (_pendingCount <= 1)
        {
            var equip = EquipmentGacha.Draw(_pendingBox);
            if (equip != null)
                Debug.Log($"[Shop] 뽑기: {equip.equipId} | {equip.itemName} | {equip.grade} | {equip.StatDescription()}");
            else
                Debug.LogWarning("[Shop] 뽑기 결과: null (DB 확인 필요)");

            EquipmentInventory.Shared?.TryAdd(equip);
            Debug.Log($"[Shop] 인벤토리 보관함 수량: {EquipmentInventory.Shared?.Bag.Count}");
            gachaPopup.ShowResult(equip);
        }
        else
        {
            var results = EquipmentGacha.DrawMulti(_pendingCount, _pendingBox);
            foreach (var equip in results)
            {
                if (equip == null) continue;
                Debug.Log($"[Shop] 뽑기: {equip.equipId} | {equip.itemName} | {equip.grade} | {equip.StatDescription()}");
                EquipmentInventory.Shared?.TryAdd(equip);
            }
            Debug.Log($"[Shop] 인벤토리 보관함 수량: {EquipmentInventory.Shared?.Bag.Count}");
            gachaPopup.ShowMultiResult(results);
        }
    }

    // ── 섹션 이동 ───────────────────────────────────────────────────

    public void ScrollToGem()  => StartCoroutine(ScrollToSection(gemSection));
    public void ScrollToGold() => StartCoroutine(ScrollToSection(goldSection));

    private IEnumerator ScrollToSection(RectTransform section)
    {
        if (scrollRect == null || section == null) yield break;

        // 패널이 방금 활성화됐을 수 있어 한 프레임 대기
        yield return null;

        Canvas.ForceUpdateCanvases();

        var content  = scrollRect.content;
        var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();

        // 섹션의 content 기준 로컬 Y 위치
        Vector2 sectionLocal = (Vector2)content.InverseTransformPoint(section.position);
        float   contentH     = content.rect.height;
        float   viewportH    = viewport.rect.height;

        float targetY = Mathf.Clamp01((-sectionLocal.y) / (contentH - viewportH));
        scrollRect.verticalNormalizedPosition = 1f - targetY;
    }

    // ── IAP / 재화 구매 ─────────────────────────────────────────────

    public void PurchaseGems(string productId)
    {
        Debug.Log($"[Shop] 보석 구매: {productId}");
    }

    public void PurchaseGold(int goldAmount)
    {
        Debug.Log($"[Shop] 골드 구매: {goldAmount}");
    }

    public void PurchaseDailyItem(int slot)
    {
        Debug.Log($"[Shop] 일일상점 아이템: slot {slot}");
    }
}
