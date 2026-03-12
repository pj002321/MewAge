using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player에 부착. 모든 WeaponPoint를 관리하고
/// 같은 적을 두 무기가 동시에 공격하지 않도록 조율합니다.
/// </summary>
public class WeaponManager : MonoBehaviour
{
    public static WeaponManager Instance { get; private set; }

    // 현재 어떤 무기가 공격 중인 적 목록
    private readonly HashSet<EnemyController> claimedEnemies = new();

    private void Awake()
    {
        Instance = this;
    }

    /// <summary>적을 예약합니다. 이미 다른 무기가 공격 중이면 false.</summary>
    public bool TryClaim(EnemyController enemy)
    {
        if (enemy == null) return false;
        return claimedEnemies.Add(enemy);
    }

    /// <summary>공격이 끝나면 예약 해제합니다.</summary>
    public void Release(EnemyController enemy)
    {
        if (enemy != null)
            claimedEnemies.Remove(enemy);
    }

    public bool IsClaimed(EnemyController enemy) => claimedEnemies.Contains(enemy);
}
