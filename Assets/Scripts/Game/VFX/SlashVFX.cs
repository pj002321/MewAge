using UnityEngine;

/// <summary>
/// 슬래시 VFX 프리팹에 부착하여 자동 파괴.
/// duration 후 게임 오브젝트를 파괴합니다.
/// </summary>
public class SlashVFX : MonoBehaviour
{
    [SerializeField] private float duration = 1f;

    private void Start()
    {
        Destroy(gameObject, duration);
    }
}