using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

/// <summary>
/// Boss · NonBoss 적이 공통으로 꺼내 쓸 수 있는 전투 행동 라이브러리.
///
/// ▸ 사용 방법 (코루틴 내부에서)
///     yield return StartCoroutine(EnemyCombatRoutines.TeleportSequence(host, renderers, cfg, ...));
///
/// ▸ 새 공통 행동 추가
///     이 파일에 static IEnumerator 을 추가하고, 필요한 파라미터는 Config struct 로 묶으세요.
///
/// ▸ 비주얼 이펙트 (페이드·바운스·ScaleY)
///     → EnemyFX 클래스 참조
/// </summary>
public static class EnemyCombatRoutines
{
    // ══════════════════════════════════════════════════════════════
    // 텔레포트 (Digging) 시퀀스
    // ══════════════════════════════════════════════════════════════

    public struct TeleportConfig
    {
        public float      PreDigDelay;
        public float      FadeOutDuration;
        public float      FadeInDuration;
        public float      SpawnRadiusMin;
        public float      SpawnRadiusMax;
        public GameObject SpawnMarkerPrefab;
        public float      MarkerDuration;
        public float      AnimEventTimeout;
    }

    /// <summary>
    /// 순간이동 전체 시퀀스 (EnemyBehaviourBase · BossSkillBase 양쪽에서 사용).
    ///
    /// <paramref name="digAnimEnded"/> / <paramref name="spawnAnimEnded"/> 는
    /// 호출자가 Animation Event 핸들러에서 <c>true</c> 로 설정합니다.
    /// </summary>
    public static IEnumerator TeleportSequence(
        IEnemyHost        host,
        SpriteRenderer[]  renderers,
        TeleportConfig    cfg,
        System.Func<bool> digAnimEnded,
        System.Func<bool> spawnAnimEnded)
    {
        // ① PreDig 대기
        if (cfg.PreDigDelay > 0f)
            yield return new WaitForSeconds(cfg.PreDigDelay);

        // ② 숨기 애니메이션 대기
        host.EnemyAnimator?.SetBool(EnemyAnim.BDigging, true);
        yield return WaitForAnimEnd(digAnimEnded, cfg.AnimEventTimeout);

        // ③ 페이드 아웃
        yield return EnemyFX.FadeSprites(renderers, 0f, cfg.FadeOutDuration).WaitForCompletion();

        // ④ 목적지 결정 + 마커 표시 + 대기
        Vector2 spawnPos = PickSpawnPosition(host, cfg.SpawnRadiusMin, cfg.SpawnRadiusMax);
        ShowMarker(spawnPos, cfg.SpawnMarkerPrefab, cfg.MarkerDuration);
        yield return new WaitForSeconds(cfg.MarkerDuration);

        // ⑤ 이동 + 스폰 애니메이션 (페이드 인 병렬)
        host.SelfTransform.position = spawnPos;
        if (host.Rb != null) host.Rb.position = spawnPos;

        host.EnemyAnimator?.SetBool(EnemyAnim.BDigging, false);
        EnemyFX.FadeSprites(renderers, 1f, cfg.FadeInDuration);   // fire-and-forget: 애니와 병렬
        yield return WaitForAnimEnd(spawnAnimEnded, cfg.AnimEventTimeout);

        // ⑥ 알파 최종 보정
        EnemyFX.SetAlpha(renderers, 1f);
    }

    // ══════════════════════════════════════════════════════════════
    // 공통 유틸리티
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// <paramref name="condition"/> 이 <c>true</c> 가 되거나 <paramref name="timeout"/> 초가 지날 때까지 대기합니다.
    /// 애니메이션 이벤트가 누락될 때의 안전 타임아웃으로 활용합니다.
    /// </summary>
    public static IEnumerator WaitForAnimEnd(System.Func<bool> condition, float timeout)
    {
        float elapsed = 0f;
        while (!condition() && elapsed < timeout)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    /// <summary>플레이어 기준 반경 내 랜덤 위치를 반환합니다.</summary>
    public static Vector2 PickSpawnPosition(IEnemyHost host, float minRadius, float maxRadius)
    {
        if (host.PlayerTransform == null)
            return host.SelfTransform.position;
        float angle = Random.Range(0f, Mathf.PI * 2f);
        float dist  = Random.Range(minRadius, maxRadius);
        return (Vector2)host.PlayerTransform.position
             + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist;
    }

    private static void ShowMarker(Vector2 pos, GameObject prefab, float duration)
    {
        if (prefab == null) return;
        var go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
        go.GetComponent<TeleportMarker>()?.Show(duration);
    }

    // ══════════════════════════════════════════════════════════════
    // 경고 라인 헬퍼  (Charge · 투사체 예고 등에서 공유)
    // ══════════════════════════════════════════════════════════════

    /// <summary>
    /// LineRenderer 경고선을 월드 공간에 생성하고 반환합니다.
    /// 호출자는 매 프레임 <see cref="UpdateLength"/> 로 끝점을 갱신하고,
    /// 완료 후 <see cref="Destroy"/> 로 제거합니다.
    /// </summary>
    public static class WarningLine
    {
        private static readonly List<LineRenderer> _all = new();

        public static LineRenderer Spawn(Vector2 origin, float lineWidth, Color color,
                                         Material material = null)
        {
            var go = new GameObject("WarnLine");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace     = true;
            lr.positionCount     = 2;
            lr.startWidth        = lineWidth;
            lr.endWidth          = lineWidth;
            lr.startColor        = color;
            lr.endColor          = color;
            lr.sortingOrder      = 10;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.material          = material != null
                ? material
                : new Material(Shader.Find("Sprites/Default"));
            lr.SetPosition(0, origin);
            lr.SetPosition(1, origin);
            _all.Add(lr);
            return lr;
        }

        /// <summary>경고선 끝점을 <paramref name="direction"/> 방향으로 <c>maxLength × progress</c> 만큼 업데이트합니다.</summary>
        public static void UpdateLength(LineRenderer lr, Vector2 origin,
                                        Vector2 direction, float maxLength, float progress)
        {
            if (lr == null) return;
            lr.SetPosition(0, origin);
            lr.SetPosition(1, origin + direction * (maxLength * Mathf.Clamp01(progress)));
        }

        public static void Destroy(LineRenderer lr)
        {
            _all.Remove(lr);
            if (lr != null) UnityEngine.Object.Destroy(lr.gameObject);
        }

        /// <summary>씬에 남아있는 모든 경고선을 일괄 제거합니다. 웨이브 전환 시 호출하세요.</summary>
        public static void DestroyAll()
        {
            for (int i = _all.Count - 1; i >= 0; i--)
            {
                var lr = _all[i];
                if (lr != null) UnityEngine.Object.Destroy(lr.gameObject);
            }
            _all.Clear();
        }
    }
}
