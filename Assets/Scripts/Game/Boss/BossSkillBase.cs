using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 고유 스킬의 추상 기반 클래스.
/// BossController 와 같은 GameObject 에 MonoBehaviour 컴포넌트로 추가합니다.
///
/// 스킬은 순차 플레이리스트 방식으로 실행됩니다.
/// PatternLoopAsync: 스킬[0] 실행 → cooldown 대기 → 스킬[1] 실행 → cooldown 대기 → ...
///
/// 신규 스킬: OnExecuteAsync(CancellationToken) 을 override.
/// 기존 스킬: OnExecute() IEnumerator 를 override — 자동으로 UniTask 브리지로 래핑됩니다.
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

    public virtual void Init(BossController boss) { Boss = boss; }

    /// <summary>BossController.PatternLoopAsync 에서 호출합니다.</summary>
    public UniTask ExecuteAsync(CancellationToken ct)
    {
        if (Boss != null && Boss.IsGrogy) return UniTask.CompletedTask;
        return OnExecuteAsync(ct);
    }

    /// <summary>
    /// 기본 구현: IEnumerator OnExecute() 를 UniTask 브리지로 래핑합니다.
    /// UniTask로 직접 구현하는 서브클래스는 이 메서드를 override 하세요.
    /// </summary>
    protected virtual UniTask OnExecuteAsync(CancellationToken ct)
    {
        if (ct.IsCancellationRequested) return UniTask.CompletedTask;
        return OnExecute().ToUniTask(this);
    }

    /// <summary>기존 IEnumerator 스킬은 이 메서드를 override 합니다.</summary>
    protected virtual IEnumerator OnExecute() { yield break; }

    /// <summary>BossBehaviour / BossPatternBehaviour 등 코루틴 기반 호출자용.</summary>
    public IEnumerator Execute()
    {
        if (Boss != null && Boss.IsGrogy) yield break;
        yield return OnExecute();
    }

    /// <summary>스킬 도중 외부에서 중단될 때 호출 (그로기, 사망 등). 서브클래스에서 상태 복구.</summary>
    public virtual void OnInterrupted() { StopAllCoroutines(); }
}
