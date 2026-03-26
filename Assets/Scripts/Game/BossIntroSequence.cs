using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 웨이브 진입 컷씬.
/// 출구/입장 위치는 WaveEnvironmentSwitcher 에서 맵별로 관리합니다.
///   - 일반 맵 출구: WaveBackgroundEntry.bossExitPoint  (맵마다 다름)
///   - 보스 맵 입장: WaveEnvironmentSwitcher.bossEnterPoint (보스 환경마다 다름)
///
/// 흐름:
///   1. 플레이어 입력 잠금
///   2. bossExitPoint 로 걸어서 이동 (null이면 생략)
///   3. 페이드 아웃
///   4. 보스 환경 전환 (WaveEnvironmentSwitcher.SetBoss)
///   5. 플레이어 → bossEnterPoint 텔레포트 (null이면 생략)
///   6. 페이드 인
///   7. 입력 해제 → onComplete 콜백
/// </summary>
public class BossIntroSequence : MonoBehaviourSingleton<BossIntroSequence>
{
    [Header("이동 속도")]
    public float walkSpeed = 4f;

    [Header("페이드 (선택 — 없으면 즉시 전환)")]
    public CanvasGroup fadeCanvasGroup;
    public float       fadeDuration = 0.5f;

    protected override bool DontDestroy => false;

    public void Play(System.Action onComplete)
    {
        StartCoroutine(Sequence(onComplete));
    }

    private IEnumerator Sequence(System.Action onComplete)
    {
        var switcher   = WaveEnvironmentSwitcher.Shared;
        var exitPoint  = switcher?.CurrentBossExitPoint;
        var enterPoint = switcher?.CurrentBossEnterPoint;

        var player = PlayerController.Instance;

        // 1. 입력 잠금
        if (player != null) player.InputLocked = true;

        // 2. exitPoint 로 걸어서 이동 (Rigidbody2D 물리와 충돌하지 않도록 rb.MovePosition 사용)
        if (player != null && exitPoint != null)
        {
            var walkRb = player.GetComponent<Rigidbody2D>();
            Vector2 target = exitPoint.position;
            while (true)
            {
                Vector2 cur = walkRb != null ? walkRb.position : (Vector2)player.transform.position;
                if (Vector2.Distance(cur, target) <= 0.08f) break;
                Vector2 next = Vector2.MoveTowards(cur, target, walkSpeed * Time.fixedDeltaTime);
                if (walkRb != null)
                    walkRb.MovePosition(next);
                else
                    player.transform.position = next;
                yield return new WaitForFixedUpdate();
            }
            if (walkRb != null)
                walkRb.MovePosition(target);
            else
                player.transform.position = target;
            yield return new WaitForFixedUpdate();
        }

        // 3. 페이드 아웃
        yield return StartCoroutine(Fade(1f));

        // 4. 보스 환경 전환
        switcher?.SetBoss();

        // 5. 플레이어 텔레포트
        if (player != null && enterPoint != null)
        {
            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.position       = enterPoint.position;
                rb.linearVelocity = Vector2.zero;
            }
            else
            {
                player.transform.position = enterPoint.position;
            }
        }

        // 6. 페이드 인
        yield return StartCoroutine(Fade(0f));

        // 7. 입력 해제 + 콜백
        if (player != null) player.InputLocked = false;
        onComplete?.Invoke();
    }

    private IEnumerator Fade(float target)
    {
        if (fadeCanvasGroup == null) yield break;

        float start   = fadeCanvasGroup.alpha;
        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            fadeCanvasGroup.alpha = Mathf.Lerp(start, target, elapsed / fadeDuration);
            yield return null;
        }
        fadeCanvasGroup.alpha = target;
    }
}
