using System;
using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 아웃게임 기물 컬렉션·강화·프리셋을 관리합니다.
/// DontDestroy=true — 홈 씬과 게임 씬에 걸쳐 유지됩니다.
///
/// 강화 규칙: 동일 기물 3개 소모 → ★ 상승 (최대 ★3)
/// 수치 공식: ★1=f_주수치, ★2=f_S2(없으면 ×1.5), ★3=f_S3(없으면 ×2.0)
/// 프리셋: 4슬롯, 인게임 시작 시 StructureInventory에 로드됨
/// </summary>
public class StructureWareManager : MonoBehaviourSingleton<StructureWareManager>
{
    // PlayerPrefs 키 접두사
    const string CntKey       = "WCnt_";
    const string StarKey      = "WStar_";
    const string PreKey       = "WPre_";
    const int    DismantleGold = 50;
    const int    MaxStar       = 3;
    const int    UpgradeCost   = 3;

    readonly Dictionary<string, int> _counts = new();
    readonly Dictionary<string, int> _stars  = new();
    readonly string[] _preset = new string[4];

    protected override bool DontDestroy => true;

    public event Action OnChanged;

    protected override void Awake()
    {
        base.Awake();
        Load();
        GiveStartingWares();
    }

    /// <summary>최초 1회: 구석기 C등급 기물 2개 지급</summary>
    void GiveStartingWares()
    {
        const string InitKey = "WareStartingGiven";
        if (PlayerPrefs.GetInt(InitKey, 0) == 1) return;

        int given = 0;
        DB_기물.ForEachEntity(row =>
        {
            if (given >= 2) return;
            if (!row.f_기물ID.StartsWith("C1_")) return;
            if (row.f_등급 != "NORMAL") return;
            AddWare(row.f_기물ID, 1);
            given++;
        });

        PlayerPrefs.SetInt(InitKey, 1);
        PlayerPrefs.Save();
    }

    // ── 조회 ────────────────────────────────────────────────────────

    public int    GetCount(string id)   => _counts.TryGetValue(id, out var v) ? v : 0;
    public int    GetStar(string id)    => _stars.TryGetValue(id, out var v)  ? v : 1;
    public string GetPreset(int slot)   => slot is >= 0 and < 4 ? _preset[slot] : null;
    public bool   CanUpgrade(string id) => GetCount(id) >= UpgradeCost && GetStar(id) < MaxStar;

    /// <summary>보유 수량이 1개 이상인 기물 ID 목록</summary>
    public IEnumerable<string> GetOwnedIds()
    {
        foreach (var kv in _counts)
            if (kv.Value > 0) yield return kv.Key;
    }

    // ── 수치 계산 ────────────────────────────────────────────────────

    /// <summary>★ 등급에 따른 최종 주수치를 반환합니다.</summary>
    public float GetMainValue(DB_기물 data, int? starOverride = null)
    {
        if (data == null) return 0f;
        int star = starOverride ?? GetStar(data.f_기물ID);
        return star switch
        {
            2 => data.f_S2 > 0 ? data.f_S2 : data.f_주수치 * 1.5f,
            3 => data.f_S3 > 0 ? data.f_S3 : data.f_주수치 * 2.0f,
            _ => data.f_주수치
        };
    }

    // ── 획득 ─────────────────────────────────────────────────────────

    public void AddWare(string id, int amount = 1)
    {
        if (string.IsNullOrEmpty(id)) return;
        _counts[id] = GetCount(id) + amount;
        if (!_stars.ContainsKey(id)) _stars[id] = 1;
        Save();
        OnChanged?.Invoke();
    }

    // ── 강화 ─────────────────────────────────────────────────────────

    /// <returns>성공 여부</returns>
    public bool TryUpgrade(string id)
    {
        if (!CanUpgrade(id)) return false;
        _counts[id] = GetCount(id) - UpgradeCost;
        if (_counts[id] <= 0) _counts.Remove(id);
        _stars[id] = GetStar(id) + 1;
        Save();
        OnChanged?.Invoke();
        return true;
    }

    // ── 분해 ─────────────────────────────────────────────────────────

    public void Dismantle(string id)
    {
        if (GetCount(id) <= 0) return;
        _counts[id]--;
        if (_counts[id] <= 0) _counts.Remove(id);
        PlayerStats.Shared?.AddGold(DismantleGold);
        Save();
        OnChanged?.Invoke();
    }

    // ── 프리셋 ───────────────────────────────────────────────────────

    public void SetPreset(int slot, string id)
    {
        if (slot < 0 || slot >= 4) return;

        // 동일 기물이 다른 슬롯에 있으면 그 슬롯을 비움 (교환)
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

    // ── 시너지 계산 ──────────────────────────────────────────────────

    /// <summary>현재 프리셋 기준 시너지 설명 문자열. 없으면 null.</summary>
    public string GetSynergyDescription()
    {
        var eraCnt = new Dictionary<string, int>();
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

    // ── 유틸 ─────────────────────────────────────────────────────────

    public static string EraOf(string id)     => id.Contains('_') ? id.Split('_')[0] : id;
    public static string EraName(string era)  => era switch
    {
        "C1" => "구석기", "C2" => "중석기", "C3" => "신석기",
        "C4" => "중세",   "C5" => "근대",   "C6" => "현대", "C7" => "우주",
        _ => era
    };

    public static Sprite LoadIcon(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        string chapter = EraOf(id);
        var prefab = Resources.Load<GameObject>($"Ware/{chapter}/{id.ToLower()}");
        return prefab?.GetComponentInChildren<SpriteRenderer>()?.sprite;
    }

    // ── 디버그 ───────────────────────────────────────────────────────

    [ContextMenu("테스트: 모든 기물 1개씩 추가")]
    void DebugAddAllWares()
    {
        DB_기물.ForEachEntity(row =>
        {
            if (!string.IsNullOrEmpty(row.f_기물ID))
                AddWare(row.f_기물ID, 1);
        });
        Debug.Log("[StructureWareManager] 모든 기물 1개씩 추가 완료");
    }

    [ContextMenu("테스트: 데이터 초기화")]
    void DebugClear()
    {
        _counts.Clear();
        _stars.Clear();
        for (int i = 0; i < 4; i++) _preset[i] = "";
        PlayerPrefs.DeleteAll();
        OnChanged?.Invoke();
        Debug.Log("[StructureWareManager] 초기화 완료");
    }

    // ── 저장/로드 ────────────────────────────────────────────────────

    void Save()
    {
        DB_기물.ForEachEntity(row =>
        {
            string id = row.f_기물ID;
            PlayerPrefs.SetInt(CntKey  + id, GetCount(id));
            PlayerPrefs.SetInt(StarKey + id, GetStar(id));
        });
        for (int i = 0; i < 4; i++)
            PlayerPrefs.SetString(PreKey + i, _preset[i] ?? "");
        PlayerPrefs.Save();
    }

    void Load()
    {
        DB_기물.ForEachEntity(row =>
        {
            string id   = row.f_기물ID;
            int    cnt  = PlayerPrefs.GetInt(CntKey  + id, 0);
            int    star = PlayerPrefs.GetInt(StarKey + id, 1);
            if (cnt > 0) { _counts[id] = cnt; _stars[id] = star; }
        });
        for (int i = 0; i < 4; i++)
            _preset[i] = PlayerPrefs.GetString(PreKey + i, "");
    }
}
