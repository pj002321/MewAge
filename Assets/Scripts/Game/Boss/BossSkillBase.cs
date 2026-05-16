using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

/// <summary>
/// 보스 스킬 추상 기반 클래스.
/// BossController 인스펙터의 Skills 리스트에 인라인으로 추가합니다.
/// 새 스킬: OnExecuteAsync(CancellationToken)을 override하세요.
/// </summary>
[System.Serializable]
public abstract class BossSkillBase
{
    [Tooltip("이 스킬 실행 완료 후 다음 스킬까지 대기 시간 (초)")]
    [SerializeField] public float cooldown        = 2f;
    [Tooltip("false면 PatternLoop 스킬 선택에서 제외됩니다")]
    [SerializeField] public bool  executeInPattern = true;

    protected BossController Boss    { get; private set; }
    protected Transform      PlayerT => Boss?.PlayerTransform;

    public virtual void Init(BossController boss) { Boss = boss; }

    /// <summary>PatternLoopAsync에서 호출합니다. 그로기 상태면 즉시 반환합니다.</summary>
    public UniTask ExecuteAsync(CancellationToken ct)
    {
        if (Boss != null && Boss.IsGrogy) return UniTask.CompletedTask;
        return OnExecuteAsync(ct);
    }

    protected abstract UniTask OnExecuteAsync(CancellationToken ct);

    /// <summary>그로기·사망 등 외부 중단 시 호출합니다. 파생 클래스에서 상태 복구를 구현하세요.</summary>
    public virtual void OnInterrupted() { }
}
