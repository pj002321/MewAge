using System;
using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 아웃게임 기물 컬렉션·강화·합성·프리셋을 관리합니다.
/// DontDestroy=true — 홈 씬과 게임 씬에 걸쳐 유지됩니다.
///
/// 강화 규칙: 카드 수집으로 레벨업 (최대 Lv.20)
///   Lv.1-5  : 2장/레벨
///   Lv.6-10 : 3장/레벨
///   Lv.11-15: 4장/레벨
///   Lv.16-19: 5장/레벨
/// 수치 공식: 주수치 × (1 + (레벨-1) × 0.15) → Lv.20 기준 ×3.85
/// 합성 (만렙 잉여): 3장→다음시대 1개, 5장→2개, 10장→5개
/// 프리셋: 4슬롯, 인게임 시작 시 StructureInventory에 로드됨
/// </summary>
public class StructureWareManager : MonoBehaviourSingleton<StructureWareManager>
{
    const string CntKey        = "WCnt_";
    const string LevelKey      = "WLvl_";
    const string PreKey        = "WPre_";
    const int    DismantleGold = 50;
    public const int MaxLevel  = 20;

    readonly Dictionary<string, int> _counts = new();
    readonly Dictionary<string, int> _levels  = new();
    readonly string[] _preset = new string[4];

    protected override bool DontDestroy => true;

    public event Action OnChanged;

    protected override void Awake()
    {
        base.Awake();
        Load();
        GiveStartingWares();
    }

    /// <summary>최초 1회: 구석기 기물 4개 지급 + 프리셋 0~3 자동 세팅</summary>
    void GiveStartingWares()
    {
        const string InitKey = "WareStartingGiven";
        if (PlayerPrefs.GetInt(InitKey, 0) == 1) return;

        var given = new System.Collections.Generic.List<string>();
        DB_기물.ForEachEntity(row =>
        {
            if (given.Count >= 4) return;
            if (!row.f_기물ID.StartsWith("C1_")) return;
            AddWareInternal(row.f_기물ID, 1);
            given.Add(row.f_기물ID);
        });

        // 프리셋 자동 세팅
        for (int i = 0; i < given.Count && i < 4; i++)
            _preset[i] = given[i];

        Save();
        OnChanged?.Invoke();
        PlayerPrefs.SetInt(InitKey, 1);
        PlayerPrefs.Save();
    }

    // ── 조회 ─────────────────────────────────────────────────────────

    public int    GetCount(string id) => _counts.TryGetValue(id, out var v) ? v : 0;
    public int    GetLevel(string id) => _levels.TryGetValue(id, out var v) ? v : 1;
    public string GetPreset(int slot) => slot is >= 0 and < 4 ? _preset[slot] : null;

    /// <summary>다음 레벨까지 필요한 카드 수 (만렙이면 0)</summary>
    public static int GetUpgradeCost(int currentLevel)
    {
        if (currentLevel >= MaxLevel) return 0;
        if (currentLevel < 6)  return 2;
        if (currentLevel < 11) return 3;
        if (currentLevel < 16) return 4;
        return 5;
    }

    public bool CanUpgrade(string id)
    {
        int lvl = GetLevel(id);
        return lvl < MaxLevel && GetCount(id) >= GetUpgradeCost(lvl);
    }

    /// <summary>3장 이상 보유 시 합성 가능 (만렙 조건 없음)</summary>
    public bool CanSynthesize(string id, int inputCount = 3)
        => GetCount(id) >= inputCount;

    /// <summary>보유 수량 > 0 이거나 레벨이 1 초과인 기물 (일부라도 강화된 기물) ID 목록</summary>
    public IEnumerable<string> GetOwnedIds()
    {
        var seen = new HashSet<string>();
        foreach (var kv in _counts)
            if (kv.Value > 0 && seen.Add(kv.Key)) yield return kv.Key;
        foreach (var kv in _levels)
            if (kv.Value > 1 && seen.Add(kv.Key)) yield return kv.Key;
    }

    // ── 수치 계산 ─────────────────────────────────────────────────────

    /// <summary>
    /// 레벨에 따른 최종 주수치를 반환합니다.
    /// Lv.1=×1.0, Lv.10=×2.35, Lv.20=×3.85
    /// </summary>
    public float GetMainValue(DB_기물 data, int? levelOverride = null)
    {
        if (data == null) return 0f;
        int   level      = levelOverride ?? GetLevel(data.f_기물ID);
        float multiplier = 1f + (level - 1) * 0.15f;
        return data.f_주수치 * multiplier;
    }

    // ── 획득 ──────────────────────────────────────────────────────────

