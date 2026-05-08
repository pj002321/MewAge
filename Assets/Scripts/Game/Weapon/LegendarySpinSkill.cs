using UnityEngine;

/// <summary>
/// 전설 스핀 스킬: 장착 시 공전 반경이 커지고 회전 속도가 빨라집니다.
/// </summary>
public class LegendarySpinSkill : WeaponLegendarySkill
{
    [SerializeField] private float radiusMultiplier = 1.4f;  // 공전 반경 배율
    [SerializeField] private float speedMultiplier  = 1.35f; // 회전 속도 배율

    public override void OnEquip()
    {
        WP?.ApplySpinBoost(radiusMultiplier, speedMultiplier);
    }

    // 스핀은 SpinProcessHit에서 타격하므로 OnHit 미사용
    public override void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount) { }
}
