using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// 메테오 기물 스킬.
/// 설치 위치를 기준으로 사선(우상→좌하)으로 meteorsCount개의 메테오 프리팹을 떨어뜨립니다.
/// 메테오가 착지 시 mainValue 피해를 radius 범위 내 적에게 줍니다.
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. meteorPrefab : 사선으로 날아가는 돌 프리팹 (SpriteRenderer + Rigidbody2D Kinematic).
///   3. impactRadius : 착지 충격 범위.
///   4. meteorInterval : 메테오 사이 간격(초).
///   5. meteorsCount : 총 메테오 수.
///   6. spreadRange : 착지 지점 좌우 랜덤 범위.
/// </summary>
public class MeteorSkill : StructureSkillBase
{
    [Header("메테오 설정")]
    [SerializeField] private GameObject meteorPrefab;
    [SerializeField] private int        meteorsCount   = 5;
    [SerializeField] private float      impactRadius   = 1.2f;
    [SerializeField] private float      spreadRange    = 3f;     // 착지 지점 좌우 분산
    [SerializeField] private float      fallHeight     = 8f;     // 출발 높이 오프셋
    [SerializeField] private float      fallDuration   = 0.5f;   // 각 메테오 낙하 시간

    [Header("조명 어둡게")]
    [SerializeField] private float dimAmount   = 0.45f;   // 어두울 때 밝기 (0=완전어둠, 1=그대로)
    [SerializeField] private float fadeDuration = 0.3f;   // 페이드 in/out 시간

    public override void Activate() => StartCoroutine(MeteorRoutine());

    IEnumerator MeteorRoutine()
    {
        if (meteorPrefab == null)
        {
            this.DError("[MeteorSkill] meteorPrefab이 연결되지 않았습니다.");
            yield break;
        }

        Light2D globalLight = FindGlobalLight();
        yield return StartCoroutine(FadeLight(globalLight, 1f, dimAmount, fadeDuration));

        // duration 내에 모든 메테오가 떨어지도록 간격 자동 계산
        float structDuration = GetComponent<StructureController>()?.Duration ?? 0f;
        float available = structDuration > 0f
            ? structDuration - fadeDuration * 2f - fallDuration
            : meteorsCount * 0.4f;
        float meteorInterval = Mathf.Max(0.05f, available / meteorsCount);

        var wait = new WaitForSeconds(meteorInterval);
        for (int i = 0; i < meteorsCount; i++)
        {
            // 착지 목표: MovementBounds 내 랜덤 위치 (없으면 기물 주변 fallback)
            Vector2 landPos = GetRandomPointInBounds();
            // 출발 위치: 착지 지점 우상단
            Vector2 startPos = landPos + new Vector2(fallHeight * 0.7f, fallHeight);

            StartCoroutine(FallRoutine(startPos, landPos));
            yield return wait;
        }

        // 마지막 메테오 낙하 완료 대기 후 복구
        yield return new WaitForSeconds(fallDuration);
        yield return StartCoroutine(FadeLight(globalLight, dimAmount, 1f, fadeDuration));
    }

    private IEnumerator FadeLight(Light2D light, float from, float to, float duration)
    {
        if (light == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            light.intensity = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        light.intensity = to;
    }

    private void OnDestroy()
    {
        // 코루틴이 중단되더라도 조명이 어두운 채로 남지 않도록 즉시 복구
        var light = FindGlobalLight();
        if (light != null) light.intensity = 1f;
    }

    private static Light2D FindGlobalLight()
    {
        foreach (var l in Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None))
            if (l.lightType == Light2D.LightType.Global) return l;
        return null;
    }

    // 카메라 시야 안쪽에서 랜덤 착지 지점을 반환합니다.
    Vector2 GetRandomPointInBounds()
    {
        var cam = Camera.main;
        if (cam == null)
            return (Vector2)transform.position + new Vector2(Random.Range(-spreadRange, spreadRange), 0f);

        // 뷰포트 (0,0)~(1,1) → 월드 좌표 AABB
        Vector3 bl = cam.ViewportToWorldPoint(new Vector3(0f, 0f, cam.nearClipPlane));
        Vector3 tr = cam.ViewportToWorldPoint(new Vector3(1f, 1f, cam.nearClipPlane));

        // 가장자리 여백 (화면 끝에 붙어 떨어지지 않도록)
        float margin = 0.5f;
        float x = Random.Range(bl.x + margin, tr.x - margin);
        float y = Random.Range(bl.y + margin, tr.y - margin);
        return new Vector2(x, y);
    }

    IEnumerator FallRoutine(Vector2 from, Vector2 to)
    {
        var go = Instantiate(meteorPrefab, from, Quaternion.identity);
        float elapsed = 0f;

        while (elapsed < fallDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fallDuration);
            go.transform.position = Vector2.Lerp(from, to, t);
            yield return null;
        }

        go.transform.position = to;

        // 착지 충격
        var hits = Physics2D.OverlapCircleAll(to, impactRadius);
        foreach (var col in hits)
            if (col.TryGetComponent<EnemyController>(out var enemy))
                enemy.TakeDamage(mainValue);

        Destroy(go);
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, spreadRange);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.4f);
        Gizmos.DrawWireSphere(transform.position, impactRadius);
    }
}
