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

    private SpriteRenderer[] srs;
    [HideInInspector] public Color BaseColor = Color.white;
    private Coroutine      flashCoroutine;

    private void Awake()
    {
        srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs.Length == 0) srs = GetComponentsInParent<SpriteRenderer>();
        if (srs.Length > 0) BaseColor = srs[0].color;
    }

    public void Flash()
    {
        if (srs == null || srs.Length == 0) return;
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        foreach(var sr in srs) sr.color = flashColor;
        yield return new WaitForSeconds(flashDuration);
        foreach(var sr in srs) sr.color = BaseColor;
    }
}
