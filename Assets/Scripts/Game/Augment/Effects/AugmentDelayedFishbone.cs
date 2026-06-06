using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 에픽 이상 무기가 적을 명중하면 생선뼈가 박힌다.
// f_주수치 초 후 폭발해 f_보조수치 피해 (반경 2 이내 50% 추가 피해).
public class AugmentDelayedFishbone : AugmentEffectBase
{
    public override void OnGain()   => WeaponPoint.OnHitEnemy += OnHit;
    public override void OnRemove() => WeaponPoint.OnHitEnemy -= OnHit;

    private void OnHit(EnemyController enemy, float damage, WeaponGrade grade)
    {
        if (grade < WeaponGrade.Epic) return;
        Explode(enemy).Forget();
    }

    private async UniTaskVoid Explode(EnemyController enemy)
    {
        await UniTask.WaitForSeconds(Data.f_주수치);
        if (enemy == null) return;

        Vector2 pos = enemy.transform.position;
        enemy.TakeDamage(Data.f_보조수치);

        foreach (var e in new List<EnemyController>(EnemyController.All))
        {
            if (e == null || e == enemy) continue;
            if (Vector2.Distance(e.transform.position, pos) < 2f)
                e.TakeDamage(Data.f_보조수치 * 0.5f);
        }
    }
}
