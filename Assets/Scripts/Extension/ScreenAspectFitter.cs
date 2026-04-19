using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// AspectRatioFitter의 aspectRatio를 현재 화면 비율로 자동 설정합니다.
/// AspectRatioFitter가 붙은 오브젝트에 함께 부착하세요.
/// </summary>
[RequireComponent(typeof(AspectRatioFitter))]
public class ScreenAspectFitter : MonoBehaviour
{
    [Tooltip("허용 최소 비율 (width/height). Z플립 등 초세로 화면 대응용. 0이면 제한 없음.")]
    [SerializeField] private float minRatio = 0f;
    [Tooltip("허용 최대 비율. 태블릿 등 가로 화면 대응용. 0이면 제한 없음.")]
    [SerializeField] private float maxRatio = 0f;

    AspectRatioFitter _fitter;
    int _lastWidth, _lastHeight;

    void Awake()
    {
        _fitter = GetComponent<AspectRatioFitter>();
        Apply();
    }

    void Update()
    {
        if (Screen.width != _lastWidth || Screen.height != _lastHeight)
            Apply();
    }

    void Apply()
    {
        float ratio = (float)Screen.width / Screen.height;

        if (minRatio > 0f) ratio = Mathf.Max(ratio, minRatio);
        if (maxRatio > 0f) ratio = Mathf.Min(ratio, maxRatio);

        _fitter.aspectRatio = ratio;
    }
}
