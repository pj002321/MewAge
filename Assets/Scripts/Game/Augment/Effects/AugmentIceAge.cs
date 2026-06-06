using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 웨이브 시작 시 모든 적을 f_주수치 초 동안 빙결.
// 최초 1회 빙결 종료 후 공격속도 f_보조수치% 증가.
public class AugmentIceAge : AugmentEffectBase
{
    private bool _speedGranted;

    public override void OnGain()   => WeaponPoint.OnWaveStarted += OnWaveStart;
    public override void OnRemove() => WeaponPoint.OnWaveStarted -= OnWaveStart;

    private void OnWaveStart() => IceAgeRoutine().Forget();

    private async UniTaskVoid IceAgeRoutine()
    {
        float duration = Data.f_주수치;
        var   color    = new Color(0.6f, 0.9f, 1f);

        foreach (var e in new List<EnemyController>(EnemyController.All))
            IceSkill.FreezeEnemy(ImprovManager.Shared, e, duration, 0f, color);

        if (!_speedGranted)
        {
            await UniTask.WaitForSeconds(duration);
            _speedGranted = true;
            PlayerStatManager.Shared?.AddPctMod(StatType.AtkSpeed, Data.f_보조수치);
        }
    }
}
