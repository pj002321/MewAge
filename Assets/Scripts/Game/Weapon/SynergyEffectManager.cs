using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 장착 무기 변경 시 시너지 조건을 재계산하고 효과를 적용합니다.
/// </summary>
public class SynergyEffectManager : MonoBehaviourSingleton<SynergyEffectManager>
{
    [Header("시너지 스탯 보너스 (%)")]
    [SerializeField] private float meleeAtkBonus      = 20f;
    [SerializeField] private float spinMoveBonus      = 20f;
    [SerializeField] private float hammerDefBonus     = 25f;
    [SerializeField] private float boomerangCritBonus = 30f;
    [SerializeField] private float allDiffAspdBonus   = 20f;

    protected override bool DontDestroy => false;

    /// <summary>달성 집합이 바뀔 때마다 발행됩니다. SynergySlot이 구독합니다.</summary>
    public static event System.Action<HashSet<string>> OnAchievedChanged;

    /// <summary>새 시너지가 달성됐을 때만 발행됩니다. InfoButton UIShiny 등이 구독합니다.</summary>
    public static event System.Action OnNewSynergyAchieved;

    public static HashSet<string> AchievedSynergies { get; private set; } = new();

    private HashSet<string>          _previous    = new();
    private Dictionary<StatType, float> _appliedMods = new();

    // ── 라이프사이클 ──────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        WeaponSynergyFlags.Reset();
    }

    private void Start()
    {
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged += RecalculateSynergies;
            RecalculateSynergies(); // 초기 상태 반영
        }
    }

    private void OnDestroy()
    {
        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged -= RecalculateSynergies;

        WeaponSynergyFlags.Reset();
        RemoveAllStatMods();
    }

    // ── 핵심 계산 ─────────────────────────────────────────────────────

    public void RecalculateSynergies()
    {
        var equipped     = PlayerInventory.Shared?.Equipped;
        var newAchieved  = SynergyConditionChecker.CheckAll(equipped);

        if (newAchieved.SetEquals(_previous)) return;

        // 신규 달성 (이전에 없던 것)
        var newlyGained = new HashSet<string>(newAchieved);
        newlyGained.ExceptWith(_previous);

        // 1. 이전 스탯 모디파이어 제거
        RemoveAllStatMods();

        // 2. 플래그 리셋 후 재설정
        WeaponSynergyFlags.Reset();
        ApplyFlags(newAchieved);

        // 3. 패시브 스탯 적용
        ApplyPassiveStats(newAchieved);

        // 4. 신규 달성 일회성 효과
        foreach (var id in newlyGained)
            TriggerOneTimeEffect(id);

        _previous       = new HashSet<string>(newAchieved);
        AchievedSynergies = newAchieved;

        OnAchievedChanged?.Invoke(AchievedSynergies);

        if (newlyGained.Count > 0)
            OnNewSynergyAchieved?.Invoke();
    }

    // ── 스탯 적용/제거 ────────────────────────────────────────────────

    private void ApplyPassiveStats(HashSet<string> achieved)
    {
        var sm = PlayerStatManager.Shared;
        if (sm == null) return;

        sm.BeginBatch();
        if (achieved.Contains("SYN_MELEE4_ATK"))     AddMod(sm, StatType.ATK,       meleeAtkBonus);
        if (achieved.Contains("SYN_SPIN3_MOVE"))      AddMod(sm, StatType.MoveSpeed,  spinMoveBonus);
        if (achieved.Contains("SYN_HAMMER3_DEF"))     AddMod(sm, StatType.DEF,        hammerDefBonus);
        if (achieved.Contains("SYN_BOOMERANG3_CRIT")) AddMod(sm, StatType.CritDmg,    boomerangCritBonus);
        if (achieved.Contains("SYN_ALLDIFF_ASPD"))    AddMod(sm, StatType.AtkSpeed,   allDiffAspdBonus);
        sm.EndBatch();
    }

    private void AddMod(PlayerStatManager sm, StatType stat, float pct)
    {
        sm.AddPctMod(stat, pct);
        _appliedMods.TryGetValue(stat, out float cur);
        _appliedMods[stat] = cur + pct;
    }

    private void RemoveAllStatMods()
    {
        if (_appliedMods.Count == 0) return;
        var sm = PlayerStatManager.Shared;
        if (sm != null)
        {
            sm.BeginBatch();
            foreach (var kv in _appliedMods)
                sm.AddPctMod(kv.Key, -kv.Value);
            sm.EndBatch();
        }
        _appliedMods.Clear();
    }

    // ── 플래그 설정 ───────────────────────────────────────────────────

    private static void ApplyFlags(HashSet<string> achieved)
    {
        WeaponSynergyFlags.LegendProcActive      = achieved.Contains("SYN_ALL_LEGEND_PROC");
        WeaponSynergyFlags.MythReviveAvailable   = achieved.Contains("SYN_ALL_MYTH_REVIVE");
        WeaponSynergyFlags.RangeDoubleProjectile = achieved.Contains("SYN_RANGE4_PROJECTILE_X2");
        WeaponSynergyFlags.MagicSlowActive       = achieved.Contains("SYN_MAGIC3_SLOW");
    }

    // ── 일회성 효과 ───────────────────────────────────────────────────

    private static void TriggerOneTimeEffect(string synergyId)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        switch (synergyId)
        {
            case "SYN_ALL2_UPGRADE3":
                // 장착 무기 중 star < 3인 첫 번째 무기를 3성으로 강화
                for (int i = 0; i < inv.Equipped.Count; i++)
                {
                    var w = inv.Equipped[i];
                    if (w != null && w.star < 3)
                    {
                        inv.ReplaceEquipped(i, w.UpgradedTo(3));
                        break;
                    }
                }
                break;

            case "SYN_RARE4_LOWGRADE_UP":
                // 장착 무기 중 가장 낮은 성의 무기를 1성 강화
                int lowestStar = int.MaxValue;
                int lowestIdx  = -1;
                for (int i = 0; i < inv.Equipped.Count; i++)
                {
                    var w = inv.Equipped[i];
                    if (w != null && w.star < lowestStar) { lowestStar = w.star; lowestIdx = i; }
                }
                if (lowestIdx >= 0)
                {
                    var w = inv.Equipped[lowestIdx];
                    inv.ReplaceEquipped(lowestIdx, w.UpgradedTo(w.star + 1));
                }
                break;

            case "SYN_ALL3_HIGHEST_EXTRA":
                // 현재 최고등급의 1성 무기 사본을 장착 슬롯에 추가
                WeaponData highest = null;
                foreach (var w in inv.Equipped)
                    if (w != null && (highest == null || w.grade > highest.grade))
                        highest = w;
                if (highest != null)
                {
                    var extra = highest.UpgradedTo(1);
                    inv.ForceAddToInventory(extra);
                    inv.EquipFromInventory(inv.Inventory.Count - 1);
                }
                break;
        }
    }
}
