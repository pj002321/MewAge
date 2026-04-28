using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 유물 보유 현황·조각·강화 레벨을 관리합니다.
/// DontDestroy=true로 홈→게임 씬 전환 후에도 유지됩니다.
///
/// 레벨·등급 구조 (조각 누적량 기준):
///   Lv1(10)  Lv2(30)  → 일반 (Common)
///   Lv3(50)  Lv4(80)  → 고급 (Rare)
///   Lv5(100) Lv6(150) → 희귀 (Epic)
///   Lv7(200) Lv8(350) → 전설 (Legendary)
///   Lv9(500) Lv10(800)→ 신화 (Mythic)
/// </summary>
public class RelicInventory : MonoBehaviourSingleton<RelicInventory>
{
    protected override bool DontDestroy => true;

    public const int MaxLevel = 10;

    // ── 조각 임계값 (인덱스 i → Lv i+1 해금) ────────────────────────────
    public static readonly int[] FragThresholds = { 10, 30, 50, 80, 100, 150, 200, 350, 500, 800 };

    // ── 등급 (레벨 2개 묶음 = 1등급) ─────────────────────────────────────
    public static readonly string[] GradeLabels =
    {
        "일반", "일반",   // Lv1-2
        "고급", "고급",   // Lv3-4
        "희귀", "희귀",   // Lv5-6
        "전설", "전설",   // Lv7-8
        "신화", "신화",   // Lv9-10
    };
    public static readonly Color[] GradeColors =
    {
        new Color(0.80f, 0.80f, 0.80f), new Color(0.80f, 0.80f, 0.80f), // 일반
        new Color(0.30f, 0.60f, 1.00f), new Color(0.30f, 0.60f, 1.00f), // 고급
        new Color(0.70f, 0.20f, 1.00f), new Color(0.70f, 0.20f, 1.00f), // 희귀
        new Color(1.00f, 0.80f, 0.10f), new Color(1.00f, 0.80f, 0.10f), // 전설
        new Color(1.00f, 0.30f, 0.20f), new Color(1.00f, 0.30f, 0.20f), // 신화
    };

    private const string FragKeyPrefix = "RelicFrag_";
    private const string StarKeyPrefix = "RelicStar_";

    private readonly Dictionary<string, int> _fragments  = new();
    private readonly Dictionary<string, int> _starLevels = new();
    private readonly List<RelicData>         _all        = new();

    public static event System.Action OnChanged;

    // ── 초기화 ────────────────────────────────────────────────────────────
    protected override void Awake()
    {
        base.Awake();
        Load();
    }

    private void Load()
    {
        _all.Clear();
        _fragments.Clear();

        var seen = new System.Collections.Generic.HashSet<string>();
        DB_유물.ForEachEntity(row =>
        {
            if (row == null || string.IsNullOrEmpty(row.f_ID) || string.IsNullOrEmpty(row.f_이름)) return;
            if (!seen.Add(row.f_ID)) return;   // 중복 ID 방지
            var data = RelicData.FromDB(row);
            if (data == null) return;
            _all.Add(data);
            _fragments[data.relicId]  = PlayerPrefs.GetInt(FragKeyPrefix + data.relicId, 0);
            _starLevels[data.relicId] = PlayerPrefs.GetInt(StarKeyPrefix + data.relicId, 1);
        });
    }

    // ── 공개 조회 API ─────────────────────────────────────────────────────
    /// <summary>잠금 포함 전체 유물 목록 (UI 표시용).</summary>
    public IReadOnlyList<RelicData> GetAll() => _all;

    /// <summary>해금된 유물만 반환 (인게임 스탯 적용용).</summary>
    public IEnumerable<RelicData> GetAllOwned() => _all.Where(r => IsUnlocked(r.relicId));

    public int  GetFragments(string id)   => _fragments.TryGetValue(id, out var v) ? v : 0;
    [SerializeField] private bool debugAllUnlocked = true;
    public bool IsUnlocked(string id)     => debugAllUnlocked || GetLevel(id) > 0;

    /// <summary>조각 수 → 레벨 (0=잠금, 1~10).</summary>
    public int GetLevel(string id)
    {
        if (debugAllUnlocked) return Mathf.Max(1, GetFragmentLevel(id));
        return GetFragmentLevel(id);
    }

    private int GetFragmentLevel(string id)
    {
        int frags = GetFragments(id);
        int level = 0;
        for (int i = 0; i < FragThresholds.Length; i++)
        {
            if (frags >= FragThresholds[i]) level = i + 1;
            else break;
        }
        return level;
    }

    /// <summary>레벨 → 등급 인덱스 (0=일반 … 4=신화, -1=잠금).</summary>
    public int GetGradeIndex(string id)
    {
        int lv = GetLevel(id);
        return lv <= 0 ? -1 : (lv - 1) / 2;
    }

    public string GetGradeLabel(string id)
    {
        int lv = GetLevel(id);
        return lv <= 0 ? "잠금" : GradeLabels[lv - 1];
    }

    public Color GetGradeColor(string id)
    {
        int lv = GetLevel(id);
        return lv <= 0 ? new Color(0.35f, 0.35f, 0.35f) : GradeColors[lv - 1];
    }

    /// <summary>다음 레벨에 필요한 조각 수. 최고 레벨이면 -1.</summary>
    public int GetNextThreshold(string id)
    {
        int frags = GetFragments(id);
        foreach (int t in FragThresholds)
            if (frags < t) return t;
        return -1;
    }

    public bool CanUpgrade(string id) => GetLevel(id) < MaxLevel;

    // ── 성급 ──────────────────────────────────────────────────────────────
    public const int MaxStar = 4;

    /// <summary>현재 성급 (1~4). 기본값 1.</summary>
    public int GetStarLevel(string id) =>
        _starLevels.TryGetValue(id, out var v) ? v : 1;

    /// <summary>성급 돌파 가능 여부 (4성 미만).</summary>
    public bool CanBreakThrough(string id) => GetStarLevel(id) < MaxStar;

    // ── 조각 추가 ─────────────────────────────────────────────────────────
    public void AddFragments(string id, int count)
    {
        if (count <= 0 || !_fragments.ContainsKey(id)) return;
        _fragments[id] += count;
        PlayerPrefs.SetInt(FragKeyPrefix + id, _fragments[id]);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
    }

    /// <summary>성급 돌파: 성급을 1 올립니다 (최대 4성).</summary>
    public bool TryBreakThrough(string id)
    {
        if (!CanBreakThrough(id)) return false;
        if (!_starLevels.ContainsKey(id)) return false;
        _starLevels[id]++;
        PlayerPrefs.SetInt(StarKeyPrefix + id, _starLevels[id]);
        PlayerPrefs.Save();
        OnChanged?.Invoke();
        return true;
    }
}
