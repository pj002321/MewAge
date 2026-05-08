using UnityEngine;

/// <summary>
/// 전설 원거리 스킬: 발사 시 서브 투사체를 추가로 발사합니다.
/// 서브 투사체는 메인 방향에서 좌우로 각도만큼 벌어진 방향으로 날아갑니다.
/// </summary>
public class LegendaryRangedSkill : WeaponLegendarySkill
{
    [SerializeField] private GameObject subProjectilePrefab; // null이면 WeaponPoint의 projectilePrefab 사용
    [SerializeField] private float      spreadAngle  = 18f;   // 메인 발사 방향 기준 각도 오프셋
    [SerializeField] private float      dmgRatio     = 0.6f;  // 서브 투사체 데미지 비율
    [SerializeField] private float      speedRatio   = 0.9f;  // 서브 투사체 속도 비율

    public override void OnShoot(Vector3 firePos, Vector2 fireDir)
    {
        if (subProjectilePrefab == null) return;
        if (WP == null) return;

        float   dmg   = WP.AttackDamage * dmgRatio;
        float   speed = WP.ProjectileSpeed * speedRatio;

        SpawnSub(firePos, RotateDir(fireDir, -spreadAngle), dmg, speed);
        SpawnSub(firePos, RotateDir(fireDir,  spreadAngle), dmg, speed);
    }

    private void SpawnSub(Vector3 pos, Vector2 dir, float dmg, float speed)
    {
        var go   = Instantiate(subProjectilePrefab, pos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Destroy(go); return; }
        proj.Init(dmg, dir, speed,
            criticalRate: WP.CriticalRate,
            criticalMultiplier: WP.CriticalDamage,
            baseDamage: dmg,
            knockbackPower: WP.KnockbackPower * 0.5f);
    }

    private static Vector2 RotateDir(Vector2 dir, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(dir.x * cos - dir.y * sin, dir.x * sin + dir.y * cos);
    }

    // OnHit은 원거리 무기에선 호출되지 않으므로 빈 구현
    public override void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount) { }
}
