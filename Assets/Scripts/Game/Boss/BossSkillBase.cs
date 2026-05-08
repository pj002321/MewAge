using System.Collections;
using UnityEngine;

/// <summary>
/// 보스 고유 스킬의 추상 기반 클래스.
/// BossController 와 같은 GameObject 에 MonoBehaviour 컴포넌트로 추가합니다.
/// </summary>
public abstract class BossSkillBase : MonoBehaviour
{
    [Header("스킬 공통")]
    [Tooltip("DB_보스 BS1/BS2/BS3 ID와 매핑. 예: TKD, CRG, ATK, RNG")]
    [SerializeField] private   string skillId           = "";
    public string SkillId => skillId;

    [SerializeField] protected float cooldown           = 5f;
    [Tooltip("분노 시 쿨다운에 곱하는 배율 (예: 0.7 = 30% 감소)")]
    [SerializeField] protected float enrageCooldownMult = 0.7f;
    [Tooltip("스킬 사용 가능 최대 거리 (0 = 제한 없음)")]
    [SerializeField] protected float maxRange           = 0f;
    [Tooltip("false면 PatternRoutine 스킬 선택에서 제외됩니다 (자율 루프 스킬용)")]
    [SerializeField] public    bool  executeInPattern   = true;

    protected BossController Boss;
    // Owner = BossController (이동·발사 등 공통 API 제공)
    protected BossController Owner => Boss;

    private float cooldownTimer;

    public bool IsReady => cooldownTimer <= 0f;

    public virtual void Init(BossController boss)
    {
        Boss = boss;
    }

    public void TickCooldown(float delta)
    {
        if (cooldownTimer > 0f) cooldownTimer -= delta;
    }

    public IEnumerator Execute()
    {
        if (Boss != null && Boss.IsGrogy) yield break;
        cooldownTimer = Boss != null && Boss.IsEnraged
            ? cooldown * enrageCooldownMult
            : cooldown;
        yield return OnExecute();
    }

    protected abstract IEnumerator OnExecute();

    public bool CanUse(float distToPlayer)
    {
        if (!IsReady) return false;
        if (maxRange > 0f && distToPlayer > maxRange) return false;
        return true;
    }
}
