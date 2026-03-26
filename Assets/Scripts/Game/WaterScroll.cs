using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// RawImage의 uvRect를 스크롤해 물이 흐르는 효과를 냅니다.
/// RawImage 컴포넌트와 함께 사용하세요.
/// Texture Import Settings → Wrap Mode를 Repeat으로 설정해야 합니다.
/// </summary>
[RequireComponent(typeof(RawImage))]
public class WaterScroll : MonoBehaviour
{
    [Header("스크롤 속도 (UV 단위/초)")]
    public float speedX = 0.3f;
    public float speedY = 0.05f;

    [Header("타일링 (값이 클수록 텍스처가 작게 반복됨)")]
    public float tilingX = 3f;
    public float tilingY = 5f;

    RawImage rawImage;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
        // 초기 타일링 크기 설정
        var r   = rawImage.uvRect;
        r.width  = tilingX;
        r.height = tilingY;
        rawImage.uvRect = r;
    }

    void Update()
    {
        var r = rawImage.uvRect;
        r.x += speedX * Time.unscaledDeltaTime;
        r.y += speedY * Time.unscaledDeltaTime;
        rawImage.uvRect = r;
    }
}
