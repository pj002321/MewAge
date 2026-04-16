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

    [Header("Orb 흡수 속도 (0 = 프리팹 기본값 사용)")]
    [SerializeField] private float goldMoveSpeed = 8f;
    [SerializeField] private float expMoveSpeed  = 8f;

    [Header("드롭 확률 (0~1)")]
    [SerializeField] private float goldDropChance      = 0.5f;
    [SerializeField] private float expDropChance       = 1.0f;
    [SerializeField] private float structureDropChance = 0.04f;

    [Header("보스 Orb 버스트 설정")]
    [SerializeField] private float bossOrbBurstSpeedMin = 4f;
    [SerializeField] private float bossOrbBurstSpeedMax = 11f;
    [SerializeField] private float bossOrbFriction      = 6f;

    [Header("기물 구슬 (챕터 필터링)")]
    [SerializeField] private int currentChapter = 1;

    // 회복 구슬 프리팹 캐시 (Resources/AUG/aug_spw_hp_skl)
    private const string HealOrbPath = "AUG/aug_spw_hp_skl";
    private GameObject _healOrbPrefab;
    private bool _healOrbSpawnedThisWave;

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

        TrySpawnHealOrb(pos);
    }

    /// <summary>AUG_SPW_HP_SKL 증강 확률에 따라 체력 회복 구슬을 스폰합니다.</summary>
    private void TrySpawnHealOrb(Vector3 deathPos)
    {
        if (_healOrbSpawnedThisWave) return;

        var player = PlayerController.Instance;
        if (player == null)
        {
            this.DLog("[ItemDrop] TrySpawnHealOrb: PlayerController.Instance null");
            return;
        }

        float chance = player.HealOrbSpawnChance;
        if (chance <= 0f)
        {
            this.DLog($"[ItemDrop] TrySpawnHealOrb: chance={chance} (0이하 → 스킵)");
            return;
        }

        float roll = Random.value;
        this.DLog($"[ItemDrop] TrySpawnHealOrb: chance={chance:F2}, roll={roll:F2} → {(roll <= chance ? "스폰" : "미스")}");
        if (roll > chance) return;

        SpawnHealOrb(deathPos);
        _healOrbSpawnedThisWave = true;
    }

    /// <summary>확률 없이 지정 위치(적 사망 위치 등)에 회복 구슬을 즉시 스폰합니다. (디버그/외부 호출용)</summary>
    public void SpawnHealOrb(Vector3 pos)
    {
        if (_healOrbPrefab == null)
            _healOrbPrefab = Resources.Load<GameObject>(HealOrbPath);

        if (_healOrbPrefab == null)
        {
            Debug.LogWarning($"[ItemDrop] 회복 구슬 프리팹 없음: Resources/{HealOrbPath}");
            return;
        }

        Vector3 offset = new(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
        Instantiate(_healOrbPrefab, pos + offset, Quaternion.identity);
    }

    /// <summary>웨이브 시작 시 회복 구슬 제한을 초기화합니다. GameManager.StartNextWave()에서 호출하세요.</summary>
    public void ResetWaveDrops() => _healOrbSpawnedThisWave = false;

    private void SpawnItem(GameObject prefab, Vector3 pos, DropItem.ItemType type, int value)
    {
        if (prefab == null) return;
        Vector3 offset = new(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
        var go = Instantiate(prefab, pos + offset, Quaternion.identity);
        float spd = type == DropItem.ItemType.Gold ? goldMoveSpeed : expMoveSpeed;
        go.GetComponent<DropItem>()?.Init(type, value, spd);
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
        float   spd      = type == DropItem.ItemType.Gold ? goldMoveSpeed : expMoveSpeed;
        go.GetComponent<DropItem>()?.InitBurst(type, value, velocity, bossOrbFriction, spd);
    }

    private void SpawnStructureOrb(Vector3 pos)
    {
        string prefix = $"C{currentChapter}_";
        var allPool = DB_기물.FindEntities(e => e.f_기물ID != null && e.f_기물ID.StartsWith(prefix));
        if (allPool == null || allPool.Count == 0) return;

        // 프리셋에 등록된 기물만 필터링
        var wareMgr = StructureWareManager.Shared;
        var pool = new System.Collections.Generic.List<DB_기물>();
        foreach (var item in allPool)
        {
            for (int i = 0; i < 4; i++)
            {
                if (wareMgr?.GetPreset(i) == item.f_기물ID)
                {
                    pool.Add(item);
                    break;
                }
            }
        }
        if (pool.Count == 0) return;

        var picked  = pool[Random.Range(0, pool.Count)];
        string id   = picked.f_기물ID;                               // e.g. C1_W0
        string chapter = $"C{currentChapter}";
        string path = $"Ware/{chapter}/{id.ToLower()}";             // Ware/C1/c1_w0

        var prefab = Resources.Load<GameObject>(path);
        if (prefab == null)
        {
            Debug.LogWarning($"[ItemDrop] 오브 프리팹 없음: Resources/{path}");
            return;
        }

        Vector3 offset = new(Random.Range(-0.3f, 0.3f), Random.Range(-0.3f, 0.3f), 0f);
        var go  = Instantiate(prefab, pos + offset, Quaternion.identity);
        var orb = go.GetComponent<StructureOrb>();
        if (orb != null) orb.Init(picked);
        else Debug.LogWarning($"[ItemDrop] {path} 프리팹에 StructureOrb 컴포넌트가 없습니다.");
    }

    public void OnChapterUp(int chapter) => currentChapter = chapter;

    /// <summary>웨이브 종료 시 맵에 남은 골드·경험치 orb를 전부 위로 튕겨 올립니다.</summary>
    public void ThrowUpAll()
    {
        foreach (var drop in Object.FindObjectsByType<DropItem>(FindObjectsSortMode.None))
            drop.ThrowUp();
    }
}
