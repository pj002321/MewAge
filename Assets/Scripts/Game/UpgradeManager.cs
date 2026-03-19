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
    // 시작 무기 지급
    // ────────────────────────────────────────────────────────────────
    public void GiveStarterWeapon()
    {
        var normals = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.grade == WeaponGrade.Normal && w.chapter <= currentChapter)
                normals.Add(w);

        if (normals.Count == 0) { Debug.LogWarning("[UpgradeManager] 지급할 무기 없음"); return; }

        var starter = normals[Random.Range(0, normals.Count)];
        var inv     = PlayerInventory.Shared;
        if (inv == null) return;

        if (!inv.TryAddToInventory(starter)) return;
        inv.EquipFromInventory(0); // 인벤 0번 슬롯 → 장착 슬롯으로
        Debug.Log($"[UpgradeManager] 시작 무기 지급: {starter.weaponName}");
    }

    // ────────────────────────────────────────────────────────────────
    // 패널 열기 / 닫기
    // ────────────────────────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        CollectAllDropItems();
        Time.timeScale = 0f;
        currentDraw    = DrawCards(4);
        int         playerLevel = PlayerStats.Shared?.Level ?? 1;
        DrawProfile p           = GetCurrentProfile(playerLevel);
        upgradePanel.Refresh(currentDraw, rerollPrice, buyExpPrice,
                             PlayerInventory.Shared?.Inventory.Count ?? 0,
                             PlayerInventory.MaxInventory, wave,
                             p.normal, p.rare, p.elite, p.epic, p.legend);
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

        // 인벤이 꽉 찬 경우: 인벤+장착 합산 2개 이상 동일 무기가 있으면 구매 후 즉시 합성
        if (inv.InventoryFull && inv.CountAll(data.weaponName, data.star) < 2)
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

        // 인벤 꽉 찬 상태라면 ForceAdd(합성이 직후 발동되어 슬롯 확보됨)
        if (inv.InventoryFull)
            inv.ForceAddToInventory(data);
        else if (!inv.TryAddToInventory(data))
        {
            stats.AddGold(data.price); // 환불
            upgradePanel.ShowNotification("인벤토리가 가득 찼습니다.");
            return;
        }

        // 구매한 카드는 드로우에서 제거
        for (int i = 0; i < currentDraw.Length; i++)
            if (currentDraw[i] == data) { currentDraw[i] = null; break; }

        SoundManager.Shared?.Play(SoundKey.UI_Buy);

        // 자동 합성: 같은 이름+성이 3개 모이면 반복 합성
        AutoMerge(inv, data.weaponName, data.star);

        RefreshPanel();
    }

    private void AutoMerge(PlayerInventory inv, string weaponName, int star)
    {
        while (star < 5 && inv.CanMergeAll(weaponName, star))
        {
            WeaponData result = GetNextGradeWeapon(weaponName, star);
            if (result == null) break;
            if (!inv.TryMergeAll(weaponName, star, result)) break;
            SoundManager.Shared?.Play(SoundKey.UI_Merge);
            star = result.star;
        }
    }

    /// <summary>인벤토리 + 장착 슬롯 전체를 스캔해 합성 가능한 모든 조합을 자동 합성합니다.</summary>
    public void TryAutoMergeAll()
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        bool anyMerged;
        do
        {
            anyMerged = false;
            var seen = new System.Collections.Generic.Dictionary<(string, int), int>();
            foreach (var w in inv.Inventory)
            {
                if (w == null) continue;
                var key = (w.weaponName, w.star);
                seen[key] = seen.TryGetValue(key, out int c) ? c + 1 : 1;
            }
            foreach (var w in inv.Equipped)
            {
                if (w == null) continue;
                var key = (w.weaponName, w.star);
                seen[key] = seen.TryGetValue(key, out int c) ? c + 1 : 1;
            }
            foreach (var kvp in seen)
            {
                if (kvp.Value < 3 || kvp.Key.Item2 >= 5) continue;
                var (name, star) = kvp.Key;
                WeaponData result = GetNextGradeWeapon(name, star);
                if (result == null) continue;
                if (!inv.TryMergeAll(name, star, result)) continue;
                SoundManager.Shared?.Play(SoundKey.UI_Merge);
                anyMerged = true;
                break; // 합성 후 목록 재스캔
            }
        } while (anyMerged);

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

    // ────────────────────────────────────────────────────────────────
    // 합성
    // ────────────────────────────────────────────────────────────────
    /// <summary>같은 이름의 다음 등급 무기를 반환합니다.</summary>
    public WeaponData GetNextGradeWeapon(string weaponName, int star)
    {
        if (star >= 5) return null;
        foreach (var w in allWeapons)
            if (w != null && w.weaponName == weaponName)
            {
                var next  = w.Upgraded();  // 희귀도 유지, 복사
                next.star = star + 1;      // allWeapons는 항상 star=1이므로 직접 지정
                return next;
            }
        return null;
    }

    /// <summary>인벤토리에서 같은 이름+성 3개를 합성합니다.</summary>
    public bool TryMerge(string weaponName, int star)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return false;

        if (star >= 5)
        {
            upgradePanel.ShowNotification("최대 성급은 합성할 수 없습니다.");
            return false;
        }

        if (!inv.CanMergeAll(weaponName, star))
        {
            upgradePanel.ShowNotification("같은 무기 같은 성급이 3개 필요합니다.");
            return false;
        }

        WeaponData result = GetNextGradeWeapon(weaponName, star);
        if (result == null)
        {
            upgradePanel.ShowNotification($"DB에 {weaponName} 항목이 없습니다.");
            return false;
        }

        if (!inv.TryMergeAll(weaponName, star, result)) return false;

        SoundManager.Shared?.Play(SoundKey.UI_Merge);
        RefreshPanel();
        return true;
    }

    /// <summary>DB_확률 기반으로 희귀도를 뽑아 count장 카드 드로우</summary>
    private WeaponData[] DrawCards(int count)
    {
        var pool = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.chapter <= currentChapter)
            { pool.Add(w); pool.Add(w); }   // 같은 무기 최대 2장

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

    private DrawProfile GetCurrentProfile(int level)
    {
        DrawProfile result = drawProfiles.Length > 0 ? drawProfiles[0] : default;
        foreach (var p in drawProfiles)
            if (level >= p.minLevel) result = p;
        return result;
    }

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

    private WeaponData PickByGrade(List<WeaponData> pool, WeaponGrade grade)
    {
        var candidates = pool.FindAll(w => w.grade == grade);
        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];
        // 해당 희귀도 무기 없으면 아무 무기나 반환
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
    }
}
