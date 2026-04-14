using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 화면 비율을 감지해 iPad(와이드)에서는 가로 모드로, 스마트폰에서는 세로 모드로 전환합니다.
/// HomeUICanvas에 부착하세요.
/// </summary>
[RequireComponent(typeof(CanvasScaler))]
public class HomeOrientationAdapter : MonoBehaviour
{
    [Header("기준 해상도")]
    [SerializeField] Vector2 portraitResolution  = new Vector2(1080, 1920);
    [SerializeField] Vector2 landscapeResolution = new Vector2(1920, 1080);

    [Tooltip("width/height 이 비율 이상이면 와이드(iPad 가로)로 판단")]
    [SerializeField] float wideThreshold = 1.1f;

    CanvasScaler scaler;

    void Awake()
    {
        scaler = GetComponent<CanvasScaler>();
        Apply();
    }

    void Apply()
    {
        float aspect = (float)Screen.width / Screen.height;
        bool isWide  = aspect >= wideThreshold;

        if (isWide)
        {
            Screen.orientation         = ScreenOrientation.LandscapeLeft;
            scaler.referenceResolution = landscapeResolution;
            scaler.matchWidthOrHeight  = 0f;   // 가로 모드: 높이 기준
        }
        else
        {
            Screen.orientation         = ScreenOrientation.Portrait;
            scaler.referenceResolution = portraitResolution;
            scaler.matchWidthOrHeight  = 1f;   // 세로 모드: 높이 기준
        }
    }
}
