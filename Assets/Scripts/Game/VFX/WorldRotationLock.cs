using UnityEngine;

/// <summary>
/// 부모 계층의 회전을 무시하고 월드 회전을 identity로 고정합니다.
/// ThunderSkill / IceSkill 등에서 적 자식으로 스폰되는 VFX에 런타임으로 추가됩니다.
/// </summary>
public class WorldRotationLock : MonoBehaviour
{
    private void LateUpdate()
    {
        transform.rotation = Quaternion.identity;
    }
}
