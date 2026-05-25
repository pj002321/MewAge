using System.Collections;
using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 상점 패널.
///
/// 일일상점 : f_D상점ID 풀에서 날짜 seed(year*1000+dayOfYear) 가중치 랜덤 DailySlotCount개 선택.
/// 골드상점 : f_G상점ID 행 순서대로 f_G행수개 슬롯 생성. 루비 결제 → 골드 지급.
/// 루비상점 : f_R상점ID 행 순서대로 f_R행수개 슬롯 생성. 골드 결제 → 루비 지급.
/// 챕터상점 : f_C상품ID "C_*" 행 순서대로 f_C행수개 슬롯 생성. 루비 결제 → 4가지 아이템 지급.
/// </summary>
public class HomeShopPanel : MonoBehaviour
{
    [Header("가챠 결과 팝업")]
    public GachaResultPopup gachaPopup;

    [Header("섹션 스크롤")]
    [SerializeField] ScrollRect    scrollRect;
    [SerializeField] RectTransform gemSection;
    [SerializeField] RectTransform goldSection;

    [Header("구매 결과 팝업 (공용)")]
    [SerializeField] ShopResultPopUp resultPopup;

    [Header("일일상점")]
    [SerializeField] Transform              dailySlotContainer;
    [SerializeField] DailyShopSlot          dailySlotPrefab;
    [SerializeField] DailyShopConfirmPopUp  dailyConfirmPopup;

    [Header("골드상점")]
    [SerializeField] Transform        goldRow1;
    [SerializeField] Transform        goldRow2;
    [SerializeField] CurrencyShopSlot goldSlotPrefab;

    [Header("루비상점")]
    [SerializeField] Transform        rubyRow1;
    [SerializeField] Transform        rubyRow2;
    [SerializeField] CurrencyShopSlot rubySlotPrefab;

    [Header("챕터상점")]
    [SerializeField] Transform        chapterSlotContainer;
    [SerializeField] ChapterShopSlot  chapterSlotPrefab;

    const int DailySlotCount = 6;

    bool         _resultShown;
    EquipBoxType _pendingBox;
    int          _pendingCount = 1;

    // ── 라이프사이클 ───────────────────────────────────────────────────────

    void Start()
    {
        BuildDailyShop();
        BuildGoldShop();
        BuildRubyShop();
        BuildChapterShop();
    }

    void OnDisable()
    {
        if (gachaPopup != null)
        {
            gachaPopup.StopAllCoroutines();
            gachaPopup.OnDrawRequested = null;
            gachaPopup.gameObject.SetActive(false);
        }
        resultPopup?.gameObject.SetActive(false);
        dailyConfirmPopup?.gameObject.SetActive(false);
    }

    // ── 일일상점 ──────────────────────────────────────────────────────────

    void BuildDailyShop()
    {
        if (!dailySlotContainer || !dailySlotPrefab) return;
        ClearChildren(dailySlotContainer);

        var pool = LoadRows(r => !string.IsNullOrEmpty(r.f_D상점ID), r => r.f_D행수,
            (a, b) => string.Compare(a.f_D상점ID, b.f_D상점ID, System.StringComparison.Ordinal));
        if (pool.Count == 0) return;

        int seed    = System.DateTime.Today.Year * 1000 + System.DateTime.Today.DayOfYear;
        string date = System.DateTime.Today.ToString("yyyyMMdd");

        foreach (var row in WeightedSelect(pool, DailySlotCount, seed, r => Mathf.Max(1, r.f_확률)))
        {
            var slot = Instantiate(dailySlotPrefab, dailySlotContainer);
            slot.Setup(row, $"DailyBuy_{row.f_D상점ID}_{date}", OnDailyBuy);
        }
    }

    void OnDailyBuy(DB_상점 row, DailyShopSlot slot)
    {
        string key    = $"DailyBuy_{row.f_D상점ID}_{System.DateTime.Today:yyyyMMdd}";
        int    bought = PlayerPrefs.GetInt(key, 0);
        if (row.f_한도 > 0 && bought >= row.f_한도) { PopupBaseNoti.Shared?.Show("구매 한도를 초과했습니다"); return; }

        if (dailyConfirmPopup != null)
            dailyConfirmPopup.Show(row, onConfirm: () => DoPurchase(row, slot, key));
        else
            DoPurchase(row, slot, key);
    }

