using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 웨이브 시작 시 무작위 적 3마리에게 진흙을 던져 둔화시킨다.
// f_주수치 = 둔화 지속 시간(초), f_보조수치 = 둔화 배율(0~100)
public class AugmentMudOpening : AugmentEffectBase
{
    public override void OnGain()   => WeaponPoint.OnWaveStarted += OnWaveStart;
    public override void OnRemove() => WeaponPoint.OnWaveStarted -= OnWaveStart;

    private void OnWaveStart() => ThrowMud().Forget();

    private async UniTaskVoid ThrowMud()
    {
        var pool     = new List<EnemyController>(EnemyController.All);
        float duration = Data.f_주수치;
        float factor   = Mathf.Clamp01(1f - Data.f_보조수치 / 100f);

        int count = Mathf.Min(3, pool.Count);
        for (int i = 0; i < count; i++)
        {
            int idx = Random.Range(i, pool.Count);
            (pool[i], pool[idx]) = (pool[idx], pool[i]);
            pool[i]?.ApplySlow(duration, factor);
            await UniTask.WaitForSeconds(0.15f);
        }
    }
}
