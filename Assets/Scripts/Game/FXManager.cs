using UnityEngine;

/// <summary>
/// 데미지 텍스트 / 히트 이펙트 스폰을 담당하는 싱글톤 매니저.
/// Inspector에서 각 Prefab을 연결해주세요.
/// </summary>
public class FXManager : MonoBehaviourSingleton<FXManager>
{
    [SerializeField] private GameObject damageTextPrefab;
    [SerializeField] private GameObject hitEffectPrefab;

    protected override bool DontDestroy => false;

    public void SpawnDamageText(Vector3 worldPos, float damage, bool isCritical = false)
    {
        if (damageTextPrefab == null) return;

        // 여러 텍스트가 겹치지 않도록 수평 랜덤 오프셋
        Vector3 offset = new(Random.Range(-0.2f, 0.2f), 0.3f, 0f);
        var go = Instantiate(damageTextPrefab, worldPos + offset, Quaternion.identity);
        go.GetComponent<DamageText>()?.Init(damage, isCritical);
    }

    public void SpawnHitEffect(Vector3 worldPos)
    {
        if (hitEffectPrefab == null) return;
        Instantiate(hitEffectPrefab, worldPos, Quaternion.identity);
    }
}
