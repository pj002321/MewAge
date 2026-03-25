using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 홈 상점 패널.
/// 단일 세로 스크롤: 패키지 → 일일상점 → 장비뽑기 → 골드 → 보석
/// </summary>
public class HomeShopPanel : MonoBehaviour
{
    [Header("가챠 결과 팝업")]
    public GachaResultPopup gachaPopup;

    bool        _resultShown;
    EquipBoxType _pendingBox;

    // ── 뽑기 흐름 ───────────────────────────────────────────────────

    /// <summary>뽑기 버튼(1회 / 10회) 클릭 시 호출.</summary>
    public void TryDraw(int gemCost, EquipBoxType boxType = EquipBoxType.Normal)
    {
        if (gachaPopup == null) { Debug.LogWarning("[Shop] gachaPopup 참조 없음"); return; }
        _resultShown = false;
        _pendingBox  = boxType;

        // 팝업을 루트 Canvas 바로 아래로 이동 → 전체 화면 오버레이
        MovePopupToRootCanvas();

        gachaPopup.OnDrawRequested = null;
        gachaPopup.OnDrawRequested += OnGachaDrawPressed;
        gachaPopup.OpenForDraw(gemCost);
    }

    void MovePopupToRootCanvas()
    {
        // 루트 Canvas 탐색 (가장 상위 Canvas)
        Canvas root = null;
        foreach (var c in FindObjectsOfType<Canvas>())
            if (c.transform.parent == null || c.transform.parent.GetComponent<Canvas>() == null)
                if (root == null || c.sortingOrder >= root.sortingOrder)
                    root = c;

        if (root == null) return;

        gachaPopup.transform.SetParent(root.transform, false);
        var rt       = gachaPopup.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    void OnGachaDrawPressed()
    {
        if (_resultShown) return;
        _resultShown = true;

        var equip = EquipmentGacha.Draw(_pendingBox);
        if (equip == null) { gachaPopup.ShowResult("?", "알 수 없음", null); return; }

        EquipmentInventory.Shared?.TryAdd(equip);
        gachaPopup.ShowResult(equip.grade, equip.name, null, equip);
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
