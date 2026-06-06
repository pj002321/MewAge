using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

// f_주수치 초마다 랜덤 위치에 달팽이 설치물 소환. 설치물은 주기적으로 투사체를 발사한다.
// f_주수치 = 소환 주기(초), f_보조수치 = 공격 데미지
public class AugmentSpawnSnail : AugmentEffectBase
{
    private const int MaxTurrets = 4;

    private CancellationTokenSource _cts;
    private readonly List<GameObject> _active = new();

    public override void OnGain()
    {
        _cts = new CancellationTokenSource();
        SpawnLoop(_cts.Token).Forget();
    }

    public override void OnRemove()
    {
        _cts?.Cancel();
        foreach (var go in _active)
            if (go != null) Object.Destroy(go);
        _active.Clear();
    }

    private async UniTaskVoid SpawnLoop(CancellationToken ct)
    {
        var snailPrefab      = ImprovVisualConfig.Shared?.GetEntry(ImprovType.AUG_SPAWN_SNAIL).companionPrefab;
        var projectilePrefab = ImprovVisualConfig.Shared?.GetEntry(ImprovType.AUG_SPAWN_SNAIL).projectilePrefab;

        while (!ct.IsCancellationRequested)
        {
            await UniTask.WaitForSeconds(Data.f_주수치, cancellationToken: ct);

            if (snailPrefab == null) continue;

            _active.RemoveAll(go => go == null);
            if (_active.Count >= MaxTurrets) continue;

            Vector2 pos = RandomMapPosition();
            var go = Object.Instantiate(snailPrefab, pos, Quaternion.identity);
            go.GetComponent<AugmentTurretController>()?.Init(Data.f_보조수치, projectilePrefab);
            _active.Add(go);
        }
    }

    private static Vector2 RandomMapPosition()
    {
        if (MovementBounds.Shared != null)
        {
            MovementBounds.Shared.GetPlayerBoundsMinMax(out Vector2 min, out Vector2 max);
            return new Vector2(Random.Range(min.x, max.x), Random.Range(min.y, max.y));
        }
        return PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.transform.position + Random.insideUnitCircle * 4f
            : Vector2.zero;
    }
}
