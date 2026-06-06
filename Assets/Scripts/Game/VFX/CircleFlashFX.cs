using System.Collections;
using UnityEngine;

/// <summary>
/// 수류탄 폭발 범위를 표시하는 원형 흰색 깜빡임 이펙트.
/// 런타임에 원형 텍스처를 생성하므로 별도 에셋이 필요하지 않습니다.
/// </summary>
public class CircleFlashFX : MonoBehaviour
{
    private static Sprite _cachedSprite;

    /// <summary>지정 위치에 폭발 반경에 맞는 원형 플래시를 재생합니다.</summary>
    public static void Play(Vector3 center, float radius, int sortingLayerID = 0)
    {
        var go = new GameObject("CircleFlash");
        go.transform.position = center;
        go.AddComponent<CircleFlashFX>().Initialize(radius, sortingLayerID);
    }

    private void Initialize(float radius, int sortingLayerID)
    {
        var sr = gameObject.AddComponent<SpriteRenderer>();
        sr.sprite         = GetCircleSprite();
        sr.color          = Color.white;
        sr.sortingLayerID = sortingLayerID;
        sr.sortingOrder   = 100;

        float diameter = radius * 2f;
        transform.localScale = new Vector3(diameter, diameter, 1f);

        StartCoroutine(FlashRoutine(sr));
    }

    private IEnumerator FlashRoutine(SpriteRenderer sr)
    {
        // on → off → on → fade out
        float[] durations = { 0.06f, 0.04f, 0.07f, 0.15f };
        float[] endAlphas = { 1.0f,  0.0f,  0.8f,  0.0f  };

        float startAlpha = 0f;
        for (int i = 0; i < durations.Length; i++)
        {
            float endAlpha = endAlphas[i];
            float dur      = durations[i];
            for (float t = 0f; t < dur; t += Time.deltaTime)
            {
                if (sr == null) yield break;
                sr.color = new Color(1f, 1f, 1f, Mathf.Lerp(startAlpha, endAlpha, t / dur));
                yield return null;
            }
            if (sr != null) sr.color = new Color(1f, 1f, 1f, endAlpha);
            startAlpha = endAlpha;
        }

        Destroy(gameObject);
    }

    private static Sprite GetCircleSprite()
    {
        if (_cachedSprite != null) return _cachedSprite;

        const int size = 128;
        const float r  = size * 0.5f;
        var center     = new Vector2(r, r);
        var tex        = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                tex.SetPixel(x, y, Vector2.Distance(new Vector2(x, y), center) <= r
                    ? Color.white : Color.clear);
        tex.Apply();

        _cachedSprite = Sprite.Create(tex, new Rect(0, 0, size, size), Vector2.one * 0.5f, size);
        return _cachedSprite;
    }
}
