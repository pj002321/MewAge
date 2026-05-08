using UnityEngine;

/// <summary>
/// 전설 슬래시 스킬: 슬래시 VFX 위치에서 추가 발사체를 발사합니다.
/// WeaponPoint.OnSlashVfxSpawned 이벤트를 구독해 작동합니다.
/// </summary>
public class LegendarySlashSkill : WeaponLegendarySkill
{
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private float      dmgRatio  = 0.5f;
    [SerializeField] private float      speed     = 9f;

    public override void OnEquip()
    {
        if (WP != null) WP.OnSlashVfxSpawned += HandleSlashVfx;
    }

    private void OnDestroy()
    {
        if (WP != null) WP.OnSlashVfxSpawned -= HandleSlashVfx;
    }

    private void HandleSlashVfx(Vector3 vfxPos, Vector2 slashDir)
    {
        if (projectilePrefab == null || WP == null) return;

        var go   = Instantiate(projectilePrefab, vfxPos, Quaternion.identity);
        var proj = go.GetComponent<Projectile>();
        if (proj == null) { Destroy(go); return; }

        proj.Init(WP.AttackDamage * dmgRatio, slashDir, speed,
            criticalRate: WP.CriticalRate,
            criticalMultiplier: WP.CriticalDamage,
            baseDamage: WP.AttackDamage * dmgRatio,
            knockbackPower: WP.KnockbackPower * 0.5f);
    }

    // 슬래시 타입은 OnHit가 호출되지 않으므로 빈 구현
    public override void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount) { }
}
