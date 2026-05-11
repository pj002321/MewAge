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

    private void EnsureComponents()
    {
        if (_rawImage == null) _rawImage = GetComponent<RawImage>();
    }

    public Color Tint
    {
        get => tint;
        set { tint = value; if (_rawImage != null && _rt != null) _rawImage.color = value; }
    }

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
        EnsureComponents();

        panel.SetActive(false);
        yield return new WaitForEndOfFrame();

        bool blurOk = false;
        if (_rawImage != null)
        {
            try
            {
                int w = Mathf.Max(1, Mathf.RoundToInt(Screen.width  * downscale));
                int h = Mathf.Max(1, Mathf.RoundToInt(Screen.height * downscale));

                var src = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGB32);
                ScreenCapture.CaptureScreenshotIntoRenderTexture(src);

                if (_rt != null) { _rt.Release(); Destroy(_rt); }
                _rt = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(src, _rt);
                src.Release();

                if (blurMaterial != null)
                {
                    var tmp = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32);
                    for (int i = 0; i < iterations; i++)
                    {
                        Graphics.Blit(_rt, tmp, blurMaterial, 0);
                        Graphics.Blit(tmp, _rt, blurMaterial, 1);
                    }
                    RenderTexture.ReleaseTemporary(tmp);
                }

                _rawImage.texture = _rt;
                _rawImage.color   = tint;
                _rawImage.uvRect  = new Rect(0, 0, 1, 1);
                blurOk = true;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[BlurBackground] 캡처 실패, 블러 없이 패널을 표시합니다: {e.Message}");
            }
        }

        if (!blurOk && _rawImage != null)
            _rawImage.gameObject.SetActive(false); // 블러 실패 시 RawImage 숨기고 패널만 표시

        panel.SetActive(true);
    }
}
