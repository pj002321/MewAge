using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 시 SpriteSilhouette 쉐이더로 스프라이트를 완전히 하얗게 Flash합니다.
/// PlayerController / EnemyController에서 Flash()를 호출하세요.
/// </summary>
public class HitFlash : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.1f;
    [SerializeField] private Color flashColor    = Color.white;

    private SpriteRenderer[] srs;
    private Material[]       originalMats;
    private Material         _instanceFlashMat;
    private Coroutine        flashCoroutine;
    private bool             _isFlashing;

    // BaseColor는 하위 호환성 유지
    [HideInInspector] public Color BaseColor = Color.white;

    private Material GetFlashMat()
    {
        if (_instanceFlashMat != null) return _instanceFlashMat;
        var shader = Shader.Find("Custom/SpriteSilhouette");
        if (shader == null) return null;
        _instanceFlashMat = new Material(shader);
        _instanceFlashMat.SetColor("_Color", flashColor);
        return _instanceFlashMat;
    }

    private void Awake()
    {
        RefreshRenderers();
    }

    /// <summary>모델이 동적으로 교체된 후 호출해 SpriteRenderer 캐시를 갱신합니다.</summary>
    public void RefreshRenderers()
    {
        srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs.Length == 0) srs = GetComponentsInParent<SpriteRenderer>();
        CacheOriginalMats();
    }

    private void CacheOriginalMats()
    {
        if (srs == null || srs.Length == 0) return;
        originalMats = new Material[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null) originalMats[i] = srs[i].material;
    }

    public void Flash()
    {
        if (srs == null || srs.Length == 0) return;
        var mat = GetFlashMat();
        if (mat == null) return;

        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(FlashRoutine(mat));
    }

    private IEnumerator FlashRoutine(Material flashMat)
    {
        // 이미 플래시 중이라면 원본 재질을 다시 캐싱하지 않습니다.
        // (하얗게 변한 재질을 원본으로 오인해 저장하는 현상 방지)
        if (!_isFlashing)
            CacheOriginalMats();

        _isFlashing = true;
        foreach (var sr in srs) 
            if (sr != null) sr.material = flashMat;

        yield return new WaitForSeconds(flashDuration);

        RestoreMats();
        _isFlashing = false;
        flashCoroutine = null;
    }

    private void RestoreMats()
    {
        if (originalMats == null || srs == null) return;
        for (int i = 0; i < srs.Length && i < originalMats.Length; i++)
            if (srs[i] != null && originalMats[i] != null)
                srs[i].material = originalMats[i];
    }

    private void OnDisable()
    {
        // 도중에 비활성화되더라도 재질이 하얗게 남지 않도록 복구
        if (_isFlashing)
        {
            RestoreMats();
            _isFlashing = false;
        }
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        // 파괴 시 재질 복구 (코루틴 중단으로 복구 안 된 경우 대비)
        RestoreMats();
    }
}

