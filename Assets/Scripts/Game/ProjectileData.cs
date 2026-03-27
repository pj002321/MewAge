using UnityEngine;

/// <summary>
/// 투사체 런타임 데이터 — 유닛(RANGED) 및 보스(RNG 스킬) 공용.
/// DB_유닛 / DB_보스의 투사체 필드에서 생성됩니다.
/// </summary>
[System.Serializable]
public class ProjectileData
{
    public GameObject prefab;
    public float      speed;
    public float      damage;
    public float      bonusDamage;
    public bool       canReflect;
    public int        rowCount;
}
