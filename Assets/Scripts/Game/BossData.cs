using Mewage.DB;
using UnityEngine;

/// <summary>
/// DB_보스 한 행에서 로드한 런타임 데이터.
///
/// 스테이지 문자열 규칙: "1_1_10" → 챕터1, 스테이지1, 웨이브10 (waveIndex=0)
///   waveIndex = wave/10 - 1  (10→0, 20→1, 30→2 …)
///   프리팹 경로: Resources/Unit/Ch1/Boss/c1_s1_0
/// </summary>
public class BossData
{
    public string     bossId;
    public int        chapter;
    public int        stage;
    public int        waveIndex;
    public GameObject prefab;

    // ── 표시 정보 ──────────────────────────────────────────────
    public string bossName;
    public string skillDesc;   // UI용 스킬 설명

    // ── 기본 스탯 ──────────────────────────────────────────────
    public int   hp;
    public int   contactDamage;
    public float moveSpeed;
    public int   rowCount;

    // ── 스킬 루프 (BS1→D1→추격T, BS2→D2→추격T, BS3→D3→추격T) ──
    /// <summary>스킬 ID 배열: BS1, BS2, BS3. 빈 문자열이면 해당 슬롯 없음.</summary>
    public string[] skillIds = new string[3];
    /// <summary>각 스킬 실행 후 대기 시간 (D1, D2, D3).</summary>
    public float[]  delays   = new float[3];
    /// <summary>각 스킬 루프 후 플레이어 추격 시간 (초).</summary>
    public float    chaseTime;

    // ── 드롭 (1원짜리 오브 N개) ────────────────────────────────
    public int goldCount;
    public int expCount;

    // ── 투사체 (RNG 스킬 보스) ─────────────────────────────────
    /// <summary>RNG 스킬이 있을 때만 설정, 나머지 null.</summary>
    public ProjectileData projectile;

    // ── DB 로드 ────────────────────────────────────────────────

    public static BossData FromDB(DB_보스 row)
    {
        var d = new BossData();
        d.bossId = row.f_보스ID;

        // 스테이지 문자열 파싱: "1_1_10" → chapter=1, stage=1, waveIndex=0
        ParseStage(row.f_스테이지, out d.chapter, out d.stage, out d.waveIndex);

        d.bossName   = row.f_보스이름;
        d.skillDesc  = row.f_스킬;
        d.hp          = row.f_B체력;
        d.contactDamage = row.f_B접촉데미지;
        d.moveSpeed   = row.f_B이속;
        d.rowCount    = row.f_B행수;

        d.skillIds[0] = row.f_BS1 ?? ""; d.delays[0] = row.f_D1;
        d.skillIds[1] = row.f_BS2 ?? ""; d.delays[1] = row.f_D2;
        d.skillIds[2] = row.f_BS3 ?? ""; d.delays[2] = row.f_D3;
        d.chaseTime   = row.f_추격T;

        d.goldCount = row.f_골드;
        d.expCount  = row.f_경험치;

        // 프리팹 로드: Resources/Unit/Ch1/Boss/c1_s1_0
        if (!string.IsNullOrEmpty(d.bossId))
        {
            string path = $"Unit/Ch{d.chapter}/Boss/{d.bossId.ToLower()}";
            d.prefab = Resources.Load<GameObject>(path);
            if (d.prefab == null)
                Debug.LogWarning($"[BossData] 프리팹 없음: Resources/{path}");
        }

        // RNG 스킬이 있을 때 투사체 데이터
        bool hasRng = System.Array.IndexOf(d.skillIds, "RNG") >= 0;
        if (hasRng && !string.IsNullOrEmpty(row.f_투사체ID))
        {
            string pPath = $"Unit/Ch{d.chapter}/Proj/{row.f_투사체ID.ToLower()}_p";
            var pPrefab = Resources.Load<GameObject>(pPath);
            if (pPrefab == null)
                Debug.LogWarning($"[BossData] 투사체 없음: Resources/{pPath}");

            d.projectile = new ProjectileData
            {
                prefab      = pPrefab,
                speed       = row.f_P이속,
                damage      = row.f_P데미지,
                bonusDamage = row.f_보조데미지,
                canReflect  = row.f_반사,
                rowCount    = row.f_P행수,
            };
        }

        return d;
    }

    /// <summary>"1_1_10" → chapter=1, stage=1, waveIndex=0</summary>
    private static void ParseStage(string s, out int chapter, out int stage, out int waveIndex)
    {
        chapter = 1; stage = 1; waveIndex = 0;
        if (string.IsNullOrEmpty(s)) return;

        var parts = s.Split('_');
        if (parts.Length > 0) int.TryParse(parts[0], out chapter);
        if (parts.Length > 1) int.TryParse(parts[1], out stage);
        if (parts.Length > 2 && int.TryParse(parts[2], out int wave))
            waveIndex = wave / 10 - 1;
    }
}
