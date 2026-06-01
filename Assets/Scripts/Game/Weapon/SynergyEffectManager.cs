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

    /// <summary>새 시너지가 달성됐을 때만 발행됩니다. 신규 달성된 ID 목록을 전달합니다.</summary>
    public static event System.Action<IReadOnlyList<string>> OnNewSynergyAchieved;

    public static HashSet<string> AchievedSynergies { get; private set; } = new();

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        OnAchievedChanged    = null;
        OnNewSynergyAchieved = null;
        AchievedSynergies    = new HashSet<string>();
    }

    private HashSet<string>          _previous    = new();
    private Dictionary<StatType, float> _appliedMods = new();

    // ── 라이프사이클 ──────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        AchievedSynergies = new HashSet<string>();
        _previous         = new HashSet<string>();
        WeaponSynergyFlags.Reset();
        _all3OriginalData = null;
        _all3BoostedData  = null;
        _upgradeRecords.Clear();
    }

    private void Start()
    {
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged += RecalculateSynergies;
            RecalculateSynergies();
        }
        else
        {
            // PlayerInventory가 Start() 시점에 아직 없으면 한 프레임 후 재시도
            StartCoroutine(LateSubscribeRoutine());
        }

        _magicSlowRoutine = StartCoroutine(MagicSlowLoop());
    }

    private System.Collections.IEnumerator LateSubscribeRoutine()
    {
        yield return null;
        if (PlayerInventory.Shared != null)
        {
            PlayerInventory.Shared.OnChanged += RecalculateSynergies;
            RecalculateSynergies();
        }
    }

    private void OnDestroy()
    {
        StopAllCoroutines();
        if (PlayerInventory.Shared != null)
            PlayerInventory.Shared.OnChanged -= RecalculateSynergies;

        WeaponSynergyFlags.Reset();
        RemoveAllStatMods();

        // 씬 정리 시 부스트 ScriptableObject만 파괴 (슬롯 복구는 하지 않음)
        if (_all3BoostedData != null)
            Destroy(_all3BoostedData);
        _all3OriginalData = null;
        _all3BoostedData  = null;
        _upgradeRecords.Clear();
    }

    // SYN_MAGIC3_SLOW: 2.5초마다 현재 모든 적에게 둔화 적용
    private System.Collections.IEnumerator MagicSlowLoop()
    {
        var interval = new WaitForSeconds(2.5f);
        while (true)
        {
            yield return interval;
            if (!WeaponSynergyFlags.MagicSlowActive) continue;
            foreach (var e in EnemyController.All)
            {
                if (e != null && e.gameObject.activeInHierarchy)
                    e.ApplySlow(3f, 0.5f);
            }
        }
    }

    // ── 핵심 계산 ─────────────────────────────────────────────────────

    private bool      _recalculating;
    private bool      _recalcPending;
    private Coroutine _magicSlowRoutine;

    // SYN_ALL3_HIGHEST_EXTRA 임시 부스트 상태
    private WeaponData _all3OriginalData;
    private WeaponData _all3BoostedData;

    // SYN_ALL2_UPGRADE3 / SYN_RARE4_LOWGRADE_UP 업그레이드 기록 (되돌리기용)
    private readonly Dictionary<string, List<(int slot, string weaponName, int originalStar)>> _upgradeRecords = new();

    public void RecalculateSynergies()
    {
        if (_recalculating) { _recalcPending = true; return; }
        _recalculating = true;
        bool firstPass = true;
        try
        {
            do
            {
                _recalcPending = false;
                RecalculateSynergiesInternal(firstPass);
                firstPass = false;
            } while (_recalcPending);
        }
        finally { _recalculating = false; }
    }

    private void RecalculateSynergiesInternal(bool allowOneTimeEffects)
    {
        var equipped    = PlayerInventory.Shared?.Equipped;
        var newAchieved = SynergyConditionChecker.CheckAll(equipped);

        int equipCount = equipped?.Count ?? 0;
        string achievedStr = newAchieved.Count > 0 ? string.Join(", ", newAchieved) : "(없음)";
        Debug.Log($"[Synergy] 장착무기={equipCount}개 | 달성={achievedStr}");

        if (newAchieved.SetEquals(_previous)) return;

        // 신규 달성 (이전에 없던 것)
        var newlyGained = new HashSet<string>(newAchieved);
        newlyGained.ExceptWith(_previous);

        // 신규 손실 (이전에 있었으나 이제 없는 것)
        var newlyLost = new HashSet<string>(_previous);
        newlyLost.ExceptWith(newAchieved);

        // 1. 이전 스탯 모디파이어 제거
        RemoveAllStatMods();

        // 2. 플래그 리셋 후 재설정
        WeaponSynergyFlags.Reset();
        ApplyFlags(newAchieved);

        // 3. 패시브 스탯 적용
        ApplyPassiveStats(newAchieved);

        // 4. 시너지 손실 처리 (부스트 복구 등)
        foreach (var id in newlyLost)
            TriggerSynergyLost(id);

        // 5. 신규 달성 일회성 효과 (최초 패스에서만 — 효과로 인한 장착 변경이 새 시너지를 연쇄 발동하는 것을 막음)
        if (allowOneTimeEffects)
        {
            foreach (var id in newlyGained)
                TriggerOneTimeEffect(id);
        }

        _previous       = new HashSet<string>(newAchieved);
        AchievedSynergies = newAchieved;

        OnAchievedChanged?.Invoke(AchievedSynergies);

        if (newlyGained.Count > 0)
            OnNewSynergyAchieved?.Invoke(new List<string>(newlyGained));
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

    private void TriggerOneTimeEffect(string synergyId)
    {
        var inv = PlayerInventory.Shared;
        if (inv == null) return;

        switch (synergyId)
        {
            case "SYN_ALL2_UPGRADE3":
                // 장착 무기 중 star < 3인 첫 번째 무기를 3성으로 강화 (해제 시 복구)
                for (int i = 0; i < inv.Equipped.Count; i++)
                {
                    var w = inv.Equipped[i];
                    if (w != null && w.star < 3)
                    {
                        AddUpgradeRecord(synergyId, i, w.weaponName, w.star);
                        inv.ReplaceEquipped(i, w.UpgradedTo(3));
                        break;
                    }
                }
                break;

            case "SYN_RARE4_LOWGRADE_UP":
                // 장착 무기 중 가장 낮은 성의 무기를 1성 강화 (해제 시 복구)
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
                    AddUpgradeRecord(synergyId, lowestIdx, w.weaponName, w.star);
                    inv.ReplaceEquipped(lowestIdx, w.UpgradedTo(w.star + 1));
                }
                break;

            case "SYN_ALL3_HIGHEST_EXTRA":
            {
                // 장착된 2성 무기 중 랜덤 1개를 임시 3성으로 부스트 (시너지 해제 시 원래 성으로 복구)
                var candidates = new System.Collections.Generic.List<int>();
                for (int i = 0; i < inv.Equipped.Count; i++)
                {
                    var w = inv.Equipped[i];
                    if (w != null && w.star == 2) candidates.Add(i);
                }
                if (candidates.Count == 0) break;
                int chosen = candidates[UnityEngine.Random.Range(0, candidates.Count)];
                _all3OriginalData = inv.Equipped[chosen];
                var boosted = _all3OriginalData.UpgradedTo(3);
                boosted.price = _all3OriginalData.price;  // 판매가는 원래 2성 기준 유지
                _all3BoostedData = boosted;
                inv.ReplaceEquipped(chosen, boosted);
                break;
            }
        }
    }

    // ── 시너지 손실 처리 ──────────────────────────────────────────────

    private void TriggerSynergyLost(string synergyId)
    {
        switch (synergyId)
        {
            case "SYN_ALL3_HIGHEST_EXTRA":
                RevertAll3Boost();
                break;
            case "SYN_ALL2_UPGRADE3":
            case "SYN_RARE4_LOWGRADE_UP":
                RevertUpgradeRecords(synergyId);
                break;
        }
    }

    private void AddUpgradeRecord(string synergyId, int slot, string weaponName, int originalStar)
    {
        if (!_upgradeRecords.TryGetValue(synergyId, out var list))
            _upgradeRecords[synergyId] = list = new List<(int, string, int)>();
        list.Add((slot, weaponName, originalStar));
    }

    private void RevertUpgradeRecords(string synergyId)
    {
        if (!_upgradeRecords.TryGetValue(synergyId, out var records)) return;
        var inv = PlayerInventory.Shared;
        if (inv != null)
        {
            foreach (var (slotIdx, weaponName, originalStar) in records)
            {
                if (slotIdx >= inv.Equipped.Count) continue;
                var w = inv.Equipped[slotIdx];
                // 슬롯에 같은 이름의 무기가 있고 성급이 높은 경우에만 복구
                if (w != null && w.weaponName == weaponName && w.star > originalStar)
                    inv.ReplaceEquipped(slotIdx, w.UpgradedTo(originalStar));
            }
        }
        _upgradeRecords.Remove(synergyId);
    }

    private void RevertAll3Boost()
    {
        if (_all3OriginalData == null || _all3BoostedData == null) return;
        var inv = PlayerInventory.Shared;
        if (inv != null)
        {
            // 장착 슬롯에서 부스트된 무기를 참조로 탐색 (슬롯 이동에도 안전)
            bool reverted = false;
            for (int i = 0; i < inv.Equipped.Count && !reverted; i++)
            {
                if (inv.Equipped[i] == _all3BoostedData)
                {
                    inv.ReplaceEquipped(i, _all3OriginalData);
                    reverted = true;
                }
            }
            // 인벤토리로 이동된 경우
            if (!reverted)
                inv.ReplaceInInventory(_all3BoostedData, _all3OriginalData);
            // 판매된 경우: 두 탐색 모두 실패하면 아무것도 안 함 (판매가는 이미 원래 가격)
        }
        var toDestroy = _all3BoostedData;
        _all3OriginalData = null;
        _all3BoostedData  = null;
        if (toDestroy != null)
            Destroy(toDestroy);
    }
}
