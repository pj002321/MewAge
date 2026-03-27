using Mewage.DB;
using UnityEngine;

/// <summary>
/// DB_유닛 한 행에서 로드한 런타임 데이터.
/// EnemySpawner 가 스폰 직후 EnemyController.ApplyUnitData() 에 전달합니다.
/// </summary>
public class UnitData
{
    public string     unitId;
    public int        chapter;
    public GameObject prefab;

    // ── 기본 스탯 ──────────────────────────────────────────────
    public int   hp;
    public int   contactDamage;
    public int   expDrop;
    public int   goldDrop;
    public int   attackRange;
    public float cooldown;
    public float moveSpeed;
    public string typeId;      // "MELEE", "RANGED", "CHARGE" …
    public int   rowCount;     // 스프라이트 시트 행 수

    /// <summary>RANGED 타입일 때만 설정, 나머지는 null.</summary>
    public ProjectileData projectile;

    // ── DB 로드 ────────────────────────────────────────────────

    public static UnitData FromDB(DB_유닛 row)
    {
        var d = new UnitData();
        d.unitId = row.f_유닛ID;

        // ID 접두사에서 챕터 추출 (C1_ → 1, C2_ → 2 …)
        d.chapter = 1;
        if (!string.IsNullOrEmpty(d.unitId) && d.unitId.Length >= 2 &&
            d.unitId[0] == 'C' && int.TryParse(d.unitId[1].ToString(), out int ch))
            d.chapter = ch;

        d.hp            = row.f_체력;
        d.contactDamage = row.f_데미지;
        d.expDrop       = row.f_경험치;
        d.goldDrop      = row.f_골드;
        d.attackRange   = row.f_공격범위;
        d.cooldown      = row.f_쿨타임;
        d.moveSpeed     = row.f_U이속;
        d.typeId        = row.f_타입ID;
        d.rowCount      = row.f_U행수;

        // 프리팹 로드: Resources/Unit/Ch1/c1_m1
        if (!string.IsNullOrEmpty(d.unitId))
        {
            string path = $"Unit/Ch{d.chapter}/{d.unitId.ToLower()}";
            d.prefab = Resources.Load<GameObject>(path);
            if (d.prefab == null)
                Debug.LogWarning($"[UnitData] 프리팹 없음: Resources/{path}");
        }

        // RANGED 타입 투사체 데이터
        if (d.typeId == "RANGED" && !string.IsNullOrEmpty(row.f_투사체ID))
        {
            string pPath = $"Unit/Ch{d.chapter}/Proj/{row.f_투사체ID.ToLower()}_p";
            var pPrefab = Resources.Load<GameObject>(pPath);
            if (pPrefab == null)
                Debug.LogWarning($"[UnitData] 투사체 없음: Resources/{pPath}");

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
}
