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

    [Header("보스 알림 (선택 — 없으면 즉시 스폰)")]
    [SerializeField] private GameObject bossNotiObject;

    protected override bool DontDestroy => false;

    public void Play(System.Action onComplete)
    {
        StartCoroutine(Sequence(onComplete));
    }

    public IEnumerator PlayBossNoti()
    {
        if (bossNotiObject == null) yield break;
        bossNotiObject.SetActive(true);
        yield return null; // Animator 초기화 대기 (1프레임)
        var anim = bossNotiObject.GetComponent<Animator>();
        if (anim != null)
        {
            float clipLength = anim.GetCurrentAnimatorStateInfo(0).length;
            yield return new WaitForSeconds(clipLength);
        }
        bossNotiObject.SetActive(false);
    }

    private IEnumerator Sequence(System.Action onComplete)
    {
        var switcher  = WaveEnvironmentSwitcher.Shared;
        var exitPoint = switcher?.CurrentBossExitPoint;
        // enterPoint는 SetBoss() 이후에 세팅되므로 나중에 읽음

        var player = PlayerController.Instance;

        // 1. 입력 잠금
        if (player != null) player.InputLocked = true;

        // 2. exitPoint 로 걸어서 이동
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

        // 4. 보스 환경 전환 — 이 시점에 CurrentBossEnterPoint 세팅됨
        switcher?.SetBoss();

        // 5. 플레이어 텔레포트 (SetBoss 이후에 읽어야 올바른 위치)
        var enterPoint = switcher?.CurrentBossEnterPoint;
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

        // 7. BossNoti 활성화 → 애니메이션 종료 대기
        if (bossNotiObject != null)
        {
            bossNotiObject.SetActive(true);
            yield return null; // Animator 초기화 대기 (1프레임)
            var anim = bossNotiObject.GetComponent<Animator>();
            if (anim != null)
            {
                float clipLength = anim.GetCurrentAnimatorStateInfo(0).length;
                yield return new WaitForSeconds(clipLength);
            }
            bossNotiObject.SetActive(false);
        }

        // 8. 입력 해제 + 콜백 (스폰 마커 활성화)
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
