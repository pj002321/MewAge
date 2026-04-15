using UnityEngine;

/// <summary>
/// Data container for rolling projectiles.
/// </summary>
[CreateAssetMenu(fileName = "BossRollingProjectileData", menuName = "Mewage/Boss/RollingProjectileData")]
public class BossRollingProjectileData : ScriptableObject
{
    [Header("Basic Settings")]
    public float Damage = 10f;
    public float Speed = 5f;
    public float LifeTime = 6f;
    public int MaxBounces = 3;
    
    [Header("Visual Settings")]
    public float RotateSpeed = 360f;
}
