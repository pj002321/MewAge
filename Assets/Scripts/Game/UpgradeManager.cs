using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 웨이브 종료 업그레이드 시스템을 총괄합니다.
/// - 레벨에 따라 등급 가중치로 카드를 뽑습니다.
/// - 최대 10장 구매, 리롤/경험치 구매 기능 제공.
/// </summary>
public class UpgradeManager : MonoBehaviourSingleton<UpgradeManager>
{
    [Header("무기 데이터 풀 (모든 WeaponData 등록)")]
    [SerializeField] private WeaponData[] allWeapons;

    [Header("UI")]
    [SerializeField] private UpgradePanel upgradePanel;

    [Header("설정")]
    [SerializeField] private int rerollPrice  = 15;
    [SerializeField] private int buyExpAmount = 30;
    [SerializeField] private int buyExpPrice  = 20;
    [SerializeField] private int maxOwnedCards = 10;

    // 등급 기본 가중치 (레벨에 따라 일부 조정됨)
    private static readonly Dictionary<WeaponGrade, int> GradeWeight = new()
    {
        { WeaponGrade.Normal,    100 },
        { WeaponGrade.Rare,       50 },
        { WeaponGrade.Epic,       20 },
        { WeaponGrade.Legendary,   5 },
    };

    private readonly List<WeaponData> ownedWeapons  = new();
    private WeaponData[] currentDraw = System.Array.Empty<WeaponData>();

    protected override bool DontDestroy => false;

    // ──────────────────────────────────────────────
    // 패널 열기 / 닫기
    // ──────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        CollectAllDropItems();
        Time.timeScale = 0f;
        currentDraw    = DrawCards(4);
        upgradePanel.Refresh(currentDraw, rerollPrice, buyExpPrice,
                             ownedWeapons.Count, maxOwnedCards, wave);
        upgradePanel.gameObject.SetActive(true);
    }

    private static void CollectAllDropItems()
    {
        foreach (var item in Object.FindObjectsByType<DropItem>(FindObjectsSortMode.None))
            item.Collect();
    }

    public void ClosePanel()
    {
        upgradePanel.gameObject.SetActive(false);
        Time.timeScale = 1f;
        GameManager.Shared?.StartNextWave();
    }

    // ──────────────────────────────────────────────
    // 카드 구매
    // ──────────────────────────────────────────────
    public void TryBuy(WeaponData data)
    {
        if (data == null) return;
        if (ownedWeapons.Count >= maxOwnedCards)
        {
            upgradePanel.ShowNotification($"카드는 최대 {maxOwnedCards}장까지 보관할 수 있습니다.");
            return;
        }

        var stats = PlayerStats.Shared;
        if (stats == null || stats.Gold < data.price)
        {
            upgradePanel.ShowNotification("골드가 부족합니다.");
            return;
        }

        stats.SpendGold(data.price);
        ownedWeapons.Add(data);

        // 구매한 카드는 현재 드로우에서 제거하고 비워둠
        for (int i = 0; i < currentDraw.Length; i++)
            if (currentDraw[i] == data) { currentDraw[i] = null; break; }

        RefreshPanel();
    }

    // ──────────────────────────────────────────────
    // 리롤
    // ──────────────────────────────────────────────
    public void TryReroll()
    {
        var stats = PlayerStats.Shared;
        if (stats == null || stats.Gold < rerollPrice)
        {
            upgradePanel.ShowNotification("골드가 부족합니다.");
            return;
        }

        stats.SpendGold(rerollPrice);
        currentDraw = DrawCards(4);
        RefreshPanel();
    }

    // ──────────────────────────────────────────────
    // 경험치 구매
    // ──────────────────────────────────────────────
    public void TryBuyExp()
    {
        var stats = PlayerStats.Shared;
        if (stats == null || stats.Gold < buyExpPrice)
        {
            upgradePanel.ShowNotification("골드가 부족합니다.");
            return;
        }

        stats.SpendGold(buyExpPrice);
        stats.AddExp(buyExpAmount);
        RefreshPanel();
    }

    // ──────────────────────────────────────────────
    // 내부 유틸
    // ──────────────────────────────────────────────
    private void RefreshPanel()
    {
        int wave = GameManager.Shared?.CurrentWave ?? 1;
        upgradePanel.Refresh(currentDraw, rerollPrice, buyExpPrice,
                             ownedWeapons.Count, maxOwnedCards, wave);
    }

    /// <summary>레벨 필터 + 등급 가중치로 count장 뽑기 (중복 없음)</summary>
    private WeaponData[] DrawCards(int count)
    {
        int playerLevel = PlayerStats.Shared?.Level ?? 1;

        // 레벨 조건을 만족하는 무기 필터링
        var pool = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.minPlayerLevel <= playerLevel)
                pool.Add(w);

        var result = new WeaponData[count];
        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            // 가중 합산
            int totalWeight = 0;
            foreach (var w in pool)
                totalWeight += GetWeight(w.grade, playerLevel);

            int roll = Random.Range(0, totalWeight);
            int acc  = 0;
            WeaponData picked = pool[0];
            foreach (var w in pool)
            {
                acc += GetWeight(w.grade, playerLevel);
                if (roll < acc) { picked = w; break; }
            }

            result[i] = picked;
            pool.Remove(picked);
        }
        return result;
    }

    /// <summary>플레이어 레벨에 따라 고급 등급 가중치를 올려줌</summary>
    private int GetWeight(WeaponGrade grade, int level)
    {
        int w = GradeWeight[grade];
        // 레벨이 높을수록 고급 등급 가중치 상승
        return grade switch
        {
            WeaponGrade.Epic      => w + Mathf.Max(0, (level - 3) * 5),
            WeaponGrade.Legendary => w + Mathf.Max(0, (level - 6) * 5),
            _                     => w,
        };
    }

    public IReadOnlyList<WeaponData> OwnedWeapons => ownedWeapons;
}
