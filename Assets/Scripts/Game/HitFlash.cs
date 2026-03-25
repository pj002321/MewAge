using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 시 SpriteRenderer를 빨간색으로 잠깐 Flash합니다.
/// PlayerController / EnemyController에서 Flash()를 호출하세요.
/// </summary>
public class HitFlash : MonoBehaviour
{
    [SerializeField] private Color  flashColor    = new(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float  flashDuration = 0.12f;

    private SpriteRenderer sr;
    private Color          originalColor;
    private Coroutine      flashCoroutine;

    private void Awake()
    {
        sr            = GetComponentInChildren<SpriteRenderer>(true);
        if (sr == null) sr = GetComponentInParent<SpriteRenderer>();
        if (sr != null) originalColor = sr.color;
    }

    public void Flash()
    {
        if (sr == null) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        sr.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        sr.color = originalColor;
    }
}
