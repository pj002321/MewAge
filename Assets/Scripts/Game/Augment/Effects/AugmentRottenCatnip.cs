// 적 명중 시 f_주수치% 확률로 독 DoT 적용 (틱당 피해 f_보조수치).
public class AugmentRottenCatnip : AugmentEffectBase
{
    public override void OnGain()   => WeaponPoint.OnHitEnemy += OnHit;
    public override void OnRemove() => WeaponPoint.OnHitEnemy -= OnHit;

    private void OnHit(EnemyController enemy, float damage, WeaponGrade grade)
    {
        if (UnityEngine.Random.value > Data.f_주수치 / 100f) return;
        enemy.ApplyPurpleDot(Data.f_보조수치, duration: 4f);
    }
}
