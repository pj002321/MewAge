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

    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    private EnemyController _ec;

    public void Init(float duration, float moveSpeed, float contactDamage)
    {
        _ec = GetComponent<EnemyController>();
        if (_ec != null)
        {
            _ec.ApplySummonStats(maxHP, contactDamage, moveSpeed);
            _ec.WakeFromPool();  // 비활성 프리팹 기준으로 스폰되므로 컨트롤러·Behaviour 활성화
        }
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
