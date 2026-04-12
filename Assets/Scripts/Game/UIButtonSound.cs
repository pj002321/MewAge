using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 버튼에 붙여서 Click 사운드를 재생합니다.
/// onClick.AddListener 방식으로 연결하므로 클릭 시 정확히 1회만 재생됩니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class UIButtonSound : MonoBehaviour
{
    [SerializeField] private SoundKey clickKey = SoundKey.UI_Click;

    public void SetKeys(SoundKey hover, SoundKey click)
    {
        clickKey = click;
    }

    private void Awake()
    {
        GetComponent<Button>().onClick.AddListener(() => SoundManager.Shared?.Play(clickKey));
    }
}
