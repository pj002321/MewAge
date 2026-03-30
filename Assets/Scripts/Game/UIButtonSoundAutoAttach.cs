using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 이 컴포넌트를 Canvas 루트(또는 임의 부모)에 붙이면
/// Awake 시점에 자식 계층 내 모든 Button에 UIButtonSound가 없을 경우 자동으로 추가합니다.
/// Home 씬 HomeUICanvas에 붙여서 사용하세요.
/// </summary>
public class UIButtonSoundAutoAttach : MonoBehaviour
{
    [SerializeField] private SoundKey clickKey = SoundKey.UI_Click;

    void Awake()
    {
        foreach (var btn in GetComponentsInChildren<Button>(includeInactive: true))
        {
            SoundKey capturedClick = clickKey;
            btn.onClick.AddListener(() => SoundManager.Shared?.Play(capturedClick));
        }
    }
}
