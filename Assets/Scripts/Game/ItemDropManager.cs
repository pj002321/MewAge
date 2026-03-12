using UnityEngine;

/// <summary>
/// 아이템 드롭 Prefab을 관리하고 스폰합니다.
/// Inspector에서 goldPrefab / expPrefab 을 연결하세요.
/// </summary>
public class ItemDropManager : MonoBehaviourSingleton<ItemDropManager>
{
    [SerializeField] private GameObject goldPrefab;
    [SerializeField] private GameObject expPrefab;

    [Header("드롭 확률 (0~1)")]
    [SerializeField] private float goldDropChance = 0.5f;   // 골드 드롭 확률
    [SerializeField] private float expDropChance  = 1.0f;   // 경험치는 항상 드롭

    protected override bool DontDestroy => false;

    /// <summary>적 사망 위치에서 아이템을 드롭합니다.</summary>
    public void SpawnDrops(Vector3 pos, int goldValue, int expValue)
    {
        if (expPrefab != null && Random.value <= expDropChance)
            SpawnItem(expPrefab, pos, DropItem.ItemType.Exp, expValue);

        if (goldPrefab != null && Random.value <= goldDropChance)
            SpawnItem(goldPrefab, pos, DropItem.ItemType.Gold, goldValue);
    }

    private void SpawnItem(GameObject prefab, Vector3 pos, DropItem.ItemType type, int value)
    {
        // 살짝 랜덤 오프셋으로 겹침 방지
        Vector3 offset = new(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
        var go = Instantiate(prefab, pos + offset, Quaternion.identity);
        go.GetComponent<DropItem>()?.Init(type, value);
    }
}
