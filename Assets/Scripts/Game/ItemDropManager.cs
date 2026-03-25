using System.Collections.Generic;
using UnityEngine;
using Mewage.DB;

/// <summary>
/// 아이템 드롭 Prefab을 관리하고 스폰합니다.
/// Inspector에서 goldPrefab / expPrefab 을 연결하세요.
/// </summary>
public class ItemDropManager : MonoBehaviourSingleton<ItemDropManager>
{
    [SerializeField] private GameObject goldPrefab;
    [SerializeField] private GameObject expPrefab;

    [Header("드롭 확률 (0~1)")]
    [SerializeField] private float goldDropChance      = 0.5f;
    [SerializeField] private float expDropChance       = 1.0f;
    [SerializeField] private float structureDropChance = 0.1f;

    [Header("보스 Orb 버스트 설정")]
    [SerializeField] private float bossOrbBurstSpeedMin = 4f;
    [SerializeField] private float bossOrbBurstSpeedMax = 11f;
    [SerializeField] private float bossOrbFriction      = 6f;

    [Header("기물 구슬 (챕터 필터링)")]
    [SerializeField] private int currentChapter = 1;

    protected override bool DontDestroy => false;

    /// <summary>보스 처치 시 다량의 골드·exp 오브를 사방으로 뿜어냅니다.</summary>
    public void SpawnBossDrops(Vector3 pos, int goldCount, int goldValue, int expCount, int expValue)
    {
        for (int i = 0; i < goldCount; i++) SpawnBurstItem(goldPrefab, pos, DropItem.ItemType.Gold, goldValue);
        for (int i = 0; i < expCount;  i++) SpawnBurstItem(expPrefab,  pos, DropItem.ItemType.Exp,  expValue);
    }

    /// <summary>적 사망 위치에서 아이템을 드롭합니다.</summary>
    public void SpawnDrops(Vector3 pos, int goldValue, int expValue)
    {
        if (expPrefab != null && Random.value <= expDropChance)
            SpawnItem(expPrefab, pos, DropItem.ItemType.Exp, expValue);

        if (goldPrefab != null && Random.value <= goldDropChance)
            SpawnItem(goldPrefab, pos, DropItem.ItemType.Gold, goldValue);

        if (Random.value <= structureDropChance)
            SpawnStructureOrb(pos);
    }

    private void SpawnItem(GameObject prefab, Vector3 pos, DropItem.ItemType type, int value)
    {
        if (prefab == null) return;
        Vector3 offset = new(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
        var go = Instantiate(prefab, pos + offset, Quaternion.identity);
        go.GetComponent<DropItem>()?.Init(type, value);
    }

    /// <summary>보스 드롭 전용 — 360도 랜덤 방향 초기 속도로 사방으로 퍼집니다.</summary>
    private void SpawnBurstItem(GameObject prefab, Vector3 pos, DropItem.ItemType type, int value)
    {
        if (prefab == null) return;
        Vector3 offset = new(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f);
        var go = Instantiate(prefab, pos + offset, Quaternion.identity);
        float   angle    = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float   speed    = Random.Range(bossOrbBurstSpeedMin, bossOrbBurstSpeedMax);
        Vector2 velocity = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * speed;
        go.GetComponent<DropItem>()?.InitBurst(type, value, velocity, bossOrbFriction);
    }

    private void SpawnStructureOrb(Vector3 pos)
    {
        var all  = DB_기물.FindEntities(_ => true);
        var pool = DB_기물.FindEntities(e => e.f_챕터 <= currentChapter && e.f_프리팹 != null);

        Debug.Log($"[ItemDrop] DB_기물 전체:{all?.Count} / 챕터·프리팹 필터 후:{pool?.Count}");

        if (pool == null || pool.Count == 0) return;

        var picked = pool[Random.Range(0, pool.Count)];
        Vector3 offset = new(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
        var go = Instantiate(picked.f_프리팹, pos + offset, Quaternion.identity);
        var orb = go.GetComponent<StructureOrb>();
        if (orb == null) Debug.LogWarning($"[ItemDrop] {picked.f_프리팹.name}에 StructureOrb 컴포넌트가 없습니다.");
        else orb.Init(picked);
    }

    public void OnChapterUp(int chapter) => currentChapter = chapter;
}
