using UnityEngine;

/// <summary>
/// 슬래시 VFX 프리팹에 부착하여 자동 파괴.
/// SpriteForward로 스프라이트의 기본 전방향을 지정하면 SpawnSlashVfx에서
/// 전달된 방향(midDir)으로 자동 회전합니다.
/// </summary>
public class SlashVFX : MonoBehaviour
{
    public enum SpriteForward { Up, Right, Down, Left }

    [SerializeField] private float         duration      = 1f;
    [SerializeField] private SpriteForward spriteForward = SpriteForward.Up;

    private float ForwardAngle => spriteForward switch
    {
        SpriteForward.Up    =>  90f,
        SpriteForward.Right =>   0f,
        SpriteForward.Down  => -90f,
        SpriteForward.Left  => 180f,
        _                   =>  90f,
    };

    /// <summary>
    /// 스폰 직후 방향 적용. WeaponPoint.SlashSequenceRoutine에서 SpawnSlashVfx가 호출합니다.
    /// </summary>
    public void SetDirection(Vector2 dir)
    {
        if (dir.sqrMagnitude < 0.001f) return;
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - ForwardAngle;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Start()
    {
        Destroy(gameObject, duration);
    }
}