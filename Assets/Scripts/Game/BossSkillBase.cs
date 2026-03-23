using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 고유 스킬의 추상 기반 클래스.
/// BossController 와 같은 GameObject 에 MonoBehaviour 컴포넌트로 추가합니다.
///
/// 파생 예시:
///   BossSlamSkill   — 내려찍기
///   BossChargeSkill — 돌진
///   BossMagicSkill  — 마법 투사체
///   BossScratchSkill— 할퀴기
/// </summary>
public abstract class BossSkillBase : MonoBehaviour
{
    [Header("스킬 공통")]
    [SerializeField] protected float cooldown            = 5f;
    [Tooltip("분노 시 쿨다운에 곱하는 배율 (예: 0.7 = 30% 감소)")]
    [SerializeField] protected float enrageCooldownMult  = 0.7f;
    [Tooltip("스킬 사용 가능 최대 거리 (0 = 제한 없음)")]
    [SerializeField] protected float maxRange            = 0f;

    protected BossController  Boss;
    protected EnemyController Owner;

    private float cooldownTimer;

    /// <summary>스킬이 쿨다운 없이 준비된 상태인지 여부.</summary>
    public bool IsReady => cooldownTimer <= 0f;

    /// <summary>BossController.Awake() 에서 자동으로 호출됩니다.</summary>
    public virtual void Init(BossController boss, EnemyController owner)
    {
        Boss  = boss;
        Owner = owner;
    }

    /// <summary>매 프레임 BossController.Update() 에서 호출합니다.</summary>
    public void TickCooldown(float delta)
    {
        if (cooldownTimer > 0f) cooldownTimer -= delta;
    }

    /// <summary>
    /// 스킬 실행. BossBehaviour 의 코루틴에서 yield return skill.Execute() 로 호출합니다.
    /// 쿨다운을 설정한 뒤 OnExecute() 를 실행합니다.
    /// </summary>
    public IEnumerator Execute()
    {
        cooldownTimer = Boss != null && Boss.IsEnraged
            ? cooldown * enrageCooldownMult
            : cooldown;
        yield return OnExecute();
    }

    /// <summary>스킬 본체. 파생 클래스에서 override 합니다.</summary>
    protected abstract IEnumerator OnExecute();

    /// <summary>현재 플레이어와의 거리에서 이 스킬을 사용할 수 있는지 확인합니다.</summary>
    public bool CanUse(float distToPlayer)
    {
        if (!IsReady) return false;
        if (maxRange > 0f && distToPlayer > maxRange) return false;
        return true;
    }
}