    void DoPurchase(DB_상점 row, DailyShopSlot slot, string key)
    {
        if (RewardManager.Shared == null) { Debug.LogError("[DailyBuy] RewardManager.Shared is null"); return; }
        var icon = RewardVisualConfig.Shared?.GetIcon(row.f_D보상);
        RewardManager.Shared.Dispatch(new RewardItem(row.f_D보상, 1), icon);
        PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
        slot.RefreshLimit();
        resultPopup?.Show(icon, 1);
    }

    // ── 골드상점 ──────────────────────────────────────────────────────────

    void BuildGoldShop()
    {
        if (!goldSlotPrefab) return;
        ClearChildren(goldRow1); ClearChildren(goldRow2);
        var rows = LoadRows(r => !string.IsNullOrEmpty(r.f_G상점ID), r => r.f_G행수,
            (a, b) => string.Compare(a.f_G상점ID, b.f_G상점ID, System.StringComparison.Ordinal));
        var cfg      = RewardVisualConfig.Shared;
        var rubyIcon = cfg?.GetIcon(RewardType.Ruby);
        var goldIcon = cfg?.GetIcon(RewardType.Gold);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            Instantiate(goldSlotPrefab, i < 3 ? goldRow1 : goldRow2).Setup(rubyIcon, r.f_G수량, r.f_G가격, false, () =>
            {
                var rm = RewardManager.Shared;
                if (rm == null) return;
                if (rm.Ruby < r.f_G가격) { PopupBaseNoti.Shared?.Show("루비가 부족합니다"); return; }
                rm.SpendRuby(r.f_G가격);
                rm.GiveReward(new RewardItem(RewardType.Gold, r.f_G수량));
                resultPopup?.Show(goldIcon, r.f_G수량);
            });
        }
    }

    // ── 루비상점 ──────────────────────────────────────────────────────────

    void BuildRubyShop()
    {
        if (!rubySlotPrefab) return;
        ClearChildren(rubyRow1); ClearChildren(rubyRow2);
        var rows = LoadRows(r => !string.IsNullOrEmpty(r.f_R상점ID), r => r.f_R행수,
            (a, b) => string.Compare(a.f_R상점ID, b.f_R상점ID, System.StringComparison.Ordinal));
        var rubyIcon2 = RewardVisualConfig.Shared?.GetIcon(RewardType.Ruby);
        for (int i = 0; i < rows.Count; i++)
        {
            var r = rows[i];
            Instantiate(rubySlotPrefab, i < 3 ? rubyRow1 : rubyRow2).Setup(null, r.f_R수량, r.f_R가격, true, () =>
            {
                RewardManager.Shared?.GiveReward(new RewardItem(RewardType.Ruby, r.f_R수량));
                resultPopup?.Show(rubyIcon2, r.f_R수량);
            });
        }
    }

    // ── 챕터상점 ──────────────────────────────────────────────────────────

    void BuildChapterShop()
    {
        if (!chapterSlotContainer || !chapterSlotPrefab) return;
        ClearChildren(chapterSlotContainer);

        foreach (var row in LoadRows(
            r => !string.IsNullOrEmpty(r.f_C상품ID) && r.f_C상품ID.StartsWith("C_"),
            r => r.f_C행수,
            (a, b) => string.Compare(a.f_C상품ID, b.f_C상품ID, System.StringComparison.Ordinal)))
        {
            int  chNum  = ChapterNum(row.f_C상품ID);
            bool locked = chNum > 1 && PlayerPrefs.GetInt($"ChapterCleared_C{chNum - 1}_S1", 0) == 0;
            var  r      = row;
            Instantiate(chapterSlotPrefab, chapterSlotContainer).Setup(r, locked, () => BuyChapter(r));
        }
    }

    void BuyChapter(DB_상점 row)
    {
        if (RewardManager.Shared == null) return;
        var ids  = new[] { row.f_T1, row.f_T2, row.f_T3, row.f_T4 };
        var vals = new[] { row.f_V1, row.f_V2, row.f_V3, row.f_V4 };
        var cfg  = RewardVisualConfig.Shared;

        var items = new System.Collections.Generic.List<(Sprite, int)>();
        for (int i = 0; i < ids.Length; i++)
        {
            if (string.IsNullOrEmpty(ids[i]) || vals[i] <= 0) continue;
            RewardManager.Shared.Dispatch(new RewardItem(ids[i], vals[i]), cfg?.GetIcon(ids[i]));
            items.Add((cfg?.GetIcon(ids[i]), vals[i]));
        }

        if (resultPopup != null && items.Count > 0)
            resultPopup.Show(items.ToArray());
    }

    // ── 뽑기 흐름 ─────────────────────────────────────────────────────────

    public void TryDraw(int gemCost, EquipBoxType boxType = EquipBoxType.Normal)
        => TryDrawMulti(gemCost, 1, boxType);

    public void TryDrawMulti(int gemCost, int count, EquipBoxType boxType = EquipBoxType.Normal)
    {
        if (gachaPopup == null) { Debug.LogWarning("[Shop] gachaPopup 참조 없음"); return; }
        _resultShown  = false;
        _pendingBox   = boxType;
        _pendingCount = count;

        gachaPopup.OnDrawRequested  = null;
        gachaPopup.OnDrawRequested += OnGachaDrawPressed;
        gachaPopup.OnDrawReset      = null;
        gachaPopup.OnDrawReset     += () => _resultShown = false;
        gachaPopup.OpenForDraw(gemCost, ToGachaBoxType(boxType));
    }

    void OnGachaDrawPressed()
    {
        _resultShown = true;
        if (EquipmentInventory.Shared == null)
            MonoBehaviourSingleton<EquipmentInventory>.Create("EquipmentInventory");

        if (_pendingCount <= 1)
        {
            var equip = EquipmentGacha.Draw(_pendingBox);
            EquipmentInventory.Shared?.TryAdd(equip);
            gachaPopup.ShowResult(equip);
        }
        else
        {
            var results = EquipmentGacha.DrawMulti(_pendingCount, _pendingBox);
            foreach (var e in results) { if (e != null) EquipmentInventory.Shared?.TryAdd(e); }
            gachaPopup.ShowMultiResult(results);
        }
    }

    static GachaBoxType ToGachaBoxType(EquipBoxType t) => t switch
    {
        EquipBoxType.Rare => GachaBoxType.EquipRare,
        EquipBoxType.Epic => GachaBoxType.EquipEpic,
        _                 => GachaBoxType.EquipNormal,
    };

    // ── 섹션 스크롤 ───────────────────────────────────────────────────────

    public void ScrollToGem()  => StartCoroutine(ScrollToSection(gemSection));
    public void ScrollToGold() => StartCoroutine(ScrollToSection(goldSection));

    IEnumerator ScrollToSection(RectTransform section)
    {
        if (!scrollRect || !section) yield break;
        yield return null;
        Canvas.ForceUpdateCanvases();
        var content   = scrollRect.content;
        var viewport  = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        float targetY = Mathf.Clamp01((-((Vector2)content.InverseTransformPoint(section.position)).y)
                        / (content.rect.height - viewport.rect.height));
        scrollRect.verticalNormalizedPosition = 1f - targetY;
    }

    // ── 공용 유틸 ─────────────────────────────────────────────────────────

    static void ClearChildren(Transform t)
    {
        if (!t) return;
        for (int i = t.childCount - 1; i >= 0; i--) Destroy(t.GetChild(i).gameObject);
    }

    static List<DB_상점> LoadRows(System.Func<DB_상점, bool> filter,
        System.Func<DB_상점, int> countFn, System.Comparison<DB_상점> compare)
    {
        var list  = new List<DB_상점>();
        int count = 0;
        DB_상점.ForEachEntity(row =>
        {
            if (!filter(row)) return;
            list.Add(row);
            if (count == 0 && countFn(row) > 0) count = countFn(row);
        });
        list.Sort(compare);
        if (count > 0 && list.Count > count) list.RemoveRange(count, list.Count - count);
        return list;
    }

    static List<T> WeightedSelect<T>(List<T> pool, int count, int seed, System.Func<T, int> weightFn)
    {
        var result = new List<T>();
        var rem    = new List<T>(pool);
        var rng    = new System.Random(seed);
        for (int i = 0; i < Mathf.Min(count, rem.Count); i++)
        {
            int total = 0;
            foreach (var x in rem) total += weightFn(x);
            if (total <= 0) break;
            int roll = rng.Next(total), acc = 0;
            for (int j = 0; j < rem.Count; j++)
            {
                acc += weightFn(rem[j]);
                if (roll < acc) { result.Add(rem[j]); rem.RemoveAt(j); break; }
            }
        }
        return result;
    }

    static int ChapterNum(string cId) =>
        cId.Split('_') is { Length: >= 2 } p && int.TryParse(p[1], out int n) ? n : 0;

    // ── IAP ───────────────────────────────────────────────────────────────

    public void PurchaseGems(string productId) => Debug.Log($"[Shop] 보석 구매: {productId}");
    public void PurchaseGold(int goldAmount)   => Debug.Log($"[Shop] 골드 구매: {goldAmount}");
}
