using DG.Tweening;
using UnityEngine;

/// <summary>
/// 적·보스 공통 DOTween 비주얼 이펙트 헬퍼 (정적 유틸리티).
///
/// 코루틴 없이 Tween/Sequence 를 반환하므로
///   yield return tween.WaitForCompletion()  — 대기
///   tween 반환값 무시                        — fire-and-forget
/// 두 방식 모두 지원합니다.
/// </summary>
public static class EnemyFX
{
    // ── 페이드 ────────────────────────────────────────────────────

    /// <summary>SpriteRenderer 배열의 알파를 <paramref name="to"/> 로 동시에 페이드합니다.</summary>
    public static Sequence FadeSprites(SpriteRenderer[] srs, float to, float duration, Ease ease = Ease.Linear)
    {
        var seq = DOTween.Sequence();
        if (srs == null) return seq;
        foreach (var sr in srs)
            if (sr != null) seq.Join(sr.DOFade(to, duration).SetEase(ease));
        return seq;
    }

    /// <summary>알파를 즉시 설정합니다 (코루틴 완료 후 보정용).</summary>
    public static void SetAlpha(SpriteRenderer[] srs, float alpha)
    {
        if (srs == null) return;
        foreach (var sr in srs)
        {
            if (sr == null) continue;
            var c = sr.color;
            c.a   = alpha;
            sr.color = c;
        }
    }

    // ── 스폰 바운스 ───────────────────────────────────────────────

    /// <summary>
    /// 스폰 시 팝인 바운스 시퀀스.
    /// <paramref name="spawnScale"/> 기준으로 0 → 과슈트 → 안착까지 0.48 초 재생합니다.
    /// </summary>
    public static Sequence SpawnBounce(Transform t, Vector3 spawnScale, TweenCallback onComplete = null)
    {
        t.localScale = Vector3.zero;
        var seq = DOTween.Sequence().SetLink(t.gameObject);
        seq.Append(t.DOScale(spawnScale * 1.30f, 0.20f).SetEase(Ease.OutQuad));
        seq.Append(t.DOScale(spawnScale * 0.85f, 0.12f).SetEase(Ease.InQuad));
        seq.Append(t.DOScale(spawnScale * 1.10f, 0.08f).SetEase(Ease.OutQuad));
        seq.Append(t.DOScale(spawnScale,          0.08f).SetEase(Ease.InOutSine));
        if (onComplete != null) seq.OnComplete(onComplete);
        return seq;
    }

    // ── 스케일 Y (보스 입장 squash/stretch) ──────────────────────

    /// <summary>
    /// X/Z 는 <paramref name="originalScale"/> 그대로, Y 만 <paramref name="targetYMult"/> 배율로 트위닝합니다.
    /// BossController 입장 낙하 squash·stretch 에 사용합니다.
    /// </summary>
    public static Tween ScaleY(Transform t, Vector3 originalScale, float targetYMult,
                               float duration, Ease ease = Ease.Linear)
    {
        var target = new Vector3(originalScale.x, originalScale.y * targetYMult, originalScale.z);
        return t.DOScale(target, duration).SetEase(ease).SetLink(t.gameObject);
    }
}
