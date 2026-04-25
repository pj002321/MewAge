using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 유물 보유 현황·강화 레벨을 관리합니다.
/// DontDestroy=true로 홈→게임 씬 전환 후에도 유지됩니다.
/// </summary>
public class RelicInventory : MonoBehaviourSingleton<RelicInventory>
{
    protected override bool DontDestroy => true;

    public const int MaxLevel = 10;

    private const string LevelKeyPrefix = "RelicLvl_";

    // relicId → 강화 레벨
    private readonly Dictionary<string, int> _levels = new();

    // 보유 유물 목록 (런타임 캐시)
    private readonly List<RelicData> _owned = new();

    public static event System.Action OnChanged;

    // ── 초기화 ────────────────────────────────────────────────────────

    protected override void Awake()
    {
        base.Awake();
        Load();
    }

    private void Load()
    {
        _owned.Clear();
        _levels.Clear();

        DB_유물.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_ID)) return;

            string id    = row.f_ID;
            int    level = PlayerPrefs.GetInt(LevelKeyPrefix + id, 1);
            _levels[id]  = Mathf.Clamp(level, 1, MaxLevel);

            var data = RelicData.FromDB(row);
            if (data != null) _owned.Add(data);
        });
    }

    private void Save()
    {
        foreach (var kv in _levels)
            PlayerPrefs.SetInt(LevelKeyPrefix + kv.Key, kv.Value);
        PlayerPrefs.Save();
    }

    // ── 공개 API ──────────────────────────────────────────────────────

    public int GetLevel(string id)
        => _levels.TryGetValue(id, out var v) ? v : 1;

    public bool CanUpgrade(string id)
        => GetLevel(id) < MaxLevel;

    /// <summary>강화 레벨을 1 올립니다. 재료 검증은 호출부에서 처리.</summary>
    public bool TryUpgrade(string id)
    {
        if (!CanUpgrade(id)) return false;
        _levels[id] = GetLevel(id) + 1;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    /// <summary>현재 보유한 유물 전체를 반환합니다. (이번 프레임에서는 전부 장착으로 처리)</summary>
    public IEnumerable<RelicData> GetAllOwned() => _owned;
}
