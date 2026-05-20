using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 홈 상점 패널.
/// 단일 세로 스크롤: 패키지 → 일일상점 → 장비뽑기 → 골드 → 보석
///
/// 일일상점: DB_상점 f_상점타입="DAILY" 행 전체를 풀로 사용,
///   날짜 기반 시드로 매일 6개를 랜덤 추출해 노출합니다.
///   DB 필드 매핑: f_T1=통화(GOLD/RUBY) f_V1=수량최소 f_V2=수량최대 f_S가격=일일한도
/// </summary>
public class HomeShopPanel : MonoBehaviour
{
    [Header("가챠 결과 팝업")]
    public GachaResultPopup gachaPopup;

    [Header("섹션 스크롤")]
    [SerializeField] private ScrollRect    scrollRect;
    [SerializeField] private RectTransform gemSection;
    [SerializeField] private RectTransform goldSection;

    [Header("일일상점")]
    [SerializeField] private Transform  dailySlotContainer;
    [SerializeField] private RewardSlot dailySlotPrefab;
    private const int DailySlotCount = 6;

    bool         _resultShown;
    EquipBoxType _pendingBox;
    int          _pendingCount = 1;

    // ── 라이프사이클 ────────────────────────────────────────────────────

    private void Start() => BuildDailyShop();

    // ── 일일상점 ────────────────────────────────────────────────────────

    private void BuildDailyShop()
    {
        if (dailySlotContainer == null || dailySlotPrefab == null) return;

        foreach (Transform child in dailySlotContainer)
            Destroy(child.gameObject);

        var cfg = RewardVisualConfig.Shared;
        var slots = new (string id, int amount)[]
        {
            ("COIN",            5000),
            ("RUBY",              10),
            ("KEY_NORMAL",         1),
            ("KEY_RELIC_NORMAL",   1),
            ("MAT_HERO",           3),
            ("EXP",              500),
        };
        foreach (var s in slots)
        {
            var item = new RewardItem(s.id, s.amount);
            Instantiate(dailySlotPrefab, dailySlotContainer)
                .Setup(cfg?.GetIcon(s.id), $"×{s.amount}", () => RewardManager.Shared?.GiveReward(item), s.id);
        }
    }

    // ── 뽑기 흐름 ───────────────────────────────────────────────────────

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
            foreach (var equip in results)
            {
                if (equip == null) continue;
                EquipmentInventory.Shared?.TryAdd(equip);
            }
            gachaPopup.ShowMultiResult(results);
        }
    }

    static GachaBoxType ToGachaBoxType(EquipBoxType t) => t switch
    {
        EquipBoxType.Rare => GachaBoxType.EquipRare,
        EquipBoxType.Epic => GachaBoxType.EquipEpic,
        _                 => GachaBoxType.EquipNormal,
    };

    // ── 섹션 이동 ───────────────────────────────────────────────────────

    public void ScrollToGem()  => StartCoroutine(ScrollToSection(gemSection));
    public void ScrollToGold() => StartCoroutine(ScrollToSection(goldSection));

    private IEnumerator ScrollToSection(RectTransform section)
    {
        if (scrollRect == null || section == null) yield break;
        yield return null;

        Canvas.ForceUpdateCanvases();
        var content  = scrollRect.content;
        var viewport = scrollRect.viewport != null ? scrollRect.viewport : scrollRect.GetComponent<RectTransform>();
        Vector2 sectionLocal = (Vector2)content.InverseTransformPoint(section.position);
        float   contentH     = content.rect.height;
        float   viewportH    = viewport.rect.height;
        float   targetY      = Mathf.Clamp01((-sectionLocal.y) / (contentH - viewportH));
        scrollRect.verticalNormalizedPosition = 1f - targetY;
    }

    // ── IAP / 재화 구매 ─────────────────────────────────────────────────

    public void PurchaseGems(string productId) => Debug.Log($"[Shop] 보석 구매: {productId}");
    public void PurchaseGold(int goldAmount)   => Debug.Log($"[Shop] 골드 구매: {goldAmount}");
}
