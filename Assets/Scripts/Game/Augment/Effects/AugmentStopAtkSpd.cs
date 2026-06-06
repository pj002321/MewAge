using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// 이동속도가 0일 때 공격속도 f_주수치% 증가. 움직이면 즉시 해제.
public class AugmentStopAtkSpd : AugmentEffectBase
{
    private CancellationTokenSource _cts;
    private bool _buffActive;
    private Rigidbody2D _rb;

    public override void OnGain()
    {
        _cts = new CancellationTokenSource();
        MonitorLoop(_cts.Token).Forget();
    }

    public override void OnRemove()
    {
        _cts?.Cancel();
        RemoveBuff();
    }

    private async UniTaskVoid MonitorLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            await UniTask.Yield(PlayerLoopTiming.Update, cancellationToken: ct);

            if (_rb == null)
                _rb = PlayerController.Instance?.GetComponent<Rigidbody2D>();

            bool isStopped = _rb == null || _rb.linearVelocity.sqrMagnitude < 0.01f;

            if (isStopped && !_buffActive)       ApplyBuff();
            else if (!isStopped && _buffActive)  RemoveBuff();
        }
    }

    private void ApplyBuff()
    {
        _buffActive = true;
        PlayerStatManager.Shared?.AddPctMod(StatType.AtkSpeed, Data.f_주수치);
    }

    private void RemoveBuff()
    {
        if (!_buffActive) return;
        _buffActive = false;
        PlayerStatManager.Shared?.AddPctMod(StatType.AtkSpeed, -Data.f_주수치);
    }
}
