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

    // 이전에 PlayerStatManager에 적용한 시너지 누계 (재계산 시 차감용)
    private float _prevSynergyAtk;
    private float _prevSynergyBleed;
    private float _prevSynergyCrit;

    protected override bool DontDestroy => false;

    protected override void Awake()
    {
        base.Awake();
        LoadWeaponsFromDB();
        LoadDrawProfiles();
    }

    private void Start()
    {
        // PlayerInventory 변경 시 시너지 적용
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged += ApplySynergyBonuses;
            ApplySynergyBonuses(); // 초기 적용
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged -= ApplySynergyBonuses;
        }
    }

    /// <summary>장착 무기 시너지 효과를 PlayerStatManager와 PlayerStats에 적용합니다.</summary>
    private void ApplySynergyBonuses()
    {
        var sm = PlayerStatManager.Shared;
        var ps = PlayerStats.Shared;

        // 이전에 적용한 시너지 보너스를 먼저 차감
        if (sm != null)
        {
            if (_prevSynergyAtk   != 0f) sm.AddPctMod(StatType.ATK,        -_prevSynergyAtk);
            if (_prevSynergyBleed != 0f) sm.AddPctMod(StatType.BleedPower, -_prevSynergyBleed);
            if (_prevSynergyCrit  != 0f) sm.AddPctMod(StatType.CritChance, -_prevSynergyCrit);
        }
        _prevSynergyAtk = _prevSynergyBleed = _prevSynergyCrit = 0f;

        // 시너지 계산
        var synergies = SynergyHelper.GetChapterSynergies(currentChapter);
        float totalExpPct = 0f;
        float totalGoldPct = 0f;

        foreach (var synergy in synergies)
        {
            int count = SynergyHelper.CountEquippedTag(synergy.Tag);
            // 시너지 효과 적용 (등급에 따라)
            if (count >= 1)
            {
                // 최소 1개: 동 등급
                ApplySynergyEffect(synergy, 0, sm, ref totalExpPct, ref totalGoldPct);
            }
            if (count >= 3)
            {
                // 3개: 은 등급 (동에 누적)
                ApplySynergyEffect(synergy, 1, sm, ref totalExpPct, ref totalGoldPct);
            }
            if (count >= 5)
            {
                // 5개: 금 등급 (동+은에 누적)
                ApplySynergyEffect(synergy, 2, sm, ref totalExpPct, ref totalGoldPct);
            }
        }

        // PlayerStats에 % 적용
        ps?.SetSynergyPct(totalGoldPct, totalExpPct);
    }

    private void ApplySynergyEffect(SynergyHelper.SynergyEntry synergy, int tierIndex, PlayerStatManager sm, ref float totalExpPct, ref float totalGoldPct)
    {
        // synergy.Description에서 값 파싱 (예: "공격력 증가\n동 +10%  /  은 +20%  /  금 +30%")
        var desc = synergy.Description;
        var lines = desc.Split('\n');
        if (lines.Length < 2) return;
        var values = lines[1].Split('/');
        if (values.Length <= tierIndex) return;
        var valueStr = values[tierIndex].Trim();
        // "+10%" 추출
        var pctStr = valueStr.Split('+')[1].Split('%')[0];
        if (float.TryParse(pctStr, out float pct))
        {
            if (synergy.Tag == WeaponSynergyTag.채집)
            {
                // 채집: 경험치 증가
                totalExpPct += pct;
            }
            else
            {
                // 다른 시너지: PlayerStatManager에 적용
                StatType stat = synergy.Tag switch
                {
                    WeaponSynergyTag.야수   => StatType.ATK,
                    WeaponSynergyTag.사냥꾼 => StatType.BleedPower,
                    WeaponSynergyTag.지혜   => StatType.CritChance,
                    _ => StatType.ATK
                };
                sm?.AddPctMod(stat, pct);
                switch (stat)
                {
                    case StatType.ATK:        _prevSynergyAtk   += pct; break;
                    case StatType.BleedPower: _prevSynergyBleed += pct; break;
                    case StatType.CritChance: _prevSynergyCrit  += pct; break;
                }
            }
        }
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
            // 장비 가챠 행(f_가챠타입이 설정된 행)은 무기 상점 드로우와 무관하므로 제외
            if (!string.IsNullOrEmpty(row.f_가챠타입)) return;
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

    private DrawProfile GetCurrentProfile(int level)
    {
        DrawProfile result = drawProfiles.Length > 0 ? drawProfiles[0] : default;
        foreach (var p in drawProfiles)
            if (level >= p.minLevel) result = p;
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
