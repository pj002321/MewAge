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
        public float minLevel;
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
    private int          _currentWave;
    public  int          CurrentChapter => currentChapter;
    public  string       CurrentChapterID => currentChapterID;

    // 첫 구매 무기 변형 훅 (AUG_FIRST_HUNT 등에서 구독)
    public static event System.Func<WeaponData, WeaponData> OnModifyPurchasedWeapon;

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

        // 기본 무기는 항상 슬롯 0(우상단), 슬롯 2(좌상단)에 고정 배치
        int[] starterSlots = { 0, 2 };
        for (int i = 0; i < 2; i++)
        {
            var starter = normals[Random.Range(0, normals.Count)].UpgradedTo(1);
            if (inv.TryAddToInventory(starter))
            {
                inv.MoveInvenToEquip(0, starterSlots[i]);
                Debug.Log($"[UpgradeManager] 시작 무기 지급 {i + 1}: {starter.weaponName} → 슬롯 {starterSlots[i]}");
            }
        }
    }

    // ────────────────────────────────────────────────────────────────
    // 패널 열기 / 닫기
    // ────────────────────────────────────────────────────────────────
    public void ShowPanel(int wave)
    {
        _currentWave = wave;
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

        // 이미 판매된 카드 재구매 방지
        if (index >= 0 && index < isCardSold.Length && isCardSold[index]) return;
        if (index == 3 && isAdCardSold) return;

        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        // 인벤이 꽉 찬 경우: 슬롯 확보 시도
        if (inv.InventoryFull)
        {
            bool hasSamePartner = inv.CountAll(data.weaponName, data.star) >= 1;
            if (!hasSamePartner)
            {
                // 직접 파트너 없음 → 기존 합성 쌍으로 슬롯 1개 확보
                if (!TryFreeOneInventorySlot(inv))
                {
                    upgradePanel.ShowNotification("인벤토리가 가득 찼습니다.");
                    return;
                }
                // 슬롯 확보 성공 → 아래 정상 추가 경로로 진행
            }
            // hasSamePartner인 경우: ForceAdd → AutoMerge(data) 경로
        }

        var stats = PlayerStats.Shared;
        if (stats == null || stats.Gold < data.price)
        {
            upgradePanel.ShowNotification("골드가 부족합니다.");
            return;
        }

        stats.SpendGold(data.price);

        // 인벤토리에는 독립 복사본을 넣어야 한다.
        // data == currentDraw[index] 원본을 그대로 넣으면 AutoMerge에서 소모·Destroy 시
        // currentDraw[index]가 파괴되어 RefreshPanel에서 카드가 사라지는 문제가 생긴다.
        var invCopy = data.UpgradedTo(data.star);
        if (OnModifyPurchasedWeapon != null)
            invCopy = OnModifyPurchasedWeapon(invCopy) ?? invCopy;

        // 인벤 꽉 찬 상태(직접 파트너 있는 경우)라면 ForceAdd → AutoMerge로 슬롯 확보
        if (inv.InventoryFull)
            inv.ForceAddToInventory(invCopy);
        else if (!inv.TryAddToInventory(invCopy))
        {
            stats.AddGold(data.price); // 환불
            Destroy(invCopy);
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

        // 빈 장착 슬롯이 있으면 구매 즉시 장착
        if (inv.HasEmptyEquipSlot())
        {
            for (int i = 0; i < inv.Inventory.Count; i++)
            {
                if (inv.Inventory[i] == invCopy) { inv.EquipFromInventory(i); break; }
            }
        }

        // 자동 합성: 인벤+장착 전체 스캔 후 가능한 모든 조합 합성
        TryAutoMergeAll();
    }

    /// <summary>인벤에 합성 가능한 쌍이 있으면 하나 합성해서 슬롯 1개를 확보합니다.</summary>
    private bool TryFreeOneInventorySlot(PlayerInventory inv)
    {
        // 인벤 내 종류별 개수 집계 (인벤에 최소 1개 있어야 합성 시 슬롯이 실제로 줄어듦)
        var invCount = new System.Collections.Generic.Dictionary<(string, int), int>();
        foreach (var w in inv.Inventory)
        {
            if (w == null) continue;
            var key = (w.weaponName, w.star);
            invCount.TryGetValue(key, out int c);
            invCount[key] = c + 1;
        }

        foreach (var kvp in invCount)
        {
            if (kvp.Key.Item2 >= 3) continue; // 3★ 합성은 수동 전용
            var (name, star) = kvp.Key;
            if (inv.CountAll(name, star) < 2) continue;

            var result = GetNextGradeWeapon(name, star);
            if (result == null) continue;
            if (!inv.TryMergeAll(name, star, result)) continue;
            SoundManager.Shared?.Play(SoundKey.UI_Merge);
            return true;
        }
        return false;
    }

    private void AutoMerge(PlayerInventory inv, string weaponName, int star)
    {
        while (star < 3 && inv.CanMergeAll(weaponName, star))
        {
            WeaponData result = GetNextGradeWeapon(weaponName, star);
            if (result == null) break;
            if (!inv.TryMergeAll(weaponName, star, result)) break;
            SoundManager.Shared?.Play(SoundKey.UI_Merge);
            weaponName = result.weaponName;
            star       = result.star;
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
                if (kvp.Value < 2 || kvp.Key.Item2 >= 3) continue; // 3★ 합성은 수동 전용
                var (name, star) = kvp.Key;
                WeaponData result = GetNextGradeWeapon(name, star);
                if (result == null) continue;
                if (!inv.TryMergeAll(name, star, result)) continue;
                SoundManager.Shared?.Play(SoundKey.UI_Merge);
                anyMerged = true;
                break;
            }
        } while (anyMerged);

        RefreshPanel();
    }

    // ────────────────────────────────────────────────────────────────
    // 수동 그랜드 머지 (3★+3★ → 상위등급 1★)
    // ────────────────────────────────────────────────────────────────
    public bool TryGrandMerge(string weaponName)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null || !inv.CanMergeAll(weaponName, 3)) return false;

        WeaponData result  = GetNextGradeWeapon(weaponName, 3);
        if (result == null) return false;

        WeaponData doubleUp = TryRollDoubleUp(result);
        WeaponData actual   = doubleUp ?? result;
        if (!inv.TryMergeAll(weaponName, 3, actual,
            displayResult:  doubleUp != null ? result : null,
            isDoubleUp:     doubleUp != null,
            doubleUpResult: doubleUp)) return false;

        SoundManager.Shared?.Play(SoundKey.UI_Merge);
        TryAutoMergeAll(); // 결과물의 하위 성급 cascade 처리
        return true;
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

        if (inv.InventoryFull)
        {
            bool hasSamePartner = inv.CountAll(adCardData.weaponName, adCardData.star) >= 1;
            if (!hasSamePartner && !TryFreeOneInventorySlot(inv))
            {
                upgradePanel.ShowNotification("인벤토리가 가득 찼습니다.");
                return;
            }
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

        // 빈 장착 슬롯이 있으면 구매 즉시 장착
        if (inv.HasEmptyEquipSlot())
        {
            for (int i = 0; i < inv.Inventory.Count; i++)
            {
                if (inv.Inventory[i] == adCardData) { inv.EquipFromInventory(i); break; }
            }
        }

        // 자동 합성: 인벤+장착 전체 스캔 후 가능한 모든 조합 합성
        TryAutoMergeAll();
    }

    // ────────────────────────────────────────────────────────────────
    // 합성
    // ────────────────────────────────────────────────────────────────
    /// <summary>합성 결과 무기를 반환합니다. 1★/2★ → 같은 무기 다음 성, 3★ → 상위등급 랜덤 무기 1★.</summary>
    public WeaponData GetNextGradeWeapon(string weaponName, int star)
    {
        if (star <= 0 || star > 3) return null;
        if (star == 3)
        {
            WeaponGrade baseGrade = WeaponGrade.Common;
            foreach (var w in allWeapons)
                if (w != null && w.weaponName == weaponName) { baseGrade = w.grade; break; }
            return GetRandomHigherGradeWeapon(baseGrade);
        }
        foreach (var w in allWeapons)
            if (w != null && w.weaponName == weaponName)
                return w.UpgradedTo(star + 1);
        return null;
    }

    /// <summary>현재 챕터에서 baseGrade 한 단계 위 등급의 무기 1개를 랜덤 반환합니다.</summary>
    private WeaponData GetRandomHigherGradeWeapon(WeaponGrade baseGrade)
    {
        WeaponGrade target = (WeaponGrade)((int)baseGrade + 1);
        var candidates = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.chapter == currentChapter && w.grade == target)
                candidates.Add(w);
        if (candidates.Count == 0) // 다음 등급 없으면 동일 등급 폴백
            foreach (var w in allWeapons)
                if (w != null && w.chapter == currentChapter && w.grade == baseGrade)
                    candidates.Add(w);
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)].UpgradedTo(1);
    }

    /// <summary>true 시 더블업 머지 100% 발동 (디버그 전용).</summary>
    public static bool ForceDoubleUp;

    /// <summary>1% 확률로 더블업 머지를 발동합니다. 발동 시 grade+2 무기 반환, 실패 시 null.</summary>
    private WeaponData TryRollDoubleUp(WeaponData grandResult)
    {
        if (grandResult == null || (!ForceDoubleUp && Random.value > 0.01f)) return null;
        WeaponGrade target = (WeaponGrade)((int)grandResult.grade + 1);
        var candidates = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.chapter == currentChapter && w.grade == target)
                candidates.Add(w);
        if (candidates.Count == 0) return null;
        return candidates[Random.Range(0, candidates.Count)].UpgradedTo(1);
    }

    /// <summary>그랜드 머지 사이클링 연출에 사용할 한 단계 위 등급 무기 목록을 반환합니다.</summary>
    public List<WeaponData> GetGrandMergeCandidates(WeaponGrade baseGrade)
    {
        WeaponGrade target = (WeaponGrade)((int)baseGrade + 1);
        var result = new List<WeaponData>();
        foreach (var w in allWeapons)
            if (w != null && w.chapter == currentChapter && w.grade == target)
                result.Add(w);
        return result;
    }

    /// <summary>인벤토리에서 같은 이름+성 3개를 합성합니다.</summary>
    public bool TryMerge(string weaponName, int star)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return false;

        if (star > 3)
        {
            upgradePanel.ShowNotification("최대 성급은 합성할 수 없습니다.");
            return false;
        }

        if (!inv.CanMergeAll(weaponName, star))
        {
            upgradePanel.ShowNotification("같은 무기 같은 성급이 2개 필요합니다.");
            return false;
        }

        WeaponData result = GetNextGradeWeapon(weaponName, star);
        if (result == null)
        {
            upgradePanel.ShowNotification($"DB에 {weaponName} 항목이 없습니다.");
            return false;
        }

        WeaponData doubleUp = star >= 3 ? TryRollDoubleUp(result) : null;
        WeaponData actual   = doubleUp ?? result;
        if (!inv.TryMergeAll(weaponName, star, actual,
            displayResult: doubleUp != null ? result : null,
            isDoubleUp: doubleUp != null,
            doubleUpResult: doubleUp)) return false;

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
            if (picked != null) { result[i] = picked.UpgradedTo(picked.star); pool.Remove(picked); }
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
        DrawProfile result    = drawProfiles[0];
        float       bestLevel = float.MinValue;
        foreach (var p in drawProfiles)
        {
            if (p.minLevel < result.minLevel)
                result = p;
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
