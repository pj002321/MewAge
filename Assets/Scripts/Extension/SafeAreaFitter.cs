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
        lastSafeArea   = Screen.safeArea;
        lastScreen     = new Vector2Int(Screen.width, Screen.height);

        var screen    = new Vector2(Screen.width, Screen.height);
        rt.anchorMin  = lastSafeArea.position / screen;
        rt.anchorMax  = (lastSafeArea.position + lastSafeArea.size) / screen;
        rt.offsetMin  = Vector2.zero;
        rt.offsetMax  = Vector2.zero;
    }
}
