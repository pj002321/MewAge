using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 고유 스킬의 추상 기반 클래스.
/// BossController 와 같은 GameObject 에 MonoBehaviour 컴포넌트로 추가합니다.
///
/// 스킬은 순차 플레이리스트 방식으로 실행됩니다.
/// PatternRoutine: 스킬[0] 실행 → cooldown 대기 → 스킬[1] 실행 → cooldown 대기 → ...
/// </summary>
public abstract class BossSkillBase : MonoBehaviour
{
    [Header("스킬 공통")]
    [Tooltip("false면 PatternRoutine 스킬 선택에서 제외됩니다 (자율 루프 스킬용)")]
    [SerializeField] public bool  executeInPattern = true;
    [Tooltip("이 스킬 실행 완료 후 다음 스킬까지 대기 시간 (초). 분노 시 0.6배 적용")]
    [SerializeField] public float cooldown         = 2f;

    protected BossController Boss;
    protected BossController Owner => Boss;

    public virtual void Init(BossController boss)
    {
        Boss = boss;
    }

    public IEnumerator Execute()
    {
        if (Boss != null && Boss.IsGrogy) yield break;
        yield return OnExecute();
    }

    protected abstract IEnumerator OnExecute();
}
