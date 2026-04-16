using System.Collections;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 기물 설치를 담당합니다.
/// 버튼 사용 즉시 플레이어 주변에 기물 프리팹을 설치합니다.
/// Inspector: placeRadius, minRadius
/// </summary>
public class StructurePlacer : MonoBehaviourSingleton<StructurePlacer>
{
    [Header("설치 위치 (플레이어 주변)")]
    [SerializeField] private float placeRadius = 2.5f;
    [SerializeField] private float minRadius   = 1.2f;

    protected override bool DontDestroy => false;

    // ── 외부 진입점 ─────────────────────────────────────────────────

    /// <summary>기물을 즉시 설치합니다.</summary>
    public void Place(DB_기물 data, Vector3 launchOriginWorld)
    {
        if (data == null) return;
        StartCoroutine(PlaceRoutine(data));
    }

    private IEnumerator PlaceRoutine(DB_기물 data)
    {
        Vector3 target = GetTargetPosition();

        SoundManager.Shared?.Play(SoundKey.Structure_Place);

        string installPath = GetInstallPath(data.f_기물ID);
        var installPrefab  = Resources.Load<GameObject>(installPath);
        if (installPrefab != null)
        {
            var go    = Instantiate(installPrefab, target, Quaternion.identity);
            int level = StructureWareManager.Shared?.GetLevel(data.f_기물ID) ?? 1;
            var skill = go.GetComponent<StructureSkillBase>();
            skill?.Init(data, level);
        }
        else
        {
            Debug.LogWarning($"[StructurePlacer] 설치 프리팹 없음: Resources/{installPath}");
        }

        var zone = FindAnyObjectByType<StructureZone>();
        zone?.OccupyCell(target);
        yield break;
    }

    // ── 경로 헬퍼 ───────────────────────────────────────────────────

    private static string GetInstallPath(string id)
    {
        if (string.IsNullOrEmpty(id)) return string.Empty;
        string[] parts   = id.Split('_');
        string   chapter = parts.Length > 0 ? parts[0] : id;
        return $"Ware/{chapter}/{id.ToLower()}_s";
    }

    // ── 설치 위치 결정 ───────────────────────────────────────────────

    private Vector3 GetTargetPosition()
    {
        var     player    = PlayerController.Instance;
        Vector3 playerPos = player != null ? (Vector3)(Vector2)player.Position : Vector3.zero;

        // 이동 방향 우선, 없으면 랜덤 방향
        Vector2 dir = player != null ? player.MoveDirection : Vector2.up;
        if (dir.sqrMagnitude < 0.01f)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
        }

        float   r         = Random.Range(minRadius, placeRadius);
        Vector3 candidate = playerPos + (Vector3)(dir.normalized * r);

        // MovementBounds 영역 안으로 제한
        var bounds = MovementBounds.Shared;
        if (bounds != null && !bounds.IsInside(candidate))
            candidate = bounds.ClosestPointOnBoundary(candidate);

        // StructureZone 그리드 스냅
        var zone = FindAnyObjectByType<StructureZone>();
        if (zone != null && zone.TryGetNearestCell(candidate, out Vector3 snapped))
            return snapped;

        return candidate;
    }

}
