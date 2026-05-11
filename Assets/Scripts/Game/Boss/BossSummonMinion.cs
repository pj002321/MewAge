using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 보스 소환 미니언 태그 컴포넌트.
/// 이동·전투·HP는 같은 오브젝트의 EnemyController + Behaviour가 담당합니다.
/// 이 컴포넌트는 수명 타이머와 소환체 목록 관리만 합니다.
/// </summary>
public class BossSummonMinion : MonoBehaviour
{
    [SerializeField] private float maxHP = 30f;

    public static readonly List<BossSummonMinion> All = new();

    private EnemyController _ec;

    public void Init(float duration, float moveSpeed, float contactDamage)
    {
        _ec = GetComponent<EnemyController>();
        _ec?.ApplySummonStats(maxHP, contactDamage, moveSpeed);
        All.Add(this);
        StartCoroutine(LifeRoutine(duration));
    }

    private void OnDestroy() => All.Remove(this);

    private IEnumerator LifeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        _ec?.ForceDie();
    }
}
