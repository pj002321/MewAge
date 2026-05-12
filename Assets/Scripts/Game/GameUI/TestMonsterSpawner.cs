using Mewage.DB;
using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 테스트씬 전용 몬스터 스폰 도구 (Odin Inspector).
/// 챕터·타입·인덱스를 선택 후 [스폰] 버튼으로 교체 스폰.
/// 몬스터가 죽으면 같은 위치에 1초 후 자동 리스폰.
/// </summary>
public class TestMonsterSpawner : MonoBehaviour
{
    public enum MonsterCategory { Normal, Elite, Boss }

    [System.Serializable]
    public class ChapterConfig
    {
        [HorizontalGroup, LabelText("챕터"), LabelWidth(40)]
        public int chapter = 1;

        [FoldoutGroup("일반"), ListDrawerSettings(ShowIndexLabels = true, DraggableItems = false)]
        public List<GameObject> normalPrefabs = new();

        [FoldoutGroup("엘리트"), ListDrawerSettings(ShowIndexLabels = true, DraggableItems = false)]
        public List<GameObject> elitePrefabs = new();

        [FoldoutGroup("보스"), ListDrawerSettings(ShowIndexLabels = true, DraggableItems = false)]
        public List<GameObject> bossPrefabs = new();
    }

    // ── 설정 ──────────────────────────────────────────────────────
    [Title("챕터별 몬스터 등록")]
    [ListDrawerSettings(ShowItemCount = true, DraggableItems = false)]
    [SerializeField] private List<ChapterConfig> chapters = new();

    [Title("보스 환경 (dev 씬 전용)")]
    [Tooltip("dev 씬에서 보스 맵으로 전환하려면 해당 챕터의 StageEnvironmentSO를 연결하세요.")]
    [SerializeField] private StageEnvironmentSO testStageEnv;

#if UNITY_EDITOR
    [Button("프리팹 자동 로드 (에디터 전용)", ButtonSizes.Medium), GUIColor(0.5f, 0.75f, 1f)]
    private void AutoLoadPrefabs()
    {
        chapters.Clear();
        for (int ch = 1; ch <= 7; ch++)
        {
            string normalDir = $"Assets/Resources/Unit/Ch{ch}";
            string bossDir   = $"Assets/Resources/Unit/Ch{ch}/Boss";

            var entry = new ChapterConfig { chapter = ch };

            // 일반 적 (Boss 서브폴더 제외)
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { normalDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Boss/") || path.Contains("/Proj/")) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) entry.normalPrefabs.Add(prefab);
            }

            // 보스 (Boss 서브폴더 전체)
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { bossDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Proj/")) continue;
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab != null) entry.bossPrefabs.Add(prefab);
            }

            if (entry.normalPrefabs.Count > 0 || entry.bossPrefabs.Count > 0)
                chapters.Add(entry);
        }
        Debug.Log($"[TestMonsterSpawner] 자동 로드 완료 — {chapters.Count}개 챕터");
    }
