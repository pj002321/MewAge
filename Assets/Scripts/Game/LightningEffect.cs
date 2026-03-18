using UnityEngine;

/// <summary>
/// 번개 비주얼 컴포넌트.
/// 프리팹에 SpriteRenderer(번개 스프라이트)를 붙이고,
/// 이 컴포넌트를 함께 추가하면 됩니다.
/// autoDestroyTime 후 자동 소멸합니다.
/// </summary>
public class LightningEffect : MonoBehaviour
{
    [SerializeField] private float autoDestroyTime = 0.4f;

    private void Start()
    {
        Destroy(gameObject, autoDestroyTime);
    }
}
