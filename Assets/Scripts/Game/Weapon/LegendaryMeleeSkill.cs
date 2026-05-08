using UnityEngine;

/// <summary>
/// 전설 근접 무기 스킬: 타격 시 일정 확률로 적에게 상태 이상 부여.
/// Stun(기절) / Freeze(빙결) / Burn(화상-출혈) 중 하나를 인스펙터에서 설정.
/// </summary>
public class LegendaryMeleeSkill : WeaponLegendarySkill
{
    public enum StatusType { Stun, Freeze, Burn }

    [SerializeField] private float      chance   = 0.25f;
    [SerializeField] private float      duration = 1.5f;
    [SerializeField] private StatusType effect   = StatusType.Stun;

    // Freeze 전용
    [SerializeField] private float freezeDamageOnThaw = 0f;
    [SerializeField] private Color freezeColor = new Color(0.5f, 0.85f, 1f, 1f);

    public override void OnHit(Vector2 hitDir, Collider2D[] hitBuffer, int colCount)
    {
        if (Random.value > chance) return;
        for (int i = 0; i < colCount; i++)
        {
            if (hitBuffer[i] == null) continue;
            if (!hitBuffer[i].TryGetComponent<EnemyController>(out var e)) continue;
            if (!e.gameObject.activeInHierarchy) continue;

            switch (effect)
            {
                case StatusType.Stun:
                    e.Stun(duration);
                    break;
                case StatusType.Freeze:
                    e.Freeze(duration, freezeDamageOnThaw, freezeColor);
                    break;
                case StatusType.Burn:
                    e.ApplyBleed(WP != null ? WP.BleedDamage + 1f : 5f);
                    e.ApplySlow(duration, 0.5f);
                    break;
            }
        }
    }
}