#endif

    [Title("스폰 설정")]
    [BoxGroup("Control"), SerializeField, LabelText("스폰 위치")]
    private Transform spawnPoint;

    [BoxGroup("Control")]
    [ValueDropdown("GetChapterOptions"), LabelText("챕터")]
    public int selectedChapter = 1;

    [BoxGroup("Control")]
    [EnumToggleButtons, LabelText("타입")]
    public MonsterCategory category = MonsterCategory.Normal;

    [BoxGroup("Control")]
    [ValueDropdown("GetPrefabOptions"), LabelText("몬스터")]
    public int prefabIndex = 0;

    [BoxGroup("Control")]
    [ReadOnly, LabelText("현재 몬스터")]
    public string currentMonsterName = "없음";

    [BoxGroup("Control")]
    [Button("스폰", ButtonSizes.Large), GUIColor(0.3f, 0.9f, 0.3f)]
    private void SpawnSelected()
    {
        var prefab = GetSelectedPrefab();
        if (prefab == null) { Debug.LogWarning("[TestMonsterSpawner] 선택된 프리팹 없음"); return; }

        DespawnCurrent();
        var pos = spawnPoint != null ? (Vector2)spawnPoint.position : Vector2.zero;
        SpawnPrefab(prefab, pos);
    }

    [Title("보스 빠른 스폰")]
    [BoxGroup("Boss"), ValueDropdown("GetAllBossOptions"), LabelText("보스 선택")]
    public int bossQuickIndex = 0;

    [BoxGroup("Boss"), HorizontalGroup("Boss/Nav")]
    [Button("◀ 이전", ButtonSizes.Medium), GUIColor(0.7f, 0.85f, 1f)]
    private void PrevBoss()
    {
        var list = GetAllBossList();
        if (list.Count == 0) return;
        bossQuickIndex = (bossQuickIndex - 1 + list.Count) % list.Count;
        SpawnQuickBoss();
    }

    [BoxGroup("Boss"), HorizontalGroup("Boss/Nav")]
    [Button("다음 ▶", ButtonSizes.Medium), GUIColor(0.7f, 0.85f, 1f)]
    private void NextBoss()
    {
        var list = GetAllBossList();
        if (list.Count == 0) return;
        bossQuickIndex = (bossQuickIndex + 1) % list.Count;
        SpawnQuickBoss();
    }

    [BoxGroup("Boss")]
    [Button("보스 스폰", ButtonSizes.Large), GUIColor(1f, 0.75f, 0.2f)]
    private void SpawnQuickBoss()
    {
        var list = GetAllBossList();
        if (list.Count == 0) { Debug.LogWarning("[TestMonsterSpawner] 등록된 보스 없음"); return; }
        bossQuickIndex = Mathf.Clamp(bossQuickIndex, 0, list.Count - 1);
        DespawnCurrent();
        var pos = spawnPoint != null ? (Vector2)spawnPoint.position : Vector2.zero;
        SpawnBoss(list[bossQuickIndex], pos);
    }

    [BoxGroup("Control")]
    [Button("제거", ButtonSizes.Medium), GUIColor(0.9f, 0.4f, 0.4f)]
    private void DespawnCurrent()
    {
        StopAllCoroutines();
        _pendingPrefab = null;

        if (_currentEnemy != null)
        {
            _currentEnemy.OnDeath -= OnEnemyDied;
            EnemyPool.Shared?.Return(_currentEnemy);
            _currentEnemy = null;
        }

        if (_currentBoss != null)
        {
            _currentBoss.OnDeath -= OnBossDied;
            Destroy(_currentBoss.gameObject);
            _currentBoss = null;
            WaveEnvironmentSwitcher.Shared?.SetNormal();
        }

        currentMonsterName = "없음";
    }

    // ── 무적 / 플레이어 설정 ──────────────────────────────────────
    public static bool EnemyInvincible { get; private set; }

    /// <summary>씬 재로드 시 GameManager에서 호출 — 이전 테스트 세션의 정적 플래그를 초기화합니다.</summary>
    public static void ResetDebugFlags() => EnemyInvincible = false;

    [Title("테스트 옵션")]
    [ShowInInspector, ReadOnly, LabelText("적 무적 상태")]
    private static bool _enemyInvincibleDisplay => EnemyInvincible;

    [Button("$_invincibleBtnLabel", ButtonSizes.Medium)]
    private void ToggleEnemyInvincible()
    {
        EnemyInvincible = !EnemyInvincible;
        Debug.Log($"[TestMonsterSpawner] 적 무적 {(EnemyInvincible ? "ON" : "OFF")}");
    }
    private string _invincibleBtnLabel => EnemyInvincible ? "적 무적 ON  (클릭→OFF)" : "적 무적 OFF (클릭→ON)";

    // ── 런타임 상태 ───────────────────────────────────────────────
    private EnemyController _currentEnemy;
    private BossController  _currentBoss;
    private GameObject      _pendingPrefab;
    private Vector2         _lastSpawnPos;

    private void Start()
    {
        StartCoroutine(SetPlayerHpRoutine());
    }

    private System.Collections.IEnumerator SetPlayerHpRoutine()
    {
        // PlayerStatManager가 초기화될 때까지 대기
        while (PlayerStatManager.Shared == null)
            yield return null;
        PlayerStatManager.Shared.BeginBatch();
        PlayerStatManager.Shared.SetBase(StatType.MaxHP, 9999f);
        PlayerStatManager.Shared.EndBatch();
    }

    // ── 스폰 ─────────────────────────────────────────────────────
    private void SpawnPrefab(GameObject prefab, Vector2 pos)
    {
        _lastSpawnPos = pos;
        _pendingPrefab = prefab;

        var boss = prefab.GetComponent<BossController>();
        if (boss != null)
            SpawnBoss(prefab, pos);
        else
            SpawnEnemy(prefab, pos);
    }

    private void SpawnEnemy(GameObject prefab, Vector2 pos)
    {
        EnemyController enemy;
        if (EnemyPool.Shared != null)
            enemy = EnemyPool.Shared.Get(prefab, pos);
        else
        {
            var go = Instantiate(prefab, pos, Quaternion.identity);
            enemy = go.GetComponent<EnemyController>();
        }

        if (enemy == null) { Debug.LogWarning("[TestMonsterSpawner] EnemyController 없음"); return; }

        // DB에서 유닛 스탯 로드 (프리팹 이름 = 유닛 ID)
        var unitData = FindUnitData(prefab.name);
        if (unitData != null)
            enemy.ApplyUnitData(unitData);
        else
            Debug.LogWarning($"[TestMonsterSpawner] DB에서 '{prefab.name}' 유닛 없음 — 프리팹 기본값 사용");

        enemy.PlaySpawnBounce();
        enemy.OnDeath += OnEnemyDied;
        _currentEnemy = enemy;
        currentMonsterName = prefab.name;
    }

    private void SpawnBoss(GameObject prefab, Vector2 pos)
    {
        // 보스 맵으로 전환
        var wes = WaveEnvironmentSwitcher.Shared;
        if (wes != null)
        {
            if (testStageEnv != null)
                wes.SetStageForTest(testStageEnv);
            wes.SetBoss();
        }

        var go = Instantiate(prefab, pos, Quaternion.identity);
        var boss = go.GetComponent<BossController>();
        if (boss == null) { Debug.LogWarning("[TestMonsterSpawner] BossController 없음"); Destroy(go); return; }

        boss.OnDeath += OnBossDied;
        _currentBoss = boss;
        currentMonsterName = prefab.name;
    }

    // ── 사망 → 리스폰 ────────────────────────────────────────────
    private void OnEnemyDied()
    {
        if (_currentEnemy != null)
            _currentEnemy.OnDeath -= OnEnemyDied;
        _currentEnemy = null;
        currentMonsterName = "없음";
        StartCoroutine(RespawnRoutine());
    }

    private void OnBossDied()
    {
        if (_currentBoss != null)
            _currentBoss.OnDeath -= OnBossDied;
        _currentBoss = null;
        currentMonsterName = "없음";

        WaveEnvironmentSwitcher.Shared?.SetNormal();
        StartCoroutine(RespawnRoutine());
    }

    private IEnumerator RespawnRoutine()
    {
        yield return new WaitForSeconds(1f);
        if (_pendingPrefab != null)
            SpawnPrefab(_pendingPrefab, _lastSpawnPos);
    }

    // ── 전체 보스 플랫 리스트 ─────────────────────────────────────
    private List<GameObject> GetAllBossList()
    {
        var result = new List<GameObject>();
        foreach (var c in chapters)
            foreach (var p in c.bossPrefabs)
                if (p != null) result.Add(p);
        return result;
    }

    private IEnumerable<ValueDropdownItem<int>> GetAllBossOptions()
    {
        int idx = 0;
        foreach (var c in chapters)
            foreach (var p in c.bossPrefabs)
                if (p != null)
                    yield return new ValueDropdownItem<int>($"Ch{c.chapter} — {p.name}", idx++);
    }

    // ── Odin ValueDropdown 소스 ───────────────────────────────────
    private IEnumerable<ValueDropdownItem<int>> GetChapterOptions()
    {
        foreach (var c in chapters)
            yield return new ValueDropdownItem<int>($"챕터 {c.chapter}", c.chapter);
    }

    private IEnumerable<ValueDropdownItem<int>> GetPrefabOptions()
    {
        var entry = GetChapter();
        if (entry == null) yield break;
        var list = category switch
        {
            MonsterCategory.Normal => entry.normalPrefabs,
            MonsterCategory.Elite  => entry.elitePrefabs,
            MonsterCategory.Boss   => entry.bossPrefabs,
            _                      => null,
        };
        if (list == null) yield break;
        for (int i = 0; i < list.Count; i++)
            yield return new ValueDropdownItem<int>(list[i] != null ? list[i].name : $"[{i}] (null)", i);
    }

    // ── 헬퍼 ─────────────────────────────────────────────────────
    private ChapterConfig GetChapter() =>
        chapters.Find(c => c.chapter == selectedChapter);

    private GameObject GetSelectedPrefab()
    {
        var entry = GetChapter();
        if (entry == null) return null;
        return category switch
        {
            MonsterCategory.Normal => GetAt(entry.normalPrefabs, prefabIndex),
            MonsterCategory.Elite  => GetAt(entry.elitePrefabs,  prefabIndex),
            MonsterCategory.Boss   => GetAt(entry.bossPrefabs,   prefabIndex),
            _                      => null,
        };
    }

    private static GameObject GetAt(List<GameObject> list, int idx) =>
        list != null && idx >= 0 && idx < list.Count ? list[idx] : null;

    // 프리팹 이름(= 유닛 ID)으로 DB_유닛 행을 찾아 UnitData 반환
    private static UnitData FindUnitData(string prefabName)
    {
        UnitData result = null;
        DB_유닛.ForEachEntity(row =>
        {
            if (result != null) return;
            if (string.Equals(row.f_유닛ID, prefabName, System.StringComparison.OrdinalIgnoreCase))
                result = UnitData.FromDB(row);
        });
        return result;
    }
}
