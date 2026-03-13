using System.Collections.Generic;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 웨이브 종료 업그레이드 시스템을 총괄합니다.
/// 카드 구매 시 PlayerInventory에 추가합니다.
/// </summary>
public class UpgradeManager : MonoBehaviourSingleton<UpgradeManager>
{
    [Header("UI")]
    [SerializeField] private UpgradePanel upgradePanel;

    [Header("설정")]
    [SerializeField] private int rerollPrice  = 15;
    [SerializeField] private int buyExpAmount = 30;
    [SerializeField] private int buyExpPrice  = 20;

    private struct DrawProfile
    {
        public int   minLevel;
        public float normal, rare, elite, epic, legend;
    }

    private DrawProfile[] drawProfiles = System.Array.Empty<DrawProfile>();

    private WeaponData[] allWeapons   = System.Array.Empty<WeaponData>();
    private WeaponData[] currentDraw  = System.Array.Empty<WeaponData>();
    private int          currentChapter = 1;

    protected override bool DontDestroy => false;

    protected override void Awake()
    {
        base.Awake();
        LoadWeaponsFromDB();
        LoadDrawProfiles();
    }

    private void LoadWeaponsFromDB()
    {
        var list = new List<WeaponData>();
        DB_무기.ForEachEntity(row => list.Add(WeaponData.FromDB(row)));
        allWeapons = list.ToArray();
    }

    private void LoadDrawProfiles()
    {
        var list = new List<DrawProfile>();
        DB_확률.ForEachEntity(row => list.Add(new DrawProfile
        {
            minLevel = row.f_레벨,
            normal   = row.f_노멀,
            rare     = row.f_레어,
            elite    = row.f_엘리트,
            epic     = row.f_에픽,
            legend   = row.f_신화,
        }));
        list.Sort((a, b) => a.minLevel.CompareTo(b.minLevel));
        drawProfiles = list.ToArray();
    }

    // ────────────────────────────────────────────────────────────────
    // 패널 열기 / 닫기
    // ────────────────────────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        CollectAllDropItems();
        Time.timeScale = 0f;
        currentDraw    = DrawCards(4);
        upgradePanel.Refresh(currentDraw, rerollPrice, buyExpPrice,
                             PlayerInventory.Shared?.Inventory.Count ?? 0,
                             PlayerInventory.MaxInventory, wave);
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

    // ────────────────────────────────────────────────────────────────
    // 카드 구매
    // ────────────────────────────────────────────────────────────────
    public void TryBuy(WeaponData data)
    {
        if (data == null) return;

        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        if (inv.InventoryFull)
        {
            upgradePanel.ShowNotification("인벤토리가 가득 찼습니다.");
            return;
        }

        var stats = PlayerStats.Shared;
        if (stats == null || stats.Gold < data.price)
        {
            upgradePanel.ShowNotification("골드가 부족합니다.");
            return;
        }

        stats.SpendGold(data.price);

        if (!inv.TryAddToInventory(data))
        {
            stats.AddGold(data.price); // 환불
            upgradePanel.ShowNotification("인벤토리가 가득 찼습니다.");
            return;
        }

        // 구매한 카드는 드로우에서 제거
        for (int i = 0; i < currentDraw.Length; i++)
            if (currentDraw[i] == data) { currentDraw[i] = null; break; }

        SoundManager.Shared?.Play(SoundKey.UI_Buy);
        RefreshPanel();
    }

    // ────────────────────────────────────────────────────────────────
    // 리롤
    // ────────────────────────────────────────────────────────────────
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
        SoundManager.Shared?.Play(SoundKey.UI_Reroll);
        RefreshPanel();
    }

    // ────────────────────────────────────────────────────────────────
    // 경험치 구매
    // ────────────────────────────────────────────────────────────────
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

    // ────────────────────────────────────────────────────────────────
    // 내부 유틸
    // ────────────────────────────────────────────────────────────────
    private void RefreshPanel()
    {
        int wave = GameManager.Shared?.CurrentWave ?? 1;
        int playerLevel = PlayerStats.Shared?.Level ?? 1;
        DrawProfile p   = GetCurrentProfile(playerLevel);
        upgradePanel.Refresh(currentDraw, rerollPrice, buyExpPrice,
                             PlayerInventory.Shared?.Inventory.Count ?? 0,
                             PlayerInventory.MaxInventory, wave,
                             p.normal, p.rare, p.elite, p.epic, p.legend);
    }

    /// <summary>DB 확률 테이블 기반으로 count장 뽑기 (중복 없음)</summary>
    private WeaponData[] DrawCards(int count)
    {
        // 같은 무기 최대 2장까지 허용
        var pool = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.chapter <= currentChapter)
            { pool.Add(w); pool.Add(w); }

        int         playerLevel = PlayerStats.Shared?.Level ?? 1;
        DrawProfile profile     = GetCurrentProfile(playerLevel);

        var result = new WeaponData[count];
        for (int i = 0; i < count; i++)
        {
            if (pool.Count == 0) break;

            WeaponGrade grade  = RollGrade(profile);
            WeaponData  picked = PickByGrade(pool, grade);
            if (picked != null) { result[i] = picked; pool.Remove(picked); }
        }
        return result;
    }

    /// <summary>현재 레벨에 맞는 DrawProfile 반환 (가장 가까운 하위 레벨 row)</summary>
    private DrawProfile GetCurrentProfile(int level)
    {
        DrawProfile result = drawProfiles.Length > 0 ? drawProfiles[0] : default;
        foreach (var p in drawProfiles)
            if (level >= p.minLevel) result = p;
        return result;
    }

    /// <summary>확률 가중치로 등급 랜덤 선택 (합계 100 기준 소수점 지원)</summary>
    private static WeaponGrade RollGrade(DrawProfile p)
    {
        float roll = Random.Range(0f, 100f);
        float acc  = 0f;
        acc += p.normal; if (roll <= acc && p.normal > 0f) return WeaponGrade.Normal;
        acc += p.rare;   if (roll <= acc && p.rare > 0f)   return WeaponGrade.Rare;
        acc += p.elite;  if (roll <= acc && p.elite > 0f)  return WeaponGrade.Elite;
        acc += p.epic;   if (roll <= acc && p.epic > 0f)   return WeaponGrade.Epic;
        return WeaponGrade.Legend;
    }

    /// <summary>해당 등급 카드를 pool에서 무작위 선택. 없으면 null 반환 (확률DB 엄격 적용)</summary>
    private WeaponData PickByGrade(List<WeaponData> pool, WeaponGrade grade)
    {
        var candidates = pool.FindAll(w => w.grade == grade);
        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];
        
        this.DLog($"[UpgradeManager] 해당 등급({grade})의 무기가 모두 소진되었으므로, 빈 슬롯으로 표시됩니다.");
        return null;
    }
}
