using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Canvas 루트(또는 임의 부모)에 붙이면 자식 Button 전체에
/// UIButtonPressAnimation을 자동으로 추가합니다.
/// UIButtonSoundAutoAttach와 동일한 방식으로 사용하세요.
/// </summary>
public class UIButtonPressAutoAttach : MonoBehaviour
{
    [Header("기본 설정 (개별 버튼에서 override 가능)")]
    [Range(0.5f, 1f)]    public float pressScale      = 0.88f;
    [Range(1f, 1.3f)]    public float bounceScale     = 1.06f;
    [Range(0.02f, 0.2f)] public float pressDuration   = 0.07f;
    [Range(0.05f, 0.4f)] public float releaseDuration = 0.18f;

    void Awake()
    {
        foreach (var btn in GetComponentsInChildren<Button>(includeInactive: true))
        {
            if (btn.GetComponent<UIButtonPressAnimation>() != null) continue;

            var anim             = btn.gameObject.AddComponent<UIButtonPressAnimation>();
            anim.pressScale      = pressScale;
            anim.bounceScale     = bounceScale;
            anim.pressDuration   = pressDuration;
            anim.releaseDuration = releaseDuration;
        }
    }
}