    public void AddWare(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id)) return;
        AddWareInternal(id, amount);
        Save();
        OnChanged?.Invoke();
    }

    void AddWareInternal(string id, int amount)
    {
        _counts[id] = GetCount(id) + amount;
        if (!_levels.ContainsKey(id)) _levels[id] = 1;
    }

    // ── 강화 ──────────────────────────────────────────────────────────

    /// <returns>성공 여부</returns>
    public bool TryUpgrade(string id)
    {
        if (!CanUpgrade(id)) return false;
        int cost = GetUpgradeCost(GetLevel(id));
        _counts[id] = GetCount(id) - cost;
        if (_counts[id] <= 0) _counts.Remove(id);
        _levels[id] = GetLevel(id) + 1;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    // ── 합성 ──────────────────────────────────────────────────────────

    /// <summary>
    /// 같은 시대 카드 목록을 소모해 다음 시대 기물을 획득합니다.
    /// materialIds: 합성에 사용할 카드 ID 목록 (중복 허용, 같은 시대여야 함)
    /// 3장→1개, 5장→2개, 10장→5개
    /// 반환값: (결과 기물ID, 수량) 리스트. 실패 시 null.
    /// </summary>
    public List<(string id, int count)> TrySynthesize(List<string> materialIds)
    {
        if (materialIds == null || materialIds.Count < 3) return null;

        // 시대 일치 확인
        string targetEra = EraOf(materialIds[0]);
        foreach (var mid in materialIds)
            if (EraOf(mid) != targetEra) return null;

        // 보유 수량 확인 (같은 ID가 여러 장 선택됐을 때)
        var useCounts = new Dictionary<string, int>();
        foreach (var mid in materialIds)
            useCounts[mid] = useCounts.TryGetValue(mid, out int c) ? c + 1 : 1;
        foreach (var kv in useCounts)
            if (GetCount(kv.Key) < kv.Value) return null;

        int    n           = materialIds.Count;
        int    outputCount = n >= 10 ? 5 : n >= 5 ? 2 : 1;
        string nextEra     = NextEra(targetEra);

        // 결과 풀 확인 — 카드 소모 전에 검증해야 카드가 증발하지 않음
        var pool = new List<string>();
        DB_기물.ForEachEntity(row =>
        {
            if (!string.IsNullOrEmpty(row.f_기물ID) && EraOf(row.f_기물ID) == nextEra)
                pool.Add(row.f_기물ID);
        });
        // C7이면 같은 시대에서 폴백 — 입력이 전부 LEGEND면 S등급만 추출
        if (pool.Count == 0 && targetEra == "C7")
        {
            bool allLegend = true;
            foreach (var mid in materialIds)
            {
                var d = DB_기물.FindEntity(r => r.f_기물ID == mid);
                if (d == null || d.f_등급 != "LEGEND") { allLegend = false; break; }
            }

            string gradeFilter = allLegend ? "S" : null;
            DB_기물.ForEachEntity(row =>
            {
                if (string.IsNullOrEmpty(row.f_기물ID)) return;
                if (EraOf(row.f_기물ID) != targetEra) return;
                if (gradeFilter != null && row.f_등급 != gradeFilter) return;
                pool.Add(row.f_기물ID);
            });

            // S등급 DB 데이터가 아직 없으면 LEGEND 전체로 폴백
            if (pool.Count == 0 && allLegend)
            {
                DB_기물.ForEachEntity(row =>
                {
                    if (!string.IsNullOrEmpty(row.f_기물ID) && EraOf(row.f_기물ID) == targetEra)
                        pool.Add(row.f_기물ID);
                });
            }
        }
        if (pool.Count == 0)
        {
            Debug.LogWarning($"[StructureWareManager] {EraName(nextEra)} 시대 기물이 DB에 없어 합성 불가. 카드 유지.");
            return null;
        }

        // 카드 소모 (풀 확인 통과 후)
        foreach (var kv in useCounts)
        {
            _counts[kv.Key] = GetCount(kv.Key) - kv.Value;
            if (_counts[kv.Key] <= 0) _counts.Remove(kv.Key);
        }

        var results = new List<(string, int)>();
        for (int i = 0; i < outputCount && pool.Count > 0; i++)
        {
            string picked = pool[UnityEngine.Random.Range(0, pool.Count)];
            AddWareInternal(picked, 1);

            int idx = results.FindIndex(r => r.Item1 == picked);
            if (idx >= 0) results[idx] = (picked, results[idx].Item2 + 1);
            else          results.Add((picked, 1));
        }

        Save();
        OnChanged?.Invoke();
        return results;
    }

    // ── 분해 ──────────────────────────────────────────────────────────

    public void Dismantle(string id)
    {
        if (GetCount(id) <= 0) return;
        _counts[id]--;
        if (_counts[id] <= 0) _counts.Remove(id);
        PlayerStats.Shared?.AddGold(DismantleGold);
        Save();
        OnChanged?.Invoke();
    }

    // ── 프리셋 ────────────────────────────────────────────────────────

    public void SetPreset(int slot, string id)
    {
        if (slot < 0 || slot >= 4) return;

        if (!string.IsNullOrEmpty(id))
        {
            for (int i = 0; i < 4; i++)
            {
                if (i != slot && _preset[i] == id)
                    _preset[i] = null;
            }
        }

        _preset[slot] = id;
        Save();
        OnChanged?.Invoke();
    }

    public void ClearPreset(int slot) => SetPreset(slot, null);

    // ── 시너지 계산 ───────────────────────────────────────────────────

    /// <summary>현재 프리셋 기준 역할 태그 시너지 설명 목록 (3개 이상 동일 태그)</summary>
    public List<string> GetRoleTagSynergies()
    {
        var tagCnt = BuildPresetTagCount();
        var list   = new List<string>();
        foreach (var kv in tagCnt)
            if (kv.Value >= 3) list.Add(WareSynergySystem.GetSynergyDescription(kv.Key, kv.Value));
        return list;
    }

    /// <summary>현재 프리셋 기준 시대 시너지 설명 문자열. 없으면 null.</summary>
    public string GetEraSynergyDescription()
    {
        var eraCnt  = new Dictionary<string, int>();
        foreach (var id in _preset)
        {
            if (string.IsNullOrEmpty(id)) continue;
            string era = EraOf(id);
            eraCnt[era] = eraCnt.TryGetValue(era, out int c) ? c + 1 : 1;
        }

        int variety = eraCnt.Count;
        foreach (var kv in eraCnt)
        {
            if (kv.Value >= 4) return $"{EraName(kv.Key)} 4개 → 쿨타임 -30% · 수치 +20%";
            if (kv.Value >= 2) return $"{EraName(kv.Key)} 2개 → 쿨타임 -15%";
        }
        if (variety >= 4) return "시대의 수렴 → 전체 효과 +50%";
        if (variety >= 3) return "3시대 혼합 → 전역 쿨타임 -20%";
        if (variety >= 2) return "2시대 혼합 → 체력 회복";
        return null;
    }

    /// <summary>역할 태그별 프리셋 장착 수 반환</summary>
    public Dictionary<string, int> BuildPresetTagCount()
    {
        var tagCnt = new Dictionary<string, int>();
        foreach (var id in _preset)
        {
            if (string.IsNullOrEmpty(id)) continue;
            var data = DB_기물.FindEntity(r => r.f_기물ID == id);
            string tag = data?.f_역할;
            if (string.IsNullOrEmpty(tag)) continue;
            tagCnt[tag] = tagCnt.TryGetValue(tag, out int c) ? c + 1 : 1;
        }
        return tagCnt;
    }

    // ── 유틸 ──────────────────────────────────────────────────────────

    public static string EraOf(string id)    => id.Contains('_') ? id.Split('_')[0] : id;
    public static string EraName(string era) => era switch
    {
        "C1" => "구석기", "C2" => "중석기", "C3" => "신석기",
        "C4" => "중세",   "C5" => "근대",   "C6" => "현대", "C7" => "우주",
        _ => era
    };

    public static string NextEra(string era) => era switch
    {
        "C1" => "C2", "C2" => "C3", "C3" => "C4",
        "C4" => "C5", "C5" => "C6", "C6" => "C7",
        _    => era
    };

    /// <summary>기물 아이콘 로드. Resources/Ware/{chapter}/{id}_icon 스프라이트를 반환합니다.</summary>
    public static Sprite LoadIcon(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string chapter = EraOf(id);
        return Resources.Load<Sprite>($"Ware/{chapter}/{id.ToLower()}_icon");
    }

    // ── 디버그 ────────────────────────────────────────────────────────

    [ContextMenu("테스트: 모든 기물 1개씩 추가")]
    void DebugAddAllWares()
    {
        DB_기물.ForEachEntity(row =>
        {
            if (!string.IsNullOrEmpty(row.f_기물ID))
                AddWareInternal(row.f_기물ID, 1);
        });
        Save();
        OnChanged?.Invoke();
        Debug.Log("[StructureWareManager] 모든 기물 1개씩 추가 완료");
    }

    [ContextMenu("테스트: 데이터 초기화")]
    void DebugClear()
    {
        _counts.Clear();
        _levels.Clear();
        for (int i = 0; i < 4; i++) _preset[i] = "";
        PlayerPrefs.DeleteAll();
        OnChanged?.Invoke();
        Debug.Log("[StructureWareManager] 초기화 완료");
    }

    // ── 저장/로드 ─────────────────────────────────────────────────────

    void Save()
    {
        DB_기물.ForEachEntity(row =>
        {
            string id = row.f_기물ID;
            PlayerPrefs.SetInt(CntKey   + id, GetCount(id));
            PlayerPrefs.SetInt(LevelKey + id, GetLevel(id));
        });
        for (int i = 0; i < 4; i++)
            PlayerPrefs.SetString(PreKey + i, _preset[i] ?? "");
        PlayerPrefs.Save();
    }

    void Load()
    {
        DB_기물.ForEachEntity(row =>
        {
            string id    = row.f_기물ID;
            int    cnt   = PlayerPrefs.GetInt(CntKey   + id, 0);
            int    level = PlayerPrefs.GetInt(LevelKey + id, 1);
            if (cnt > 0)  _counts[id] = cnt;
            if (level > 1) _levels[id] = level;
        });
        var seen = new System.Collections.Generic.HashSet<string>();
        for (int i = 0; i < 4; i++)
        {
            string id = PlayerPrefs.GetString(PreKey + i, "");
            if (!string.IsNullOrEmpty(id) && !seen.Add(id))
                id = ""; // 중복 프리셋 제거
            _preset[i] = id;
        }
    }
}
