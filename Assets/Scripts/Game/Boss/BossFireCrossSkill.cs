using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 스킬: 8방향 화염 확산.
///
/// 흐름:
///   정지 → 8방향 경고 라인 표시 (warningDuration)
///   → 경고 제거 + 화염 타일 확산 (spreadDelay 간격)
///   → fireDuration 유지 → 타일 소멸 → 종료
/// </summary>
public class BossFireCrossSkill : BossSkillBase
{
    [Header("타일 프리팹")]
    [Tooltip("화염 타일 프리팹. BoxCollider2D가 없으면 자동으로 추가합니다.")]
    [SerializeField] private GameObject tilePrefab;

    [Header("경고 라인")]
    [SerializeField] private float    warningDuration   = 1.2f;
    [SerializeField] private float    warningLineWidth  = 0.08f;
    [SerializeField] private Color    warningColor      = new Color(1f, 0.3f, 0f, 1f);
    [Tooltip("Sprites/Default 또는 URP Unlit 머티리얼. null이면 자동 생성")]
    [SerializeField] private Material warningMaterial;

    [Header("확산 설정")]
    [Tooltip("보스 위치에서 뻗어나가는 타일 수 (중심 제외)")]
    [SerializeField] private int   spreadLength = 5;
    [Tooltip("격자 크기 (타일 프리팹 크기와 맞춰야 합니다)")]
    [SerializeField] private float cellSize     = 1f;
    [Tooltip("한 칸씩 깔리는 간격 (초). 0이면 즉시 전개.")]
    [SerializeField] private float spreadDelay  = 0.05f;

    [Header("지속 시간")]
    [Tooltip("타일이 깔린 뒤 유지되는 시간 (초)")]
    [SerializeField] private float fireDuration = 3f;

    [Header("피해")]
    [SerializeField] private float tileDamage    = 10f;
    [Tooltip("타일 위에 서 있을 때 피해 간격 (초)")]
    [SerializeField] private float tickInterval  = 0.5f;

    // 4방향 (십자)
    private static readonly Vector2[] CrossDirs =
    {
        Vector2.up, Vector2.down, Vector2.left, Vector2.right,
    };

    // 4방향 (대각)
    private static readonly Vector2[] DiagDirs =
    {
        new Vector2( 1f,  1f).normalized,
        new Vector2(-1f,  1f).normalized,
        new Vector2( 1f, -1f).normalized,
        new Vector2(-1f, -1f).normalized,
    };

    private static IEnumerable<Vector2> AllDirs()
    {
        foreach (var d in CrossDirs) yield return d;
        foreach (var d in DiagDirs)  yield return d;
    }

    // ── 스킬 실행 ─────────────────────────────────────────────

    protected override IEnumerator OnExecute()
    {
        if (tilePrefab == null)
        {
            this.DError("[BossFireCrossSkill] tilePrefab이 연결되지 않았습니다.");
            yield break;
        }

        Boss.IsFiringSkill = true;
        Owner.EnemyAnimator?.SetBool(EnemyAnim.BMove, false);

        var warnLines = new List<LineRenderer>();
        var tiles     = new List<GameObject>();

        try
        {
            Vector2 origin   = Owner.transform.position;
            float totalLen   = spreadLength * cellSize;

            // ① 경고 라인 생성 (BossChargeSkill 방식)
            foreach (var dir in AllDirs())
                warnLines.Add(SpawnWarningLine(origin, dir, totalLen));

            // 경고 라인 점진적으로 뻗어나가기
            float elapsed = 0f;
            while (elapsed < warningDuration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / warningDuration);
                foreach (var lr in warnLines)
                {
                    if (lr == null) continue;
                    // lr.GetPosition(0) 이 origin, dir는 userData 대신 (pos1 - pos0).normalized 로 복원
                    Vector3 start = lr.GetPosition(0);
                    Vector3 dir3  = (lr.GetPosition(2) - start).normalized; // pos2에 방향 저장
                    lr.SetPosition(1, start + dir3 * totalLen * t);
                }
                yield return null;
            }

            // 경고 라인 제거
            foreach (var lr in warnLines) if (lr != null) Destroy(lr.gameObject);
            warnLines.Clear();

            // ② 화염 타일 확산
            origin = Owner.transform.position; // 경고 중 보스가 이동했을 수 있으므로 재샘플
            var wait = spreadDelay > 0f ? new WaitForSeconds(spreadDelay) : null;

            // 중심 타일
            SpawnTile(origin, tiles);

            for (int step = 1; step <= spreadLength; step++)
            {
                foreach (var dir in AllDirs())
                    SpawnTile((Vector2)(origin) + dir * cellSize * step, tiles);

                if (wait != null) yield return wait;
            }

            // ③ 지속 시간 대기
            yield return new WaitForSeconds(fireDuration);
        }
        finally
        {
            foreach (var lr in warnLines) if (lr != null) Destroy(lr.gameObject);
            foreach (var t in tiles)      if (t != null) Destroy(t);
            if (Boss != null) Boss.IsFiringSkill = false;
        }
    }

    // ── 경고 라인 ─────────────────────────────────────────────

    /// <summary>
    /// LineRenderer로 경고 라인을 생성합니다.
    /// pos[0]=origin, pos[1]=현재 끝점(애니메이션), pos[2]=방향 인코딩(origin+dir)
    /// </summary>
    private LineRenderer SpawnWarningLine(Vector2 origin, Vector2 dir, float totalLen)
    {
        var go = new GameObject("WarnLine");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace  = true;
        lr.positionCount  = 3;
        lr.SetPosition(0, origin);
        lr.SetPosition(1, origin);                  // 뻗어나갈 끝점 (애니메이션)
        lr.SetPosition(2, (Vector3)origin + (Vector3)dir); // 방향 인코딩용 더미
        lr.startWidth     = warningLineWidth;
        lr.endWidth       = warningLineWidth;
        lr.material       = warningMaterial != null
            ? warningMaterial
            : new Material(Shader.Find("Sprites/Default"));
        lr.startColor     = warningColor;
        lr.endColor       = warningColor;
        lr.sortingOrder   = 10;
        return lr;
    }

    // ── 타일 스폰 ─────────────────────────────────────────────

    private void SpawnTile(Vector2 pos, List<GameObject> list)
    {
        var go = Instantiate(tilePrefab, pos, Quaternion.identity);

        // 콜라이더 보장
        var col = go.GetComponent<BoxCollider2D>();
        if (col == null) col = go.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size      = Vector2.one * cellSize * 0.9f;

        // 피해 컴포넌트
        var tile = go.GetComponent<BossFireTile>() ?? go.AddComponent<BossFireTile>();
        tile.Init(tileDamage, tickInterval);

        list.Add(go);
    }

    // ── 에디터 기즈모 ─────────────────────────────────────────

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // 십자
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawWireCube(transform.position, Vector3.one * cellSize * 0.9f);
        foreach (var dir in CrossDirs)
            for (int i = 1; i <= spreadLength; i++)
                Gizmos.DrawWireCube(transform.position + (Vector3)(dir * cellSize * i),
                                    Vector3.one * cellSize * 0.9f);

        // 대각
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.35f);
        foreach (var dir in DiagDirs)
            for (int i = 1; i <= spreadLength; i++)
                Gizmos.DrawWireCube(transform.position + (Vector3)(dir * cellSize * i),
                                    Vector3.one * cellSize * 0.9f);
    }
#endif
}
