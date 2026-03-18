using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 시 시각적 자식 오브젝트(visualRoot)를 흔들어 몸 떨림 효과를 냅니다.
/// 루트 Transform에 Rigidbody2D가 있는 경우 반드시 visualRoot를
/// SpriteRenderer만 갖는 자식 GameObject로 지정해야 물리 이동에 영향을 주지 않습니다.
///
/// [설정 방법]
///   플레이어 계층: Player (Rigidbody2D, Collider) → Visual (SpriteRenderer)
///   이 컴포넌트의 Visual Root 필드에 Visual 오브젝트를 연결하세요.
///   미연결 시 자식에서 SpriteRenderer를 자동 탐색합니다.
/// </summary>
public class HitShake : MonoBehaviour
{
    [SerializeField] private Transform visualRoot;    // 물리 없는 시각 자식
    [SerializeField] private float shakeDuration  = 0.22f;
    [SerializeField] private float shakeMagnitude = 0.07f;
    [SerializeField] private int   shakeSteps     = 6;

    private Coroutine shakeCoroutine;

    private void Awake()
    {
        // 미연결이면 자식에서 자동 탐색
        if (visualRoot == null)
        {
            var childSr = GetComponentInChildren<SpriteRenderer>(true);
            if (childSr != null && childSr.transform != transform)
                visualRoot = childSr.transform;
        }
    }

    public void Shake()
    {
        if (visualRoot == null) return; // visualRoot 없으면 동작하지 않음

        if (shakeCoroutine != null) StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(ShakeRoutine());
    }

    private IEnumerator ShakeRoutine()
    {
        Vector3 origin   = visualRoot.localPosition;
        float   interval = shakeDuration / shakeSteps;

        for (int i = 0; i < shakeSteps; i++)
        {
            Vector2 offset = Random.insideUnitCircle * shakeMagnitude;
            visualRoot.localPosition = origin + (Vector3)offset;
            yield return new WaitForSecondsRealtime(interval);
        }

        visualRoot.localPosition = origin;
        shakeCoroutine = null;
    }
}
