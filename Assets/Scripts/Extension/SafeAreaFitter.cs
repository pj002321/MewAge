using UnityEngine;

/// <summary>
/// 어태치된 RectTransform을 Screen.safeArea에 맞게 조정합니다.
/// iPhone 노치/다이나믹 아일랜드 및 iPad 여백을 자동으로 처리합니다.
/// Canvas 직계 자식 오브젝트에 부착하세요.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rt;
    Rect lastSafeArea = Rect.zero;
    Vector2Int lastScreen;

    void Awake()
    {
        rt = GetComponent<RectTransform>();
    }

    void Start()
    {
        Apply();
    }

    void Update()
    {
        var cur = new Vector2Int(Screen.width, Screen.height);
        if (Screen.safeArea != lastSafeArea || cur != lastScreen)
            Apply();
    }

    void Apply()
    {
        lastSafeArea = Screen.safeArea;
        lastScreen   = new Vector2Int(Screen.width, Screen.height);

        var screen = new Vector2(Screen.width, Screen.height);

        // 부모 RectTransform이 화면 전체를 채우지 않을 경우(AspectRatioFitter 등으로 축소된 경우)
        // Screen.safeArea는 화면 픽셀 기준이므로, 부모의 실제 화면 범위로 좌표를 변환해야 합니다.
        var parent = rt.parent as RectTransform;
        if (parent != null)
        {
            // Screen Space - Overlay 캔버스에서 GetWorldCorners는 화면 픽셀 좌표를 반환합니다.
            var corners = new Vector3[4];
            parent.GetWorldCorners(corners);
            // corners[0]=좌하단  corners[2]=우상단
            Vector2 parentMin  = corners[0];
            Vector2 parentMax  = corners[2];
            Vector2 parentSize = parentMax - parentMin;

            if (parentSize.x > 0f && parentSize.y > 0f)
            {
                Vector2 safeMin = (lastSafeArea.position - parentMin) / parentSize;
                Vector2 safeMax = (lastSafeArea.position + lastSafeArea.size - parentMin) / parentSize;
                rt.anchorMin = safeMin;
                rt.anchorMax = safeMax;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                return;
            }
        }

        // 폴백: 부모가 없거나 화면 전체를 채우는 경우
        rt.anchorMin = lastSafeArea.position / screen;
        rt.anchorMax = (lastSafeArea.position + lastSafeArea.size) / screen;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }
}
