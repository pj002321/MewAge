using System.Collections;
using Mewage.DB;
using UnityEngine;

/// <summary>
/// 기물 설치 연출을 담당합니다.
/// 1) 설치 위치에 인디케이터(반투명 마커) 표시
/// 2) 기물 슬롯 UI 위치에서 설치 위치로 스프라이트를 포물선 발사
/// 3) 착지 후 실제 기물 프리팹 설치
///
/// Inspector:
///   indicatorPrefab — 설치 위치 미리보기 GameObject (없으면 기물 스프라이트 사용)
///   launchSpeed     — 발사체 속도 (유닛/초)
///   arcHeight       — 포물선 최대 높이
///   placeRadius     — 플레이어 기준 설치 거리 (max)
///   minRadius       — 최소 거리
/// </summary>
public class StructurePlacer : MonoBehaviourSingleton<StructurePlacer>
{
    [Header("인디케이터 (설치 위치 미리보기)")]
    [SerializeField] private GameObject indicatorPrefab;
    [SerializeField] private Color      indicatorTint = new Color(0.4f, 0.9f, 1f, 0.45f);
    [SerializeField] private float      indicatorScale = 1.15f;

    [Header("발사체")]
    [SerializeField] private float launchSpeed = 14f;
    [SerializeField] private float arcHeight   = 3f;

    [Header("설치 위치 (플레이어 주변)")]
    [SerializeField] private float placeRadius = 2.5f;
    [SerializeField] private float minRadius   = 1.2f;

    protected override bool DontDestroy => false;

    // ── 외부 진입점 ─────────────────────────────────────────────────

    /// <summary>기물을 발사 연출과 함께 설치합니다.</summary>
    /// <param name="data">설치할 기물 DB 항목</param>
    /// <param name="launchOriginWorld">발사 시작점 (월드 좌표, 보통 UI 버튼을 변환한 위치)</param>
    public void Place(DB_기물 data, Vector3 launchOriginWorld)
    {
        if (data?.f_설치프리팹 == null) return;
        StartCoroutine(PlaceRoutine(data, launchOriginWorld));
    }

    // ── 연출 코루틴 ─────────────────────────────────────────────────

    private IEnumerator PlaceRoutine(DB_기물 data, Vector3 launchOrigin)
    {
        Vector3 target = GetTargetPosition();
        Sprite  sprite = data.f_설치프리팹.GetComponentInChildren<SpriteRenderer>()?.sprite;

        // 1. 인디케이터 — 착지 위치를 즉시 표시
        GameObject indicator = SpawnIndicator(target, sprite);

        // 2. 발사체 — 버튼(launchOrigin)에서 target으로 날아감
        GameObject proj = CreateSpriteGO("StructureProj", launchOrigin, sprite, Color.white, 1f);

        // 3. 포물선 이동
        float dist     = Vector3.Distance(launchOrigin, target);
        float duration = Mathf.Max(0.25f, dist / launchSpeed);
        float elapsed  = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float   t   = Mathf.Clamp01(elapsed / duration);
            float   arc = Mathf.Sin(t * Mathf.PI) * arcHeight;
            Vector3 pos = Vector3.Lerp(launchOrigin, target, t) + Vector3.up * arc;

            if (proj)
            {
                // 진행 방향으로 회전
                float   tNext    = Mathf.Clamp01((elapsed + Time.deltaTime) / duration);
                float   arcNext  = Mathf.Sin(tNext * Mathf.PI) * arcHeight;
                Vector3 posNext  = Vector3.Lerp(launchOrigin, target, tNext) + Vector3.up * arcNext;
                Vector3 forward  = posNext - pos;
                if (forward.sqrMagnitude > 0.0001f)
                    proj.transform.up = forward.normalized;

                proj.transform.position = pos;
            }

            yield return null;
        }

        // 4. 착지 — 연출 오브젝트 제거 후 실제 기물 설치
        if (proj)      Destroy(proj);
        if (indicator) Destroy(indicator);

        var go = Instantiate(data.f_설치프리팹, target, Quaternion.identity);

        // StructureZone 그리드 점유
        var zone = FindAnyObjectByType<StructureZone>();
        zone?.OccupyCell(target);
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

    // ── 헬퍼 ────────────────────────────────────────────────────────

    private GameObject SpawnIndicator(Vector3 pos, Sprite sprite)
    {
        if (indicatorPrefab != null)
            return Instantiate(indicatorPrefab, pos, Quaternion.identity);

        // indicatorPrefab 미설정 시 기물 스프라이트로 대체
        return CreateSpriteGO("Indicator", pos, sprite, indicatorTint, indicatorScale);
    }

    private static GameObject CreateSpriteGO(string name, Vector3 pos, Sprite sprite, Color color, float scale)
    {
        var go = new GameObject(name);
        go.transform.position   = pos;
        go.transform.localScale = Vector3.one * scale;

        if (sprite != null)
        {
            var sr        = go.AddComponent<SpriteRenderer>();
            sr.sprite       = sprite;
            sr.color        = color;
            sr.sortingOrder = 20;
        }
        return go;
    }
}
