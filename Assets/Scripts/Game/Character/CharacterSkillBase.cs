using UnityEngine;

/// <summary>
/// 캐릭터 고유 스킬의 기반 클래스.
/// Resources/Skills/{skillId.ToLower()} 경로의 프리팹에 서브클래스를 부착하세요.
/// </summary>
public abstract class CharacterSkillBase : MonoBehaviour
{
    [Header("스킬 기본 설정")]
    [Tooltip("스킬 발동 지속시간 (초)")]
    [SerializeField] protected float duration = 5f;

    [Tooltip("스킬 게이지 최대치 (적 처치 횟수)")]
    [SerializeField] protected int pointsRequired = 10;

    public virtual float Duration => duration;
    public int   PointsRequired => pointsRequired;

    /// <summary>스킬 고유 ID (f_스킬ID와 일치해야 합니다)</summary>
    public abstract string SkillId { get; }

    /// <summary>스킬 발동 — CharacterSkillManager가 호출합니다.</summary>
    public abstract void Activate();

    /// <summary>스킬 종료 — duration 경과 후 또는 강제 종료 시 호출됩니다.</summary>
    public virtual void Deactivate() { }
}
