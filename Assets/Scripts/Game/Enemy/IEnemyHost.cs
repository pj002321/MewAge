using UnityEngine;

/// <summary>
/// EnemyController · BossController 모두에 공통으로 노출되는 적 호스트 API.
/// EnemyCombatRoutines 등 공유 행동 로직이 이 인터페이스를 통해 호스트에 접근합니다.
/// </summary>
public interface IEnemyHost
{
    Transform    SelfTransform  { get; }
    Rigidbody2D  Rb             { get; }
    Animator     EnemyAnimator  { get; }
    Transform    PlayerTransform { get; }
    Transform    Decal           { get; }
    float        MoveSpeed      { get; }
    float        ContactDamage  { get; }
    void         MoveInDirection(Vector2 dir, float speed);
    void         FlipToward(Vector2 dir);
    void         MoveTowardPlayer(float speed);
}
