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

    public int BuyExpAmount => buyExpAmount;

    private struct DrawProfile
    {
        public int   minLevel;
        public float common, rare, epic, legendary, mythic;
    }

    private DrawProfile[] drawProfiles = System.Array.Empty<DrawProfile>();

    private WeaponData[] allWeapons   = System.Array.Empty<WeaponData>();
    private WeaponData[] currentDraw  = System.Array.Empty<WeaponData>();
    private WeaponData   adCardData;
    private int          currentChapter = 1;
    private string       currentChapterID = "C1_S1";
    private bool[]       isCardSold = new bool[3]; // 일반 카드 판매 여부
    private bool         isAdCardSold;             // 광고 카드 판매 여부
    public  int          CurrentChapter => currentChapter;
    public  string       CurrentChapterID => currentChapterID;

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
        DB_확률.ForEachEntity(row =>
        {
            if (row.f_레벨 <= 0) return;
            list.Add(new DrawProfile
            {
                minLevel  = row.f_레벨,
                common    = row.f_일반,
                rare      = row.f_고급,
                epic      = row.f_희귀,
                legendary = row.f_전설,
                mythic    = row.f_신화,
            });
        });
        list.Sort((a, b) => a.minLevel.CompareTo(b.minLevel));
        drawProfiles = list.ToArray();
    }

    // ────────────────────────────────────────────────────────────────
    // 시작 무기 지급
    // ────────────────────────────────────────────────────────────────
    public void SetCurrentChapterID(string id)
    {
        currentChapterID = id;
        // chapter 번호 추출
        string chapterPart = id.Split('_')[0];
        if (int.TryParse(chapterPart.Substring(1), out int chap))
            currentChapter = chap;
    }

    public void GiveStarterWeapon()
    {
        // 1순위: Common + Melee
        var normals = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.grade == WeaponGrade.Common && w.chapter == currentChapter
                && w.weaponType == WeaponType.Melee)
                normals.Add(w);

        // 2순위: Common 아무 타입
        if (normals.Count == 0)
        {
            foreach (var w in allWeapons)
                if (w != null && w.grade == WeaponGrade.Common && w.chapter == currentChapter)
                    normals.Add(w);
        }

        if (normals.Count == 0)
        {
            Debug.LogWarning($"[UpgradeManager] 지급할 무기 없음 — chapter={currentChapter}, 전체무기={allWeapons.Length}, ch{currentChapter}무기={System.Array.FindAll(allWeapons, w => w != null && w.chapter == currentChapter).Length}");
            return;
        }

        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        for (int i = 0; i < 2; i++)
        {
            var starter = normals[Random.Range(0, normals.Count)];
            if (inv.TryAddToInventory(starter))
            {
                inv.EquipFromInventory(0); // 인벤 0번 슬롯 → 장착 슬롯으로
                Debug.Log($"[UpgradeManager] 시작 무기 지급 {i + 1}: {starter.weaponName}");
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 패널 열기 / 닫기
    // ────────────────────────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        CollectAllDropItems();
        int         playerLevel = PlayerStats.Shared?.Level ?? 1;
        DrawProfile p           = GetCurrentProfile(playerLevel);
        currentDraw = DrawCards(3);
        adCardData  = DrawAdCard(p);

        // 판매 상태 초기화
        for (int i = 0; i < isCardSold.Length; i++) isCardSold[i] = false;
        isAdCardSold = false;

        upgradePanel.Refresh(currentDraw, rerollPrice, buyExpPrice,
                             PlayerInventory.Shared?.Inventory.Count ?? 0,
                             PlayerInventory.MaxInventory, wave,
                             p.common, p.rare, p.epic, p.legendary, p.mythic, adCardData,
                             isCardSold, isAdCardSold);
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
        GameManager.Shared?.StartNextWave();
    }

    // ────────────────────────────────────────────────────────────────
    // 카드 구매
    // ────────────────────────────────────────────────────────────────
    public void TryBuy(WeaponData data, int index)
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

        // 구매한 카드는 판매됨으로 표시 (인덱스 기반)
        if (index >= 0 && index < isCardSold.Length)
        {
            isCardSold[index] = true;
        }
        else if (index == 3) // 광고 카드 슬롯 (카드 슬롯이 4개일 때 4번째는 인덱스 3)
        {
            isAdCardSold = true;
        }
        
        // 카드에 sold overlay 표시 (즉각 연출)
        upgradePanel?.SoldOutCard(data);

        SoundManager.Shared?.Play(SoundKey.Weapon_Equip);

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
        int         playerLevel = PlayerStats.Shared?.Level ?? 1;
        DrawProfile p           = GetCurrentProfile(playerLevel);
        currentDraw = DrawCards(3);
        adCardData  = DrawAdCard(p);

        // 리롤 시 판매 상태 초기화
        for (int i = 0; i < isCardSold.Length; i++) isCardSold[i] = false;
        isAdCardSold = false;

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
                             p.common, p.rare, p.epic, p.legendary, p.mythic, adCardData,
                             isCardSold, isAdCardSold);
    }

    // ────────────────────────────────────────────────────────────────
    // 광고 카드
    // ────────────────────────────────────────────────────────────────
    /// <summary>일반 카드와 동일한 확률로 등급을 뽑되, star=2로 지급합니다.</summary>
    private WeaponData DrawAdCard(DrawProfile profile)
    {
        var pool = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.chapter == currentChapter)
            { pool.Add(w); pool.Add(w); }

        WeaponGrade grade  = RollGrade(profile);
        WeaponData  picked = PickByGrade(pool, grade);
        if (picked == null) return null;

        return picked.UpgradedTo(2);
    }

    /// <summary>광고 시청 완료 후 호출 — 광고 카드를 인벤토리에 무료로 지급합니다.</summary>
    public void TryClaimAdCard()
    {
        if (adCardData == null || isAdCardSold) return;

        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        if (inv.InventoryFull && inv.CountAll(adCardData.weaponName, adCardData.star) < 2)
        {
            upgradePanel.ShowNotification("인벤토리가 가득 찼습니다.");
            return;
        }

        string claimedName = adCardData.weaponName;
        int    claimedStar = adCardData.star;

        if (inv.InventoryFull)
            inv.ForceAddToInventory(adCardData);
        else if (!inv.TryAddToInventory(adCardData))
        {
            upgradePanel.ShowNotification("인벤토리가 가득 찼습니다.");
            return;
        }

        isAdCardSold = true;
        SoundManager.Shared?.Play(SoundKey.UI_Buy);
        AutoMerge(inv, claimedName, claimedStar);
        RefreshPanel();
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
                return w.UpgradedTo(star + 1);  // 목표 성까지 보너스 전부 적용
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
            if (w != null && w.chapter == currentChapter)
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

    public struct DrawProbs
    {
        public float common, rare, epic, legendary, mythic;
    }

    public DrawProbs GetCurrentProbs()
    {
        int playerLevel = PlayerStats.Shared?.Level ?? 1;
        var p = GetCurrentProfile(playerLevel);
        return new DrawProbs
        {
            common    = p.common,
            rare      = p.rare,
            epic      = p.epic,
            legendary = p.legendary,
            mythic    = p.mythic,
        };
    }

    private DrawProfile GetCurrentProfile(int level)
    {
        if (drawProfiles.Length == 0) return default;

        // 플레이어 레벨을 충족하는 프로필 중 minLevel이 가장 높은 것을 선택
        DrawProfile result    = drawProfiles[0]; // minLevel 최솟값 프로필을 기본 폴백으로
        int         bestLevel = int.MinValue;
        foreach (var p in drawProfiles)
        {
            if (p.minLevel < result.minLevel)
                result = p; // 가장 낮은 minLevel 프로필 추적 (폴백용)
            if (level >= p.minLevel && p.minLevel > bestLevel)
            {
                bestLevel = p.minLevel;
                result    = p;
            }
        }
        return result;
    }

    private static WeaponGrade RollGrade(DrawProfile p)
    {
        // 총합 기준으로 정규화해서 롤 — DB 합계가 100이 아니어도 안전
        float total = p.common + p.rare + p.epic + p.legendary + p.mythic;
        if (total <= 0f) return WeaponGrade.Common;

        float roll = Random.Range(0f, total);
        float acc  = 0f;
        acc += p.common;    if (roll < acc) return WeaponGrade.Common;
        acc += p.rare;      if (roll < acc) return WeaponGrade.Rare;
        acc += p.epic;      if (roll < acc) return WeaponGrade.Epic;
        acc += p.legendary; if (roll < acc) return WeaponGrade.Legendary;
        if (p.mythic > 0f)                  return WeaponGrade.Mythic;
        return WeaponGrade.Legendary;
    }

    private WeaponData PickByGrade(List<WeaponData> pool, WeaponGrade grade)
    {
        var candidates = pool.FindAll(w => w.grade == grade);
        if (candidates.Count > 0)
            return candidates[Random.Range(0, candidates.Count)];

        // 해당 등급 무기 없으면 한 단계씩 낮춰서 탐색
        int gradeInt = (int)grade - 1;
        while (gradeInt >= 0)
        {
            candidates = pool.FindAll(w => w.grade == (WeaponGrade)gradeInt);
            if (candidates.Count > 0)
                return candidates[Random.Range(0, candidates.Count)];
            gradeInt--;
        }
        return null;
    }
}
