using System.Collections;
using UnityEngine;

/// <summary>
/// N웨이브마다 보물 상자를 1개 스폰합니다.
/// 보스 웨이브에는 스폰하지 않으며, 해당 웨이브의 랜덤 시점에 낙하합니다.
///
/// 사용법: 인게임 씬 오브젝트에 컴포넌트로 추가.
///
/// 필요 리소스:
///   Resources/World/treasure_chest        — TreasureChest 프리팹
///   Resources/World/treasure_chest_marker — (선택) 착지 경고 마커 프리팹
/// </summary>
public class TreasureChestSpawner : MonoBehaviour
{
    [Header("스폰 주기")]
    [Tooltip("몇 웨이브마다 상자 1개를 스폰할지 설정합니다.")]
    [SerializeField] private int spawnEveryNWaves = 3;

    [Header("경고 마커 표시 시간 (초)")]
    [SerializeField] private float warningDuration = 1.2f;

    [Header("화면 상단 바깥 스폰 Y 오프셋")]
    [SerializeField] private float spawnAboveScreenOffset = 2f;

    private const string ChestPrefabPath  = "World/treasure_chest";
    private const string MarkerPrefabPath = "World/treasure_chest_marker";

    private GameObject _chestPrefab;
    private GameObject _markerPrefab;
    private bool       _prefabsLoaded;
    private Coroutine  _pendingSpawn;

    // ── 생명주기 ───────────────────────────────────────────────────

    private void OnEnable()  => GameEvents.OnWaveChanged += OnWaveChanged;
    private void OnDisable() => GameEvents.OnWaveChanged -= OnWaveChanged;

    // ── 웨이브 변경 이벤트 ─────────────────────────────────────────

    private void OnWaveChanged(int wave, bool isBoss)
    {
        this.DLog($"[TreasureChestSpawner] OnWaveChanged wave={wave} isBoss={isBoss} | {wave}%{spawnEveryNWaves}={wave % spawnEveryNWaves}");
        if (isBoss) return;
        if (wave % spawnEveryNWaves != 0) return;

        // 이전 예약 스폰이 남아 있으면 취소
        if (_pendingSpawn != null) StopCoroutine(_pendingSpawn);
        _pendingSpawn = StartCoroutine(SpawnChestRoutine());
    }

    // ── 스폰 루틴 ─────────────────────────────────────────────────

    private IEnumerator SpawnChestRoutine()
    {
        // 한 프레임 대기 — TeleportPlayerToMapCenter() 이후 위치를 읽기 위해
        yield return null;

        var gm = GameManager.Shared;
        if (gm == null || !gm.IsWaveActive || gm.IsBossWave)
        {
            this.DLog($"[TreasureChestSpawner] 스폰 취소 — gm={gm != null} IsWaveActive={gm?.IsWaveActive} IsBossWave={gm?.IsBossWave}");
            yield break;
        }

        LoadPrefabs();
        if (_chestPrefab == null)
        {
            this.DLog("[TreasureChestSpawner] treasure_chest 프리팹 없음 — Resources/World/treasure_chest 확인");
            yield break;
        }

        Vector2 landPos   = PickLandingPosition();
        float   spawnTopY = GetSpawnTopY();

        // 경고 마커 (선택 프리팹)
        GameObject marker = null;
        if (_markerPrefab != null)
            marker = Instantiate(_markerPrefab, new Vector3(landPos.x, landPos.y, 0f), Quaternion.identity);

        yield return new WaitForSeconds(warningDuration);

        if (marker != null) Destroy(marker);

        // 웨이브가 종료됐으면 취소
        if (gm == null || !gm.IsWaveActive || gm.IsBossWave) yield break;

        var go    = Instantiate(_chestPrefab);
        var chest = go.GetComponent<TreasureChest>();
        if (chest != null)
            chest.StartFall(landPos, spawnTopY);
        else
            Destroy(go);

        _pendingSpawn = null;
    }

    // ── 헬퍼 ──────────────────────────────────────────────────────

    private Vector2 PickLandingPosition()
    {
        Vector2 basePos = PlayerController.Instance != null
            ? (Vector2)PlayerController.Instance.Position
            : Vector2.zero;

        float angle  = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float radius = Random.Range(2f, 4f);
        Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
        Vector2 target = basePos + offset;

        if (MovementBounds.Shared != null)
        {
            MovementBounds.Shared.GetPlayerBoundsMinMax(out Vector2 min, out Vector2 max);
            target.x = Mathf.Clamp(target.x, min.x + 0.5f, max.x - 0.5f);
            target.y = Mathf.Clamp(target.y, min.y + 0.5f, max.y - 0.5f);
        }

        return target;
    }

    private float GetSpawnTopY()
    {
        var cam = Camera.main;
        if (cam == null) return 12f;
        Vector3 top = cam.ViewportToWorldPoint(new Vector3(0.5f, 1f, -cam.transform.position.z));
        return top.y + spawnAboveScreenOffset;
    }

    private void LoadPrefabs()
    {
        if (_prefabsLoaded) return;
        _prefabsLoaded = true;
        _chestPrefab   = Resources.Load<GameObject>(ChestPrefabPath);
        _markerPrefab  = Resources.Load<GameObject>(MarkerPrefabPath);
    }

    /// <summary>웨이브 클리어 시 GameManager에서 호출 — 활성 상자를 즉시 제거합니다.</summary>
    public static void ClearAll()
    {
        foreach (var chest in TreasureChest.All.ToArray())
            if (chest != null) Destroy(chest.gameObject);
    }
}
