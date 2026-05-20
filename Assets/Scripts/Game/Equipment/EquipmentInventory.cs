using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mewage.DB;

public enum EquipSortMode { None, ByGrade, ByCivilization, ByCategory }

/// <summary>
/// 장비 아이템 인벤토리 싱글톤.
/// 장착 슬롯 6개 — 카테고리별 고정 슬롯:
///   0=RING, 1=NECK, 2=FOOT, 3=TOP, 4=CLOAK, 5=HEAD
/// 보관함 무제한.
/// DontDestroy=true: 홈→게임 씬 유지.
/// 장착/해제 시 PlayerStatManager에 플랫 스탯 자동 적용.
/// </summary>
public class EquipmentInventory : MonoBehaviourSingleton<EquipmentInventory>
{
    public const int MaxEquipped       = 6;
    public const int GoldCostPerLevel  = 100;
    public const int MatCostPerLevel   = 10;

    // 인덱스 = (int)EquipCategory  (RING=0 … HEAD=5)
    private readonly EquipmentData[] equipped = new EquipmentData[MaxEquipped];
    private readonly List<EquipmentData> bag  = new();

    public System.Action OnChanged;

    // ── HomeGold (홈 씬 전용, PlayerPrefs 저장) ───────────────────────
    public int HomeGold
    {
        get => PlayerPrefs.GetInt("HomeGold", 0);
        private set { PlayerPrefs.SetInt("HomeGold", Mathf.Max(0, value)); PlayerPrefs.Save(); }
    }

    public void AddHomeGold(int amount) => HomeGold += amount;

    // ── 장비 레벨업 재료 ─────────────────────────────────────────────
    public int GetMatCount(EquipCategory cat) => PlayerPrefs.GetInt($"EquipMat_{cat}", 0);

    public void AddMat(EquipCategory cat, int amount)
    {
        PlayerPrefs.SetInt($"EquipMat_{cat}", Mathf.Max(0, GetMatCount(cat) + amount));
        PlayerPrefs.Save();
    }

    public static RewardType CategoryToMatType(EquipCategory cat) => cat switch
    {
        EquipCategory.RING  => RewardType.EquipRingMat,
        EquipCategory.NECK  => RewardType.EquipNeckMat,
        EquipCategory.FOOT  => RewardType.EquipFootMat,
        EquipCategory.TOP   => RewardType.EquipTopMat,
        EquipCategory.CLOAK => RewardType.EquipcloakMat,
        EquipCategory.HEAD  => RewardType.EquipHeadMat,
        _                   => RewardType.EquipRingMat,
    };

    // ── 장비 레벨업 ─────────────────────────────────────────────────
    public bool TryLevelUp(EquipmentData data)
    {
        if (data == null || data.level >= EquipmentData.MaxLevel) return false;
        if (HomeGold < GoldCostPerLevel) return false;
        if (GetMatCount(data.category) < MatCostPerLevel) return false;

        HomeGold -= GoldCostPerLevel;
        PlayerPrefs.SetInt($"EquipMat_{data.category}", GetMatCount(data.category) - MatCostPerLevel);
        data.level++;
        data.SaveLevel();
        PlayerPrefs.Save();
        RecalculateEquipStats();
        OnChanged?.Invoke();
        return true;
    }

    protected override bool DontDestroy => true;

    private const string StarterGivenKey = "StarterEquipGiven";
    private const string BagSaveKey      = "EquipBag";
    private const string SlotSaveKey     = "EquipSlot_";

    protected override void Awake()
    {
        base.Awake();
        Load();
    }

    // ── 저장 / 로드 ──────────────────────────────────────────────────
    private void Save()
    {
        // bag
        var bagIds = string.Join(",", bag.ConvertAll(d => d.equipId));
        PlayerPrefs.SetString(BagSaveKey, bagIds);

        // equipped slots
        for (int i = 0; i < MaxEquipped; i++)
            PlayerPrefs.SetString(SlotSaveKey + i, equipped[i]?.equipId ?? "");

        PlayerPrefs.Save();
    }

    private void Load()
    {
        bag.Clear();
        string bagStr = PlayerPrefs.GetString(BagSaveKey, "");
        if (!string.IsNullOrEmpty(bagStr))
        {
            foreach (var id in bagStr.Split(','))
            {
                if (string.IsNullOrEmpty(id)) continue;
                var data = EquipmentData.FromId(id);
                if (data != null) bag.Add(data);
            }
        }

        for (int i = 0; i < MaxEquipped; i++)
        {
            string id = PlayerPrefs.GetString(SlotSaveKey + i, "");
            equipped[i] = string.IsNullOrEmpty(id) ? null : EquipmentData.FromId(id);
        }

        // 첫 시작 시 구석기 NORMAL 장비 부위별 1개씩 바로 지급
        GiveStarterEquipmentIfNeeded();
    }

