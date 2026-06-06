using UnityEngine;

// 적 명중 시 f_주수치% 확률로 화상 DoT 적용 (틱당 피해 f_보조수치).
// 활성 중 playerVfxPrefab을 플레이어에게 부착.
public class AugmentBurningPaws : AugmentEffectBase
{
    private GameObject _vfxInst;

    public override void OnGain()
    {
        WeaponPoint.OnHitEnemy += OnHit;

        var pfb    = ImprovVisualConfig.Shared?.GetEntry(ImprovType.AUG_BURNING_PAWS).playerVfxPrefab;
        var player = PlayerController.Instance;
        if (pfb != null && player != null)
            _vfxInst = Object.Instantiate(pfb, player.transform);
    }

    public override void OnRemove()
    {
        WeaponPoint.OnHitEnemy -= OnHit;
        if (_vfxInst != null) { Object.Destroy(_vfxInst); _vfxInst = null; }
    }

    private void OnHit(EnemyController enemy, float damage, WeaponGrade grade)
    {
        if (Random.value > Data.f_주수치 / 100f) return;
        enemy.ApplyFire(Data.f_보조수치, duration: 3f);
    }
}
