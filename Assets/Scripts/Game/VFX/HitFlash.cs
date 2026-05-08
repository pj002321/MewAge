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
    private Color[]          _cachedColors;
    private Material         _instanceFlashMat;
    private Coroutine        flashCoroutine;
    private bool             _isFlashing;

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

    private void CacheColors()
    {
        if (srs == null) return;
        _cachedColors = new Color[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            if (srs[i] != null) _cachedColors[i] = srs[i].color;
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
        if (!_isFlashing)
        {
            CacheOriginalMats();
            CacheColors();
        }

        _isFlashing = true;
        foreach (var sr in srs)
            if (sr != null) { sr.material = flashMat; sr.color = Color.white; }

        yield return new WaitForSeconds(flashDuration);

        RestoreMats();
        _isFlashing = false;
        flashCoroutine = null;
    }

    private void RestoreMats()
    {
        if (originalMats == null || srs == null) return;
        for (int i = 0; i < srs.Length && i < originalMats.Length; i++)
        {
            if (srs[i] == null) continue;
            if (originalMats[i] != null) srs[i].material = originalMats[i];
            if (_cachedColors != null && i < _cachedColors.Length)
                srs[i].color = _cachedColors[i];
        }
    }

    private void OnDisable()
    {
        if (_isFlashing)
        {
            RestoreMats();
            _isFlashing = false;
        }
        StopAllCoroutines();
    }

    private void OnDestroy()
    {
        RestoreMats();
    }
}