    public void ClearAll()
    {
        for (int i = 0; i < MaxEquipped; i++) equipped[i] = null;
        bag.Clear();
        Save();
    }

    // C1_S1 첫 클리어 시 GameClearPanel에서 호출 — 구석기 NORMAL 부위별 1개씩 지급
    public void GiveStarterEquipmentIfNeeded()
    {
        if (PlayerPrefs.GetInt(StarterGivenKey, 0) != 0) return;

        var givenCategories = new System.Collections.Generic.HashSet<EquipCategory>();
        DB_장비.ForEachEntity(row =>
        {
            if (givenCategories.Count >= 6) return;
            if (row == null || string.IsNullOrEmpty(row.f_장비ID)) return;

            var data = EquipmentData.FromDB(row);
            if (data == null) return;
            if (data.grade != EquipGrade.NORMAL) return;
            if (data.civilization != EquipCiv.구석기) return;
            if (givenCategories.Contains(data.category)) return;

            bag.Add(data);
            givenCategories.Add(data.category);
        });

        PlayerPrefs.SetInt(StarterGivenKey, 1);
        Save();
        OnChanged?.Invoke();
    }

    public IReadOnlyList<EquipmentData> Equipped => equipped;
    public IReadOnlyList<EquipmentData> Bag      => bag;

    // ── 카테고리 → 슬롯 인덱스 ────────────────────────────────────────
    public static int CategoryToSlot(EquipCategory cat) => (int)cat;

    // ── 보관함에 추가 ────────────────────────────────────────────────
    public bool TryAdd(EquipmentData data)
    {
        if (data == null) return false;
        bag.Add(data);
        Save();
        OnChanged?.Invoke();
        return true;
    }

    // ── 보관함 → 고정 슬롯 장착 ──────────────────────────────────────
    /// <summary>
    /// 카테고리에 맞는 슬롯에 장착합니다.
    /// 슬롯에 이미 아이템이 있으면 기존 아이템이 보관함으로 이동합니다.
    /// </summary>
    public void EquipFromBag(EquipmentData data)
    {
        int bagIndex = bag.IndexOf(data);
        if (bagIndex < 0) return;

        int slot      = CategoryToSlot(data.category);
        var displaced = equipped[slot];   // 기존 착용 아이템 (없으면 null)

        equipped[slot] = data;
        bag.RemoveAt(bagIndex);

        if (displaced != null)
            bag.Add(displaced);

        RecalculateEquipStats();
        Save();
        OnChanged?.Invoke();
    }

    // ── 장착 해제 → 보관함 ───────────────────────────────────────────
    public void UnequipToBag(EquipmentData data)
    {
        int slot = CategoryToSlot(data.category);
        if (equipped[slot] != data) return;

        bag.Add(equipped[slot]);
        equipped[slot] = null;

        RecalculateEquipStats();
        Save();
        OnChanged?.Invoke();
    }

    // ── 보관함에서 버리기 ────────────────────────────────────────────
    public void RemoveFromBag(EquipmentData data)
    {
        if (bag.Remove(data))
        {
            Save();
            OnChanged?.Invoke();
        }
    }

    // ── 정렬된 보관함 반환 ───────────────────────────────────────────
    public List<EquipmentData> GetSortedBag(EquipSortMode mode)
    {
        var sorted = new List<EquipmentData>(bag);
        switch (mode)
        {
            case EquipSortMode.ByGrade:
                sorted.Sort((a, b) => b.grade.CompareTo(a.grade));
                break;
            case EquipSortMode.ByCivilization:
                sorted.Sort((a, b) => a.civilization.CompareTo(b.civilization));
                break;
            case EquipSortMode.ByCategory:
                sorted.Sort((a, b) => a.category.CompareTo(b.category));
                break;
        }
        return sorted;
    }

    // ── 장착 스탯 → PlayerStatManager 적용 ────────────────────────────
    public void RecalculateEquipStats()
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return;

        sm.ResetEquipFlat();

        for (int i = 0; i < MaxEquipped; i++)
        {
            var e = equipped[i];
            if (e == null || string.IsNullOrEmpty(e.statId)) continue;
            var statType = EquipmentData.StatIdToType(e.statId);
            if (statType.HasValue)
                sm.SetEquipFlat(statType.Value,
                    sm.GetEquipFlat(statType.Value) + e.EffectiveStatValue);
        }
    }
}
