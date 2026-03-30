using System.Collections;
using UnityEngine;

/// <summary>
/// 돌창 토템 기물 스킬.
/// 토템 프리팹이 설치되며, tickInterval마다 바람개비처럼 일정 각도 간격으로
/// 돌 투사체를 발사합니다.
///
/// mainValue    : 투사체 피해량
/// subValue     : 투사체 속도 (units/s)
/// radius       : 투사체 최대 사거리 (이 거리 도달 시 소멸)
/// tickInterval : 발사 주기 (초)
///
/// # Unity Setup:
///   1. 기물 프리팹에 StructureController + 이 스크립트를 부착.
///   2. projectilePrefab : 돌 투사체 프리팹 (SpriteRenderer 포함, Rigidbody2D 불필요).
///   3. projectileCount  : 한 번에 발사할 투사체 수 (바람개비 날 수).
///   4. rotationPerTick  : 매 틱마다 회전각 (°) — 바람개비 회전 효과.
/// </summary>
public class StoneTotemSkill : StructureSkillBase
{
    [Header("돌창 토템 설정")]
    [SerializeField] private GameObject projectilePrefab;
    [SerializeField] private int        projectileCount  = 4;      // 동시 발사 수
    [SerializeField] private float      rotationPerTick  = 45f;    // 틱마다 회전 (바람개비)
    [SerializeField] private float      projectileRadius = 0.15f;  // 적 충돌 감지 반경
    [SerializeField] private float      shotInterval     = 0.1f;   // 투사체 간 딜레이 (순차 발사)

    private float _currentAngle = 0f;   // 현재 발사 기준 각도

    public override void Activate()
    {
        if (projectilePrefab == null)
        {
            this.DError("[StoneTotemSkill] projectilePrefab이 연결되지 않아 임시 생성합니다.");
            projectilePrefab = new GameObject("TempStoneProj");
            projectilePrefab.SetActive(false);
            var sr = projectilePrefab.AddComponent<SpriteRenderer>();
            var tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(200, 200, 200, 255);
            tex.SetPixels32(pixels);
            tex.Apply(false);
            sr.sprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), Vector2.one * 0.5f, 16f);
            sr.color = Color.gray;
        }
        StartCoroutine(TotemRoutine());
    }

    IEnumerator TotemRoutine()
    {
        var wait = new WaitForSeconds(tickInterval);
        while (true)
        {
            StartCoroutine(FireVolleyRoutine(_currentAngle));  // 현재 각도를 스냅샷으로 전달
            StartCoroutine(ScalePulse());
            _currentAngle += rotationPerTick;
            yield return wait;
        }
    }

    IEnumerator FireVolleyRoutine(float baseAngle)
    {
        float angleStep = 360f / projectileCount;
        var delay = shotInterval > 0f ? new WaitForSeconds(shotInterval) : null;

        for (int i = 0; i < projectileCount; i++)
        {
            float angle = baseAngle + angleStep * i;  // 스냅샷 baseAngle 사용 — yield 사이에 _currentAngle이 바뀌어도 영향 없음
            float rad   = angle * Mathf.Deg2Rad;
            Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
            StartCoroutine(ProjectileRoutine(dir));
            if (delay != null) yield return delay;
        }
    }

    IEnumerator ScalePulse()
    {
        float half   = 0.08f;
        Vector3 orig  = transform.localScale;
        Vector3 small = orig * 0.75f;

        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(orig, small, t / half);
            yield return null;
        }
        for (float t = 0f; t < half; t += Time.deltaTime)
        {
            transform.localScale = Vector3.Lerp(small, orig, t / half);
            yield return null;
        }
        transform.localScale = orig;
    }

    IEnumerator ProjectileRoutine(Vector2 direction)
    {
        var go       = Instantiate(projectilePrefab, transform.position, Quaternion.identity);
        go.SetActive(true);
        float travel = 0f;
        
        float actualSpeed = subValue > 0.01f ? subValue : 5f; // 안전 장치

        // 창 끝(스프라이트 위쪽)이 이동 방향을 향하도록 정렬
        go.transform.up = (Vector3)direction;

        var hitSet = new System.Collections.Generic.HashSet<EnemyController>();

        while (travel < radius)
        {
            if (go == null) yield break;

            float step = actualSpeed * Time.deltaTime;
            go.transform.Translate(Vector2.up * step, Space.Self);
            travel += step;

            // 충돌 판정
            var cols = Physics2D.OverlapCircleAll(go.transform.position, projectileRadius);
            foreach (var col in cols)
            {
                if (!col.TryGetComponent<EnemyController>(out var enemy)) continue;
                if (hitSet.Contains(enemy)) continue;
                hitSet.Add(enemy);
                enemy.TakeDamage(mainValue);
            }

            yield return null;
        }

        if (go != null) Destroy(go);
    }

    protected override void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.8f, 0.6f, 0.2f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, radius);

        // 현재 발사 방향 미리보기
        float angleStep = 360f / Mathf.Max(1, projectileCount);
        Gizmos.color = new Color(1f, 0.8f, 0f, 0.8f);
        for (int i = 0; i < projectileCount; i++)
        {
            float angle = _currentAngle + angleStep * i;
            float rad   = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), Mathf.Sin(rad), 0f);
            Gizmos.DrawLine(transform.position, transform.position + dir * radius);
        }
    }
}
