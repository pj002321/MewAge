using System.Collections;
using UnityEngine;

/// <summary>
/// 피격 시 SpriteSilhouette 쉐이더로 스프라이트를 완전히 하얗게 Flash합니다.
/// PlayerController / EnemyController에서 Flash()를 호출하세요.
/// </summary>
public class HitFlash : MonoBehaviour
{
    [SerializeField] private float flashDuration = 0.1f;

    private SpriteRenderer[] srs;
    private Material[]       originalMats;
    private Coroutine        flashCoroutine;

    // 씬 전체 공유 흰색 실루엣 재질
    private static Material _flashMat;
    private static Material GetFlashMat()
    {
        if (_flashMat != null) return _flashMat;
        var shader = Shader.Find("Custom/SpriteSilhouette");
        if (shader == null) return null;
        _flashMat = new Material(shader);
        _flashMat.SetColor("_Color", Color.white);
        return _flashMat;
    }

    // BaseColor는 하위 호환성 유지 (외부에서 참조 중인 코드 대응)
    [HideInInspector] public Color BaseColor = Color.white;

    private void Awake()
    {
        srs = GetComponentsInChildren<SpriteRenderer>(true);
        if (srs.Length == 0) srs = GetComponentsInParent<SpriteRenderer>();

        CacheOriginalMats();
    }

    private void CacheOriginalMats()
    {
        originalMats = new Material[srs.Length];
        for (int i = 0; i < srs.Length; i++)
            originalMats[i] = srs[i].material;
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
        // 원본 재질이 바뀌었을 수 있으니 매번 갱신
        CacheOriginalMats();

        foreach (var sr in srs) sr.material = flashMat;
        yield return new WaitForSeconds(flashDuration);
        RestoreMats();
    }

    private void RestoreMats()
    {
        if (originalMats == null) return;
        for (int i = 0; i < srs.Length && i < originalMats.Length; i++)
            if (srs[i] != null && originalMats[i] != null)
                srs[i].material = originalMats[i];
    }

    private void OnDestroy()
    {
        // 파괴 시 재질 복구 (코루틴 중단으로 복구 안 된 경우 대비)
        RestoreMats();
    }
}
