using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class BlurBackground : MonoBehaviour
{
    [SerializeField] public Material blurMaterial;
    [SerializeField] public int      iterations = 3;
    [SerializeField] public float    downscale  = 1f;               // 1 = 원본 해상도 (확대 없음)
    [SerializeField] public Color    tint       = new Color(0f, 0f, 0f, 0.55f); // 어두운 오버레이

    private RawImage      _rawImage;
    private RenderTexture _rt;

    private void Awake() => _rawImage = GetComponent<RawImage>();

    private void OnDisable()
    {
        if (_rt == null) return;
        _rawImage.texture = null;
        _rawImage.color   = Color.white;
        _rt.Release();
        Destroy(_rt);
        _rt = null;
    }

    public IEnumerator Capture(GameObject panel)
    {
        panel.SetActive(false);
        yield return new WaitForEndOfFrame();

        int w = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * downscale));
        int h = Mathf.Max(1, Mathf.RoundToInt(Screen.height * downscale));

        // 화면 캡처 (원본 해상도로 받아서 dst로 Blit — 확대 방지)
        var src = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
        ScreenCapture.CaptureScreenshotIntoRenderTexture(src);

        if (_rt != null) { _rt.Release(); Destroy(_rt); }
        _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(src, _rt); // 다운스케일 Blit
        src.Release();

        // 블러
        if (blurMaterial != null)
        {
            var tmp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
            for (int i = 0; i < iterations; i++)
            {
                Graphics.Blit(_rt, tmp, blurMaterial, 0); // 수평
                Graphics.Blit(tmp, _rt, blurMaterial, 1); // 수직
            }
            RenderTexture.ReleaseTemporary(tmp);
        }

        _rawImage.texture = _rt;
        _rawImage.color   = tint;          // 어두운 톤 적용
        _rawImage.uvRect  = new Rect(0, 0, 1, 1); // UV 정규화 (확대 방지)
        panel.SetActive(true);
    }
}
