using UnityEngine;

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
    [SerializeField] private float goldDropChance = 0.5f;
    [SerializeField] private float expDropChance  = 1.0f;

    [Header("보스 Orb 포물선 설정")]
    [SerializeField] private float bossOrbBurstSpeedMin = 5f;
    [SerializeField] private float bossOrbBurstSpeedMax = 12f;
    [SerializeField] private float bossOrbGravity       = 16f;

    [Header("엘리트 Orb 포물선 설정")]
    [SerializeField] private int   eliteOrbCount    = 4;   // 금·경험치 각각 스폰 수
    [SerializeField] private float eliteOrbSpeedMin = 5f;
    [SerializeField] private float eliteOrbSpeedMax = 9f;
    [SerializeField] private float eliteOrbGravity  = 12f;

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

    /// <summary>엘리트 처치 시 gold·exp 오브를 위쪽 부채꼴로 팡 터뜨립니다.</summary>
    public void SpawnEliteDrops(Vector3 pos, int goldValue, int expValue)
    {
        int count = Mathf.Max(1, eliteOrbCount);
        int goldPer = Mathf.Max(1, goldValue / count);
        int expPer  = Mathf.Max(1, expValue  / count);

        for (int i = 0; i < count; i++) SpawnEliteBurstItem(goldPrefab, pos, DropItem.ItemType.Gold, goldPer);
        for (int i = 0; i < count; i++) SpawnEliteBurstItem(expPrefab,  pos, DropItem.ItemType.Exp,  expPer);

        TrySpawnHealOrb(pos);
    }

    // 위쪽 부채꼴(60°~120°) 포물선으로 분수처럼 터집니다.
    private void SpawnEliteBurstItem(GameObject prefab, Vector3 pos, DropItem.ItemType type, int value)
    {
        if (prefab == null) return;
        Vector3 offset = new(Random.Range(-0.2f, 0.2f), Random.Range(-0.2f, 0.2f), 0f);
        var go = Instantiate(prefab, pos + offset, Quaternion.identity);
        float angleDeg = Random.Range(60f, 120f);
        float rad      = angleDeg * Mathf.Deg2Rad;
        float speed    = Random.Range(eliteOrbSpeedMin, eliteOrbSpeedMax);
        Vector2 vel    = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
        go.GetComponent<DropItem>()?.InitParabolic(type, value, vel, eliteOrbGravity);
    }

    /// <summary>골드만 드롭합니다. 체력 절반 도달 시 호출됩니다.</summary>
    public void SpawnGoldDrop(Vector3 pos, int goldValue)
    {
        if (goldPrefab == null || Random.value > goldDropChance) return;
        SpawnItem(goldPrefab, pos, DropItem.ItemType.Gold, goldValue);
    }

    /// <summary>적 사망 위치에서 아이템을 드롭합니다.</summary>
    public void SpawnDrops(Vector3 pos, int goldValue, int expValue)
    {
        if (expPrefab != null && Random.value <= expDropChance)
            SpawnItem(expPrefab, pos, DropItem.ItemType.Exp, expValue);

        if (goldPrefab != null && Random.value <= goldDropChance)
            SpawnItem(goldPrefab, pos, DropItem.ItemType.Gold, goldValue);

        TrySpawnHealOrb(pos);
    }

    /// <summary>AUG_SPW_HP_SKL 증강 확률에 따라 체력 회복 구슬을 스폰합니다.</summary>
    private void TrySpawnHealOrb(Vector3 deathPos)
    {
        if (_healOrbSpawnedThisWave) return;

        var player = PlayerController.Instance;
        if (player == null) return;

        float chance = player.HealOrbSpawnChance;
        if (chance <= 0f) return;

        float roll = Random.value;
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

    /// <summary>보스 드롭 전용 — 위쪽 부채꼴(45°~135°) 포물선으로 분수처럼 터집니다.</summary>
    private void SpawnBurstItem(GameObject prefab, Vector3 pos, DropItem.ItemType type, int value)
    {
        if (prefab == null) return;
        Vector3 offset = new(Random.Range(-0.4f, 0.4f), Random.Range(-0.4f, 0.4f), 0f);
        var go = Instantiate(prefab, pos + offset, Quaternion.identity);
        float   angleDeg = Random.Range(45f, 135f);
        float   rad      = angleDeg * Mathf.Deg2Rad;
        float   speed    = Random.Range(bossOrbBurstSpeedMin, bossOrbBurstSpeedMax);
        Vector2 velocity = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad)) * speed;
        go.GetComponent<DropItem>()?.InitParabolic(type, value, velocity, bossOrbGravity);
    }

    /// <summary>웨이브 종료 시 맵에 남은 골드·경험치 orb를 전부 위로 튕겨 올립니다.</summary>
    public void ThrowUpAll()
    {
        foreach (var drop in Object.FindObjectsByType<DropItem>(FindObjectsSortMode.None))
            drop.ThrowUp();
    }

    /// <summary>보스 처치 후 맵에 남은 orb를 모두 플레이어 쪽으로 자동 흡수시킵니다.</summary>
    public void ForceAttractAll()
    {
        foreach (var drop in Object.FindObjectsByType<DropItem>(FindObjectsSortMode.None))
            drop.ForceAttract();
    }
}
